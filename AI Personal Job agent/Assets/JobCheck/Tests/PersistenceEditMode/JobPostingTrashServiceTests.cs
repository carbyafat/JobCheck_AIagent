using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JobCheck.Domain;
using NUnit.Framework;

namespace JobCheck.Persistence.Tests
{
    /// <summary>
    /// 驗證刪除職缺只移動目標關聯檔案，且公司與其他職缺不受影響。
    /// </summary>
    public sealed class JobPostingTrashServiceTests
    {
        private const string CompanyId = "cmp_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string TargetJobId = "job_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string OtherJobId = "job_cccccccccccccccccccccccccccccccc";
        private const string TargetApplicationId = "app_dddddddddddddddddddddddddddddddd";
        private const string OtherApplicationId = "app_eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";

        [Test]
        public void MoveToTrash_JobAndRelatedApplication_AreRecoverable()
        {
            string root = CreateUnusedPath();
            try
            {
                WriteData(root);

                PersistenceStorageResult<JobPostingTrashSummary> result =
                    JobPostingTrashService.MoveToTrash(root, TargetJobId);

                Assert.That(result.IsSuccess, Is.True, FormatIssues(result.Issues));
                Assert.That(result.Value.ApplicationCount, Is.EqualTo(1));
                Assert.That(File.Exists(Path.Combine(
                    root,
                    JobCheckDataRepository.JobsDirectoryName,
                    TargetJobId + ".json")), Is.False);
                Assert.That(File.Exists(Path.Combine(
                    root,
                    JobCheckDataRepository.ApplicationsDirectoryName,
                    TargetApplicationId + ".json")), Is.False);
                Assert.That(File.Exists(Path.Combine(
                    result.Value.TrashDirectory,
                    JobCheckDataRepository.JobsDirectoryName,
                    TargetJobId + ".json")), Is.True);
                Assert.That(File.Exists(Path.Combine(
                    result.Value.TrashDirectory,
                    JobCheckDataRepository.ApplicationsDirectoryName,
                    TargetApplicationId + ".json")), Is.True);
            }
            finally
            {
                DeleteTestDirectory(root);
            }
        }

        [Test]
        public void MoveToTrash_PreservesCompanyAndUnrelatedRecords()
        {
            string root = CreateUnusedPath();
            try
            {
                WriteData(root);

                PersistenceStorageResult<JobPostingTrashSummary> result =
                    JobPostingTrashService.MoveToTrash(root, TargetJobId);
                PersistenceStorageResult<JobCheckDataSet> remaining =
                    JobCheckDataRepository.Load(root);

                Assert.That(result.IsSuccess, Is.True, FormatIssues(result.Issues));
                Assert.That(remaining.IsSuccess, Is.True, FormatIssues(remaining.Issues));
                Assert.That(remaining.Value.Companies.Select(item => item.Id),
                    Does.Contain(CompanyId));
                Assert.That(remaining.Value.JobPostings.Select(item => item.Id),
                    Is.EquivalentTo(new[] { OtherJobId }));
                Assert.That(remaining.Value.Applications.Select(item => item.Id),
                    Is.EquivalentTo(new[] { OtherApplicationId }));
            }
            finally
            {
                DeleteTestDirectory(root);
            }
        }

        [Test]
        public void MoveToTrash_MissingJob_DoesNotMoveFiles()
        {
            string root = CreateUnusedPath();
            try
            {
                WriteData(root);

                PersistenceStorageResult<JobPostingTrashSummary> result =
                    JobPostingTrashService.MoveToTrash(
                        root,
                        "job_ffffffffffffffffffffffffffffffff");

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(Directory.Exists(Path.Combine(
                    root,
                    JobPostingTrashService.TrashDirectoryName)), Is.False);
                Assert.That(JobCheckDataRepository.Load(root).Value.JobPostings.Count,
                    Is.EqualTo(2));
            }
            finally
            {
                DeleteTestDirectory(root);
            }
        }

        [Test]
        public void ListAndRestore_ReturnsDeletedJobWithoutOverwritingOtherData()
        {
            string root = CreateUnusedPath();
            try
            {
                WriteData(root);
                PersistenceStorageResult<JobPostingTrashSummary> moved =
                    JobPostingTrashService.MoveToTrash(root, TargetJobId);

                PersistenceStorageResult<JobPostingTrashCatalog> catalog =
                    JobPostingTrashService.List(root);
                Assert.That(catalog.IsSuccess, Is.True, FormatIssues(catalog.Issues));
                Assert.That(catalog.Value.Entries.Count, Is.EqualTo(1));
                Assert.That(catalog.Value.Entries[0].JobPostingId, Is.EqualTo(TargetJobId));
                Assert.That(catalog.Value.Entries[0].ApplicationCount, Is.EqualTo(1));

                PersistenceStorageResult<JobPostingTrashSummary> restored =
                    JobPostingTrashService.Restore(root, moved.Value.TrashDirectory);
                Assert.That(restored.IsSuccess, Is.True, FormatIssues(restored.Issues));
                PersistenceStorageResult<JobCheckDataSet> data = JobCheckDataRepository.Load(root);
                Assert.That(data.Value.JobPostings.Select(item => item.Id),
                    Is.EquivalentTo(new[] { TargetJobId, OtherJobId }));
                Assert.That(data.Value.Applications.Select(item => item.Id),
                    Is.EquivalentTo(new[] { TargetApplicationId, OtherApplicationId }));
                Assert.That(Directory.Exists(moved.Value.TrashDirectory), Is.False);
            }
            finally { DeleteTestDirectory(root); }
        }

