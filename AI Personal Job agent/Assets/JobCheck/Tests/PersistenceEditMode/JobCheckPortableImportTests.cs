using System;
using System.IO;
using System.Linq;
using JobCheck.Domain;
using NUnit.Framework;

namespace JobCheck.Persistence.Tests
{
    public sealed class JobCheckPortableImportTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void Import_EmptyTarget_PublishesCompleteSnapshot(bool targetAlreadyExists)
        {
            string root = NewRoot();
            try
            {
                string packagePath = CreatePackage(root);
                string target = Path.Combine(root, "personal");
                Assert.That(JobCheckPortableImportService.CanImportIntoEmptyRoot(target), Is.True);
                if (targetAlreadyExists)
                    Assert.That(JobCheckDataRepository.WriteSnapshot(target,
                        new JobCheck.Domain.JobCheckDataSet(null, null, null, null)).IsSuccess,
                        Is.True);
                string originalPackage = File.ReadAllText(packagePath);

                PersistenceStorageResult<JobCheckPortableImportSummary> result =
                    JobCheckPortableImportService.Import(packagePath, target);

                Assert.That(result.IsSuccess, Is.True, FormatIssues(result));
                Assert.That(result.Value.CleanupWarning, Is.Null);
                Assert.That(result.Value.EventCount, Is.EqualTo(1));
                PersistenceStorageResult<JobCheck.Domain.JobCheckDataSet> loaded =
                    JobCheckDataRepository.Load(target);
                Assert.That(loaded.IsSuccess, Is.True);
                Assert.That(loaded.Value.Companies.Count, Is.EqualTo(1));
                Assert.That(loaded.Value.JobPostings.Count, Is.EqualTo(1));
                Assert.That(loaded.Value.Applications.Count, Is.EqualTo(1));
                Assert.That(loaded.Value.ApplicationEvents.Count, Is.EqualTo(1));
                Assert.That(File.ReadAllText(packagePath), Is.EqualTo(originalPackage));
                Assert.That(Directory.GetDirectories(root, "personal.*"), Is.Empty);
            }
            finally { Delete(root); }
        }

