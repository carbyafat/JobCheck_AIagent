using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JobCheck.Domain;
using NUnit.Framework;

namespace JobCheck.Persistence.Tests
{
    /// <summary>
    /// 驗證 V0.2 Repository 的無副作用讀取、完整快照寫入與錯誤攔截。
    /// </summary>
    public sealed class PersistenceRepositoryTests
    {
        [Test]
        public void JsonSerializer_CompanyDto_RoundTripsSnakeCaseFields()
        {
            var source = new CompanyDto
            {
                id = "cmp_0123456789abcdef0123456789abcdef",
                schema_version = "0.2",
                name = "測試公司"
            };

            string json = PersistenceJsonSerializer.Serialize(source);
            bool success = PersistenceJsonSerializer.TryDeserialize(
                json,
                out CompanyDto restored,
                out string error);

            Assert.That(success, Is.True, error);
            Assert.That(json, Does.Contain("\"schema_version\""));
            Assert.That(restored.id, Is.EqualTo(source.id));
            Assert.That(restored.name, Is.EqualTo(source.name));
        }

        [Test]
        public void Load_MissingRoot_ReturnsNotFoundWithoutCreatingDirectory()
        {
            string root = CreateUnusedPath();

            PersistenceStorageResult<JobCheckDataSet> result =
                JobCheckDataRepository.Load(root);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Issues.Select(item => item.Error),
                Does.Contain(PersistenceStorageError.DataRootNotFound));
            Assert.That(Directory.Exists(root), Is.False);
        }

        [Test]
        public void WriteThenLoad_CompleteDataSet_RoundTripsAllEntities()
        {
            string root = CreateUnusedPath();
            try
            {
                JobCheckDataSet source = CreateCompleteDataSet();

                PersistenceStorageResult<PersistenceWriteSummary> write =
                    JobCheckDataRepository.WriteSnapshot(root, source);
                PersistenceStorageResult<JobCheckDataSet> load =
                    JobCheckDataRepository.Load(root);

                Assert.That(write.IsSuccess, Is.True, FormatIssues(write.Issues));
                Assert.That(load.IsSuccess, Is.True, FormatIssues(load.Issues));
                Assert.That(load.Value.Companies.Count, Is.EqualTo(1));
                Assert.That(load.Value.JobPostings.Count, Is.EqualTo(1));
                Assert.That(load.Value.Applications.Count, Is.EqualTo(1));
                Assert.That(load.Value.ApplicationEvents.Count, Is.EqualTo(1));
                Assert.That(load.Value.Applications[0].CurrentStage,
                    Is.EqualTo(ApplicationStage.WaitingResponse));
            }
            finally
            {
                DeleteTestDirectory(root);
            }
        }

        [Test]
        public void WriteSnapshot_EmbedsEventsInsideApplicationFile()
        {
            string root = CreateUnusedPath();
            try
            {
                JobCheckDataSet source = CreateCompleteDataSet();

                PersistenceStorageResult<PersistenceWriteSummary> result =
                    JobCheckDataRepository.WriteSnapshot(root, source);
                string applicationFile = Path.Combine(
                    root,
                    JobCheckDataRepository.ApplicationsDirectoryName,
                    source.Applications[0].Id + ".json");
                string json = File.ReadAllText(applicationFile);

                Assert.That(result.IsSuccess, Is.True, FormatIssues(result.Issues));
                Assert.That(json, Does.Contain("\"events\""));
                Assert.That(json, Does.Contain(source.ApplicationEvents[0].Id));
                Assert.That(Directory.GetFiles(root, "*.tmp-*", SearchOption.AllDirectories),
                    Is.Empty);
            }
            finally
            {
                DeleteTestDirectory(root);
            }
        }