        [Test]
        public void Restore_WhenSameIdExists_RefusesToOverwriteActiveFile()
        {
            string root = CreateUnusedPath();
            try
            {
                WriteData(root);
                PersistenceStorageResult<JobPostingTrashSummary> moved =
                    JobPostingTrashService.MoveToTrash(root, TargetJobId);
                string activePath = Path.Combine(root,
                    JobCheckDataRepository.JobsDirectoryName, TargetJobId + ".json");
                File.WriteAllText(activePath, "keep-active-file");

                PersistenceStorageResult<JobPostingTrashSummary> restored =
                    JobPostingTrashService.Restore(root, moved.Value.TrashDirectory);
                Assert.That(restored.IsSuccess, Is.False);
                Assert.That(File.ReadAllText(activePath), Is.EqualTo("keep-active-file"));
                Assert.That(Directory.Exists(moved.Value.TrashDirectory), Is.True);
            }
            finally { DeleteTestDirectory(root); }
        }

        [Test]
        public void DeletePermanently_RemovesOnlySelectedTrashEntry()
        {
            string root = CreateUnusedPath();
            try
            {
                WriteData(root);
                PersistenceStorageResult<JobPostingTrashSummary> moved =
                    JobPostingTrashService.MoveToTrash(root, TargetJobId);

                PersistenceStorageResult<JobPostingTrashDeleteSummary> deleted =
                    JobPostingTrashService.DeletePermanently(root, moved.Value.TrashDirectory);
                Assert.That(deleted.IsSuccess, Is.True, FormatIssues(deleted.Issues));
                Assert.That(deleted.Value.DeletedCount, Is.EqualTo(1));
                Assert.That(Directory.Exists(moved.Value.TrashDirectory), Is.False);
                Assert.That(JobCheckDataRepository.Load(root).Value.JobPostings
                    .Select(item => item.Id), Is.EquivalentTo(new[] { OtherJobId }));
            }
            finally { DeleteTestDirectory(root); }
        }

        private static void WriteData(string root)
        {
            DateTimeOffset time = new DateTimeOffset(
                2026, 9, 14, 12, 0, 0, TimeSpan.FromHours(8));
            var company = new Company { Id = CompanyId, Name = "測試公司" };
            var targetJob = CreateJob(TargetJobId, "目標職缺", time);
            var otherJob = CreateJob(OtherJobId, "保留職缺", time);
            var targetApplication = CreateApplication(
                TargetApplicationId,
                TargetJobId,
                time);
            var otherApplication = CreateApplication(
                OtherApplicationId,
                OtherJobId,
                time);
            var targetEvent = CreateEvent(
                "evt_11111111111111111111111111111111",
                TargetApplicationId,
                time);
            var otherEvent = CreateEvent(
                "evt_22222222222222222222222222222222",
                OtherApplicationId,
                time);
            var dataSet = new JobCheckDataSet(
                new[] { company },
                new[] { targetJob, otherJob },
                new[] { targetApplication, otherApplication },
                new[] { targetEvent, otherEvent });

            PersistenceStorageResult<PersistenceWriteSummary> result =
                JobCheckDataRepository.WriteSnapshot(root, dataSet);
            Assert.That(result.IsSuccess, Is.True, FormatIssues(result.Issues));
        }

        private static JobPosting CreateJob(
            string id,
            string title,
            DateTimeOffset capturedAt)
        {
            return new JobPosting
            {
                Id = id,
                CompanyId = CompanyId,
                Title = title,
                Source = new JobSource { Platform = "test" },
                CapturedAt = capturedAt
            };
        }

        private static Application CreateApplication(
            string id,
            string jobPostingId,
            DateTimeOffset time)
        {
            return new Application
            {
                Id = id,
                JobPostingId = jobPostingId,
                SourceType = SourceType.MyApplication,
                CurrentStage = ApplicationStage.Saved,
                CreatedAt = time,
                UpdatedAt = time
            };
        }

        private static ApplicationEvent CreateEvent(
            string id,
            string applicationId,
            DateTimeOffset time)
        {
            return new ApplicationEvent
            {
                Id = id,
                ApplicationId = applicationId,
                EventType = ApplicationEventType.Saved,
                OccurredAt = time,
                RecordedAt = time,
                Actor = EventActor.Candidate
            };
        }

        private static string CreateUnusedPath()
        {
            return Path.Combine(
                Path.GetTempPath(),
                "JobCheckTrashTests_" + Guid.NewGuid().ToString("N"));
        }

        private static void DeleteTestDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private static string FormatIssues(IEnumerable<PersistenceStorageIssue> issues)
        {
            return string.Join(" | ", issues.Select(item =>
                item.Error + ":" + item.FilePath + ":" + item.Message));
        }
    }
}
