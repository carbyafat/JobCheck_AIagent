using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JobCheck.Domain;
using NUnit.Framework;

namespace JobCheck.Persistence.Tests
{
    /// <summary>
    /// 使用暫存 golden fixture 驗證 V0.1 demo migration 的安全性與狀態映射。
    /// </summary>
    public sealed class JobCheckV01MigrationTests
    {
        private static readonly DateTimeOffset MigrationTime =
            new DateTimeOffset(2026, 9, 8, 20, 0, 0, TimeSpan.FromHours(8));

        [Test]
        public void Run_ValidDemoData_CreatesDeduplicatedV02SnapshotAndBackup()
        {
            string root = CreateTestRoot();
            try
            {
                WriteLegacyJob(root, "demo_job_001", " 測試  公司 ", "not_viewed");
                WriteLegacyJob(root, "demo_job_002", "測試 公司", "not_viewed");
                WriteTracking(root, "demo_job_001", "waiting_reply", favorite: true);
                WriteTracking(root, "demo_job_002", "not_viewed", favorite: false, hasAction: false);
                string original = File.ReadAllText(
                    Path.Combine(root, "jobs", "demo_job_001.json"));
                string target = Path.Combine(root, "data");

                JobCheckMigrationResult result =
                    JobCheckV01Migration.Run(root, target, MigrationTime);
                PersistenceStorageResult<JobCheckDataSet> loaded =
                    JobCheckDataRepository.Load(target);

                Assert.That(result.IsSuccess, Is.True, FormatIssues(result.Issues));
                Assert.That(result.CompanyCount, Is.EqualTo(1), "company count");
                Assert.That(result.JobPostingCount, Is.EqualTo(2), "job count");
                Assert.That(result.ApplicationCount, Is.EqualTo(1), "application count");
                Assert.That(loaded.IsSuccess, Is.True, FormatStorageIssues(loaded.Issues));
                Assert.That(loaded.Value.ApplicationEvents.Count, Is.EqualTo(1));
                Assert.That(loaded.Value.ApplicationEvents[0].SourceReference,
                    Is.EqualTo("job_tracking/demo_job_001.tracking.json"));
                Assert.That(loaded.Value.Applications[0].CurrentStage,
                    Is.EqualTo(ApplicationStage.WaitingResponse));
                Assert.That(File.ReadAllText(
                    Path.Combine(root, "jobs", "demo_job_001.json")), Is.EqualTo(original));
                Assert.That(Directory.GetDirectories(
                    Path.Combine(target, "migration"), "backup_v0.1_*"), Has.Length.EqualTo(1));
                Assert.That(File.Exists(Path.Combine(target, "migration", "manifest.json")), Is.True);
                Assert.That(File.Exists(Path.Combine(target, "migration", "report.json")), Is.True);
            }
            finally
            {
                DeleteTestRoot(root);
            }
        }

        [Test]
        public void Run_SameLegacyKeys_ProducesStableIdsAcrossIndependentRuns()
        {
            string firstRoot = CreateTestRoot();
            string secondRoot = CreateTestRoot();
            try
            {
                WriteLegacyJob(firstRoot, "demo_job_001", "測試公司", "waiting_reply");
                WriteLegacyJob(secondRoot, "demo_job_001", "測試公司", "waiting_reply");

                JobCheckV01Migration.Run(firstRoot, Path.Combine(firstRoot, "data"), MigrationTime);
                JobCheckV01Migration.Run(secondRoot, Path.Combine(secondRoot, "data"),
                    MigrationTime.AddDays(1));
                JobCheckDataSet first = JobCheckDataRepository.Load(
                    Path.Combine(firstRoot, "data")).Value;
                JobCheckDataSet second = JobCheckDataRepository.Load(
                    Path.Combine(secondRoot, "data")).Value;

                Assert.That(second.Companies[0].Id, Is.EqualTo(first.Companies[0].Id));
                Assert.That(second.Applications[0].Id, Is.EqualTo(first.Applications[0].Id));
                Assert.That(second.ApplicationEvents[0].Id,
                    Is.EqualTo(first.ApplicationEvents[0].Id));
            }
            finally
            {
                DeleteTestRoot(firstRoot);
                DeleteTestRoot(secondRoot);
            }
        }

