using System;
using System.IO;
using System.Linq;
using JobCheck.Domain;
using NUnit.Framework;

namespace JobCheck.Persistence.Tests
{
    public sealed class JobCheckPortableExportTests
    {
        [Test]
        public void Export_CompleteDataSet_ProducesSinglePortableFileWithoutChangingSource()
        {
            string root = NewTestRoot();
            try
            {
                Directory.CreateDirectory(root);
                string source = Path.Combine(root, "source");
                string destination = Path.Combine(root, "backup.jobcheck.json");
                Assert.That(JobCheckDataRepository.WriteSnapshot(source, CompleteDataSet()).IsSuccess, Is.True);
                string sourceApplication = Path.Combine(source, "applications",
                    "app_0123456789abcdef0123456789abcdef.json");
                string before = File.ReadAllText(sourceApplication);

                PersistenceStorageResult<JobCheckPortableExportSummary> result =
                    JobCheckPortableExportService.Export(source, destination);

                Assert.That(result.IsSuccess, Is.True, FormatIssues(result));
                Assert.That(File.Exists(destination), Is.True);
                Assert.That(result.Value.CompanyCount, Is.EqualTo(1));
                Assert.That(result.Value.JobCount, Is.EqualTo(1));
                Assert.That(result.Value.ApplicationCount, Is.EqualTo(1));
                Assert.That(PersistenceJsonSerializer.TryDeserialize(
                    File.ReadAllText(destination),
                    out JobCheckPortablePackageDto package,
                    out string error), Is.True, error);
                Assert.That(package.format_version, Is.EqualTo("0.2"));
                Assert.That(package.content_sha256, Is.EqualTo(package.CalculateContentSha256()));
                Assert.That(package.companies.Single().name, Is.EqualTo("測試公司"));
                Assert.That(package.jobs.Single().title, Is.EqualTo("Unity 工程師"));
                Assert.That(package.applications.Single().events.Single().id,
                    Is.EqualTo("evt_0123456789abcdef0123456789abcdef"));
                Assert.That(File.ReadAllText(sourceApplication), Is.EqualTo(before));
                Assert.That(Directory.GetFiles(root, "*.tmp-*", SearchOption.AllDirectories), Is.Empty);
            }
            finally
            {
                DeleteTestRoot(root);
            }
        }

        [Test]
        public void Export_ExistingFile_RefusesToOverwrite()
        {
            string root = NewTestRoot();
            try
            {
                Directory.CreateDirectory(root);
                string source = Path.Combine(root, "source");
                Assert.That(JobCheckDataRepository.WriteSnapshot(source, CompleteDataSet()).IsSuccess, Is.True);
                string destination = Path.Combine(root, "backup.jobcheck.json");
                File.WriteAllText(destination, "keep");

                PersistenceStorageResult<JobCheckPortableExportSummary> result =
                    JobCheckPortableExportService.Export(source, destination);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Issues.Select(item => item.Error),
                    Does.Contain(PersistenceStorageError.DestinationNotEmpty));
                Assert.That(File.ReadAllText(destination), Is.EqualTo("keep"));
            }
            finally
            {
                DeleteTestRoot(root);
            }
        }

        [Test]
        public void Export_InsideSourceDirectory_RefusesToPolluteDataSet()
        {
            string root = NewTestRoot();
            try
            {
                Assert.That(JobCheckDataRepository.WriteSnapshot(root, CompleteDataSet()).IsSuccess, Is.True);
                string destination = Path.Combine(root, "backup.jobcheck.json");

                PersistenceStorageResult<JobCheckPortableExportSummary> result =
                    JobCheckPortableExportService.Export(root, destination);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(File.Exists(destination), Is.False);
            }
            finally
            {
                DeleteTestRoot(root);
            }
        }

        [Test]
        public void Export_InvalidSource_DoesNotCreateOutput()
        {
            string root = NewTestRoot();
            try
            {
                Directory.CreateDirectory(root);
                string destination = Path.Combine(root, "backup.jobcheck.json");

                PersistenceStorageResult<JobCheckPortableExportSummary> result =
                    JobCheckPortableExportService.Export(Path.Combine(root, "missing"), destination);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(File.Exists(destination), Is.False);
            }
            finally
            {
                DeleteTestRoot(root);
            }
        }

        private static JobCheckDataSet CompleteDataSet()
        {
            var time = new DateTimeOffset(2026, 9, 19, 10, 0, 0, TimeSpan.FromHours(8));
            var company = new Company
            {
                Id = "cmp_0123456789abcdef0123456789abcdef",
                Name = "測試公司"
            };
            var job = new JobPosting
            {
                Id = "demo_job_001",
                CompanyId = company.Id,
                Title = "Unity 工程師",
                Source = new JobSource { Platform = "demo", Url = "https://example.com/job" },
                CapturedAt = time
            };
            var application = new Application
            {
                Id = "app_0123456789abcdef0123456789abcdef",
                JobPostingId = job.Id,
                SourceType = SourceType.MyApplication,
                CurrentStage = ApplicationStage.WaitingResponse,
                CreatedAt = time,
                UpdatedAt = time
            };
            var applicationEvent = new ApplicationEvent
            {
                Id = "evt_0123456789abcdef0123456789abcdef",
                ApplicationId = application.Id,
                EventType = ApplicationEventType.MigrationSnapshot,
                OccurredAt = time,
                RecordedAt = time,
                Actor = EventActor.System,
                SnapshotStage = ApplicationStage.WaitingResponse,
                LegacyStatus = "waiting_reply"
            };
            return new JobCheckDataSet(
                new[] { company }, new[] { job },
                new[] { application }, new[] { applicationEvent });
        }

        private static string NewTestRoot() => Path.Combine(
            Path.GetTempPath(), "JobCheckTransferExportTests_" + Guid.NewGuid().ToString("N"));

        private static void DeleteTestRoot(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }

        private static string FormatIssues(PersistenceStorageResult<JobCheckPortableExportSummary> result)
        {
            return string.Join("; ", result.Issues.Select(item => item.Message));
        }
    }
}