        [Test]
        public void WriteSnapshot_InvalidDataSet_DoesNotCreateDestination()
        {
            string root = CreateUnusedPath();
            JobCheckDataSet invalid = CreateCompleteDataSet();
            invalid.Companies[0].Name = "";

            PersistenceStorageResult<PersistenceWriteSummary> result =
                JobCheckDataRepository.WriteSnapshot(root, invalid);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Issues.Select(item => item.Error),
                Does.Contain(PersistenceStorageError.EntityValidationFailed));
            Assert.That(Directory.Exists(root), Is.False);
        }

        [Test]
        public void WriteSnapshot_NonEmptyDestination_RefusesToOverwrite()
        {
            string root = CreateUnusedPath();
            try
            {
                Directory.CreateDirectory(root);
                string sentinel = Path.Combine(root, "keep.txt");
                File.WriteAllText(sentinel, "不可覆蓋");

                PersistenceStorageResult<PersistenceWriteSummary> result =
                    JobCheckDataRepository.WriteSnapshot(root, CreateCompleteDataSet());

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Issues.Select(item => item.Error),
                    Does.Contain(PersistenceStorageError.DestinationNotEmpty));
                Assert.That(File.ReadAllText(sentinel), Is.EqualTo("不可覆蓋"));
            }
            finally
            {
                DeleteTestDirectory(root);
            }
        }

        [Test]
        public void Load_InvalidJson_ReturnsFileAndError()
        {
            string root = CreateUnusedPath();
            try
            {
                string directory = Path.Combine(root, JobCheckDataRepository.CompaniesDirectoryName);
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "broken.json");
                File.WriteAllText(path, "{ not-json }");

                PersistenceStorageResult<JobCheckDataSet> result =
                    JobCheckDataRepository.Load(root);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Issues.Any(item =>
                    item.Error == PersistenceStorageError.InvalidJson
                    && item.FilePath == path), Is.True);
            }
            finally
            {
                DeleteTestDirectory(root);
            }
        }

        [Test]
        public void Load_UnknownSchemaVersion_IsRejected()
        {
            string root = CreateUnusedPath();
            try
            {
                string directory = Path.Combine(root, JobCheckDataRepository.CompaniesDirectoryName);
                Directory.CreateDirectory(directory);
                string id = "cmp_0123456789abcdef0123456789abcdef";
                var dto = new CompanyDto { id = id, schema_version = "9.9", name = "測試" };
                File.WriteAllText(
                    Path.Combine(directory, id + ".json"),
                    PersistenceJsonSerializer.Serialize(dto));

                PersistenceStorageResult<JobCheckDataSet> result =
                    JobCheckDataRepository.Load(root);

                Assert.That(result.Issues.Select(item => item.Error),
                    Does.Contain(PersistenceStorageError.UnsupportedSchemaVersion));
            }
            finally
            {
                DeleteTestDirectory(root);
            }
        }

        [Test]
        public void Load_FileNameDoesNotMatchEntityId_IsRejected()
        {
            string root = CreateUnusedPath();
            try
            {
                string directory = Path.Combine(root, JobCheckDataRepository.CompaniesDirectoryName);
                Directory.CreateDirectory(directory);
                var dto = new CompanyDto
                {
                    id = "cmp_0123456789abcdef0123456789abcdef",
                    schema_version = "0.2",
                    name = "測試"
                };
                File.WriteAllText(
                    Path.Combine(directory, "wrong-name.json"),
                    PersistenceJsonSerializer.Serialize(dto));

                PersistenceStorageResult<JobCheckDataSet> result =
                    JobCheckDataRepository.Load(root);

                Assert.That(result.Issues.Select(item => item.Error),
                    Does.Contain(PersistenceStorageError.FileNameIdMismatch));
            }
            finally
            {
                DeleteTestDirectory(root);
            }
        }

        private static JobCheckDataSet CreateCompleteDataSet()
        {
            var time = new DateTimeOffset(2026, 6, 20, 16, 39, 20, TimeSpan.FromHours(8));
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
                new[] { company },
                new[] { job },
                new[] { application },
                new[] { applicationEvent });
        }

        private static string CreateUnusedPath()
        {
            return Path.Combine(
                Path.GetTempPath(),
                "JobCheckPersistenceTests_" + Guid.NewGuid().ToString("N"));
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