        [TestCase("interested", ApplicationStage.Saved)]
        [TestCase("not_applying", ApplicationStage.ClosedByCandidate)]
        [TestCase("applied", ApplicationStage.Applied)]
        [TestCase("interview_scheduled", ApplicationStage.InterviewScheduled)]
        [TestCase("interviewing", ApplicationStage.InterviewCompleted)]
        [TestCase("waiting_reply", ApplicationStage.WaitingResponse)]
        [TestCase("offer", ApplicationStage.OfferReceived)]
        [TestCase("rejected", ApplicationStage.RejectedByCompany)]
        [TestCase("closed", ApplicationStage.ClosedByCandidate)]
        public void Run_KnownLegacyStatus_MapsToExpectedSnapshot(
            string status,
            ApplicationStage expectedStage)
        {
            string root = CreateTestRoot();
            try
            {
                WriteLegacyJob(root, "demo_job_001", "測試公司", status);

                JobCheckMigrationResult result = JobCheckV01Migration.Run(
                    root, Path.Combine(root, "data"), MigrationTime);
                JobCheckDataSet dataSet = JobCheckDataRepository.Load(
                    Path.Combine(root, "data")).Value;

                Assert.That(result.IsSuccess, Is.True, FormatIssues(result.Issues));
                Assert.That(dataSet.Applications[0].CurrentStage, Is.EqualTo(expectedStage));
                Assert.That(dataSet.ApplicationEvents[0].EventType,
                    Is.EqualTo(ApplicationEventType.MigrationSnapshot));
                Assert.That(dataSet.ApplicationEvents[0].SnapshotStage,
                    Is.EqualTo(expectedStage));
            }
            finally
            {
                DeleteTestRoot(root);
            }
        }

        [Test]
        public void Run_EmptyNotViewedTracking_DoesNotInventApplication()
        {
            string root = CreateTestRoot();
            try
            {
                WriteLegacyJob(root, "demo_job_001", "測試公司", "not_viewed");

                JobCheckMigrationResult result = JobCheckV01Migration.Run(
                    root, Path.Combine(root, "data"), MigrationTime);
                JobCheckDataSet dataSet = JobCheckDataRepository.Load(
                    Path.Combine(root, "data")).Value;

                Assert.That(result.IsSuccess, Is.True, FormatIssues(result.Issues));
                Assert.That(dataSet.Applications, Is.Empty);
                Assert.That(dataSet.ApplicationEvents, Is.Empty);
            }
            finally
            {
                DeleteTestRoot(root);
            }
        }

        [Test]
        public void Run_FavoriteNotViewedTracking_CreatesSavedApplication()
        {
            string root = CreateTestRoot();
            try
            {
                WriteLegacyJob(root, "demo_job_001", "測試公司", "not_viewed");
                WriteTracking(root, "demo_job_001", "not_viewed", favorite: true, hasAction: false);

                JobCheckMigrationResult result = JobCheckV01Migration.Run(
                    root, Path.Combine(root, "data"), MigrationTime);
                JobCheckDataSet dataSet = JobCheckDataRepository.Load(
                    Path.Combine(root, "data")).Value;

                Assert.That(result.IsSuccess, Is.True, FormatIssues(result.Issues));
                Assert.That(dataSet.Applications, Has.Count.EqualTo(1));
                Assert.That(dataSet.Applications[0].CurrentStage,
                    Is.EqualTo(ApplicationStage.Saved));
                Assert.That(dataSet.Applications[0].IsFavorite, Is.True);
            }
            finally
            {
                DeleteTestRoot(root);
            }
        }

        [Test]
        public void Run_SeparateTracking_OverridesEmbeddedTracking()
        {
            string root = CreateTestRoot();
            try
            {
                WriteLegacyJob(root, "demo_job_001", "測試公司", "interested");
                WriteTracking(root, "demo_job_001", "rejected", favorite: false);

                JobCheckV01Migration.Run(root, Path.Combine(root, "data"), MigrationTime);
                JobCheckDataSet dataSet = JobCheckDataRepository.Load(
                    Path.Combine(root, "data")).Value;

                Assert.That(dataSet.Applications[0].CurrentStage,
                    Is.EqualTo(ApplicationStage.RejectedByCompany));
                Assert.That(dataSet.Applications[0].LegacyStatus, Is.EqualTo("rejected"));
            }
            finally
            {
                DeleteTestRoot(root);
            }
        }

        [Test]
        public void Run_NonDemoSource_IsRejectedWithoutCreatingTarget()
        {
            string root = CreateTestRoot();
            try
            {
                LegacyV01JobDto job = CreateLegacyJob("demo_job_001", "測試公司", "not_viewed");
                job.source.platform = "real-platform";
                WriteLegacyJobFile(root, job);
                string target = Path.Combine(root, "data");

                JobCheckMigrationResult result =
                    JobCheckV01Migration.Run(root, target, MigrationTime);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Issues.Select(item => item.Error),
                    Does.Contain(JobCheckMigrationError.NonDemoSourceRejected));
                Assert.That(Directory.Exists(target), Is.False);
            }
            finally
            {
                DeleteTestRoot(root);
            }
        }