        [Test]
        public void Import_NonemptyTarget_RefusesWithoutChangingIt()
        {
            string root = NewRoot();
            try
            {
                string packagePath = CreatePackage(root);
                string target = Path.Combine(root, "personal");
                Assert.That(JobCheckDataRepository.WriteSnapshot(target,
                    JobCheckPortableExportTests.CompleteDataSet()).IsSuccess, Is.True);
                string existingFile = Path.Combine(target, "companies",
                    "cmp_0123456789abcdef0123456789abcdef.json");
                string before = File.ReadAllText(existingFile);
                Assert.That(JobCheckPortableImportService.CanImportIntoEmptyRoot(target), Is.False);

                PersistenceStorageResult<JobCheckPortableImportSummary> result =
                    JobCheckPortableImportService.Import(packagePath, target);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Issues.Any(issue => issue.Error ==
                    PersistenceStorageError.DestinationNotEmpty), Is.True);
                Assert.That(File.ReadAllText(existingFile), Is.EqualTo(before));
                Assert.That(Directory.GetDirectories(root, "personal.*"), Is.Empty);
            }
            finally { Delete(root); }
        }

        [Test]
        public void Import_InvalidPackage_DoesNotCreateTarget()
        {
            string root = NewRoot();
            try
            {
                string packagePath = CreatePackage(root);
                Assert.That(PersistenceJsonSerializer.TryDeserialize(File.ReadAllText(packagePath),
                    out JobCheckPortablePackageDto package, out string error), Is.True, error);
                package.content_sha256 = "corrupt";
                File.WriteAllText(packagePath, PersistenceJsonSerializer.Serialize(package));
                string target = Path.Combine(root, "personal");

                PersistenceStorageResult<JobCheckPortableImportSummary> result =
                    JobCheckPortableImportService.Import(packagePath, target);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(Directory.Exists(target), Is.False);
            }
            finally { Delete(root); }
        }

        [Test]
        public void Sync_ExistingSubset_AddsOnlyNewRecords_AndIsIdempotent()
        {
            string root = NewRoot();
            try
            {
                Directory.CreateDirectory(root);
                JobCheckDataSet baseline = JobCheckPortableExportTests.CompleteDataSet();
                string target = Path.Combine(root, "personal");
                string source = Path.Combine(root, "source");
                string package = Path.Combine(root, "transfer.jobcheck.json");
                Assert.That(JobCheckDataRepository.WriteSnapshot(target, baseline).IsSuccess, Is.True);
                string originalCompany = File.ReadAllText(Path.Combine(target, "companies",
                    baseline.Companies[0].Id + ".json"));
                string trash = Path.Combine(target, "trash");
                Directory.CreateDirectory(trash);
                File.WriteAllText(Path.Combine(trash, "keep.txt"), "keep");

                var addedJob = new JobPosting {
                    Id = "job_11111111111111111111111111111111",
                    CompanyId = baseline.Companies[0].Id,
                    Title = "新職缺",
                    Source = new JobSource { Platform = "demo", Url = "https://example.com/new" },
                    CapturedAt = baseline.JobPostings[0].CapturedAt
                };
                var addedApplication = new JobCheck.Domain.Application {
                    Id = "app_11111111111111111111111111111111",
                    JobPostingId = addedJob.Id,
                    SourceType = SourceType.MyApplication,
                    CurrentStage = ApplicationStage.WaitingResponse,
                    CreatedAt = baseline.Applications[0].CreatedAt,
                    UpdatedAt = baseline.Applications[0].UpdatedAt
                };
                var addedEvent = new ApplicationEvent {
                    Id = "evt_11111111111111111111111111111111",
                    ApplicationId = addedApplication.Id,
                    EventType = ApplicationEventType.MigrationSnapshot,
                    OccurredAt = baseline.ApplicationEvents[0].OccurredAt,
                    RecordedAt = baseline.ApplicationEvents[0].RecordedAt,
                    Actor = EventActor.System,
                    SnapshotStage = ApplicationStage.WaitingResponse,
                    LegacyStatus = "waiting_reply"
                };
                var extended = new JobCheckDataSet(baseline.Companies,
                    baseline.JobPostings.Concat(new[] { addedJob }),
                    baseline.Applications.Concat(new[] { addedApplication }),
                    baseline.ApplicationEvents.Concat(new[] { addedEvent }));
                Assert.That(JobCheckDataRepository.WriteSnapshot(source, extended).IsSuccess, Is.True);
                Assert.That(JobCheckPortableExportService.Export(source, package).IsSuccess, Is.True);

                PersistenceStorageResult<JobCheckPortableSyncPreview> preview =
                    JobCheckPortableImportService.PreviewSync(package, target);
                Assert.That(preview.IsSuccess, Is.True, FormatSyncIssues(preview));
                Assert.That(preview.Value.NewCompanyCount, Is.Zero);
                Assert.That(preview.Value.NewJobCount, Is.EqualTo(1));
                Assert.That(preview.Value.NewApplicationCount, Is.EqualTo(1));
                Assert.That(preview.Value.NewEventCount, Is.EqualTo(1));
                Assert.That(JobCheckPortableImportService.Sync(package, target).IsSuccess, Is.True);
                Assert.That(JobCheckDataRepository.Load(target).Value.JobPostings.Count, Is.EqualTo(2));
                Assert.That(JobCheckDataRepository.Load(target).Value.Applications.Count, Is.EqualTo(2));
                Assert.That(File.ReadAllText(Path.Combine(target, "companies",
                    baseline.Companies[0].Id + ".json")), Is.EqualTo(originalCompany));
                Assert.That(File.ReadAllText(Path.Combine(trash, "keep.txt")), Is.EqualTo("keep"));

                PersistenceStorageResult<JobCheckPortableSyncPreview> repeated =
                    JobCheckPortableImportService.PreviewSync(package, target);
                Assert.That(repeated.IsSuccess, Is.True, FormatSyncIssues(repeated));
                Assert.That(repeated.Value.NewJobCount + repeated.Value.NewApplicationCount, Is.Zero);
                Assert.That(JobCheckPortableImportService.Sync(package, target).IsSuccess, Is.True);
                Assert.That(Directory.GetDirectories(root, "personal.sync-staging-*"), Is.Empty);
            }
            finally { Delete(root); }
        }

        [Test]
        public void Sync_SameIdDifferentContent_RefusesAllAdditions()
        {
            string root = NewRoot();
            try
            {
                Directory.CreateDirectory(root);
                JobCheckDataSet baseline = JobCheckPortableExportTests.CompleteDataSet();
                string target = Path.Combine(root, "personal");
                string source = Path.Combine(root, "source");
                string package = Path.Combine(root, "transfer.jobcheck.json");
                Assert.That(JobCheckDataRepository.WriteSnapshot(target, baseline).IsSuccess, Is.True);
                baseline.Companies[0].Name = "遠端修改名稱";
                Assert.That(JobCheckDataRepository.WriteSnapshot(source, baseline).IsSuccess, Is.True);
                Assert.That(JobCheckPortableExportService.Export(source, package).IsSuccess, Is.True);

                PersistenceStorageResult<JobCheckPortableSyncPreview> result =
                    JobCheckPortableImportService.Sync(package, target);
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Issues.Any(item => item.Message.Contains("內容不同")), Is.True);
                Assert.That(JobCheckDataRepository.Load(target).Value.Companies[0].Name,
                    Is.EqualTo("測試公司"));
            }
            finally { Delete(root); }
        }

        private static string CreatePackage(string root)
        {
            Directory.CreateDirectory(root);
            string source = Path.Combine(root, "source");
            string packagePath = Path.Combine(root, "transfer.jobcheck.json");
            Assert.That(JobCheckDataRepository.WriteSnapshot(source,
                JobCheckPortableExportTests.CompleteDataSet()).IsSuccess, Is.True);
            Assert.That(JobCheckPortableExportService.Export(source, packagePath).IsSuccess, Is.True);
            return packagePath;
        }

        private static string NewRoot() => Path.Combine(
            Path.GetTempPath(), "JobCheckTransferImportTests_" + Guid.NewGuid().ToString("N"));
        private static void Delete(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        private static string FormatIssues(PersistenceStorageResult<JobCheckPortableImportSummary> result)
            => string.Join("; ", result.Issues.Select(issue => issue.Message));
        private static string FormatSyncIssues(PersistenceStorageResult<JobCheckPortableSyncPreview> result)
            => string.Join("; ", result.Issues.Select(issue => issue.Message));
    }
}