        [Test]
        public void Run_UnknownStatus_IsRejectedAndKeepsBackupInWorkDirectory()
        {
            string root = CreateTestRoot();
            try
            {
                WriteLegacyJob(root, "demo_job_001", "測試公司", "mystery");

                JobCheckMigrationResult result = JobCheckV01Migration.Run(
                    root, Path.Combine(root, "data"), MigrationTime);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Issues.Select(item => item.Error),
                    Does.Contain(JobCheckMigrationError.UnknownLegacyStatus));
                Assert.That(Directory.Exists(Path.Combine(result.WorkRoot, "backup")), Is.True);
                Assert.That(File.Exists(Path.Combine(result.WorkRoot, "report.failed.json")), Is.True);
            }
            finally
            {
                DeleteTestRoot(root);
            }
        }

        [Test]
        public void Run_ExistingTargetContent_IsNeverOverwritten()
        {
            string root = CreateTestRoot();
            try
            {
                WriteLegacyJob(root, "demo_job_001", "測試公司", "not_viewed");
                string target = Path.Combine(root, "data");
                Directory.CreateDirectory(target);
                string sentinel = Path.Combine(target, "keep.txt");
                File.WriteAllText(sentinel, "保留");

                JobCheckMigrationResult result =
                    JobCheckV01Migration.Run(root, target, MigrationTime);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Issues.Select(item => item.Error),
                    Does.Contain(JobCheckMigrationError.TargetAlreadyExists));
                Assert.That(File.ReadAllText(sentinel), Is.EqualTo("保留"));
            }
            finally
            {
                DeleteTestRoot(root);
            }
        }

        private static void WriteLegacyJob(
            string root,
            string id,
            string companyName,
            string embeddedStatus)
        {
            WriteLegacyJobFile(root, CreateLegacyJob(id, companyName, embeddedStatus));
        }

        private static LegacyV01JobDto CreateLegacyJob(
            string id,
            string companyName,
            string embeddedStatus)
        {
            return new LegacyV01JobDto
            {
                schema_version = "0.1",
                id = id,
                source = new LegacyV01SourceDto
                {
                    platform = "demo",
                    url = "https://example.com/jobs/" + id,
                    captured_at = "2026-06-20T12:50:00+08:00"
                },
                company = new LegacyV01CompanyDto
                {
                    name = companyName,
                    industry = "測試資料"
                },
                job = new LegacyV01JobInfoDto
                {
                    title = "Unity 工程師",
                    category = "軟體工程師",
                    raw_text = "Unity 工程師"
                },
                responsibilities = new List<string> { "開發與維護" },
                tracking = new LegacyV01EmbeddedTrackingDto
                {
                    status = embeddedStatus,
                    favorite = false
                }
            };
        }

        private static void WriteLegacyJobFile(string root, LegacyV01JobDto job)
        {
            string directory = Path.Combine(root, "jobs");
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, job.id + ".json"),
                PersistenceJsonSerializer.Serialize(job));
        }

        private static void WriteTracking(
            string root,
            string jobId,
            string status,
            bool favorite,
            bool hasAction = true)
        {
            string directory = Path.Combine(root, "job_tracking");
            Directory.CreateDirectory(directory);
            var tracking = new LegacyV01TrackingDto
            {
                job_id = jobId,
                status = status,
                last_action_at = hasAction ? "2026-06-20T16:39:20+08:00" : string.Empty,
                manual_expire_at = string.Empty,
                favorite = favorite,
                fit_score = -1
            };
            File.WriteAllText(
                Path.Combine(directory, jobId + ".tracking.json"),
                PersistenceJsonSerializer.Serialize(tracking));
        }

        private static string CreateTestRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "JobCheckMigrationTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteTestRoot(string root)
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private static string FormatIssues(IEnumerable<JobCheckMigrationIssue> issues)
        {
            return string.Join(" | ", issues.Select(item =>
                item.Severity + ":" + item.Error + ":" + item.Message));
        }

        private static string FormatStorageIssues(IEnumerable<PersistenceStorageIssue> issues)
        {
            return string.Join(" | ", issues.Select(item =>
                item.Error + ":" + item.FilePath + ":" + item.Message));
        }
    }
}
