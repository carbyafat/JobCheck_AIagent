using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JobCheck.Domain;
using NUnit.Framework;

namespace JobCheck.Persistence.Tests
{
    /// <summary>
    /// 驗證 V0.2 唯讀查詢會正確連結公司、職缺與本人最近一次應徵。
    /// </summary>
    public sealed class JobPostingReadOnlyQueryTests
    {
        [Test]
        public void Load_JoinsCompanyAndApplicationForDisplay()
        {
            string root = CreateUnusedPath();
            try
            {
                JobCheckDataSet dataSet = CreateDataSet();
                PersistenceStorageResult<PersistenceWriteSummary> write =
                    JobCheckDataRepository.WriteSnapshot(root, dataSet);

                PersistenceStorageResult<JobPostingReadOnlyList> result =
                    JobPostingReadOnlyQuery.Load(root);

                Assert.That(write.IsSuccess, Is.True, FormatIssues(write.Issues));
                Assert.That(result.IsSuccess, Is.True, FormatIssues(result.Issues));
                Assert.That(result.Value.Items, Has.Count.EqualTo(1));
                Assert.That(result.Value.Items[0].Company.Name, Is.EqualTo("測試公司"));
                Assert.That(result.Value.Items[0].JobPosting.Title, Is.EqualTo("Unity 工程師"));
                Assert.That(result.Value.Items[0].CurrentApplication.Id,
                    Is.EqualTo("app_0123456789abcdef0123456789abcdef"));
                Assert.That(result.Value.Items[0].StatusCode, Is.EqualTo("waiting_reply"));
            }
            finally
            {
                DeleteTestDirectory(root);
            }
        }

        [Test]
        public void Load_JobWithoutApplication_DisplaysAsNotViewed()
        {
            string root = CreateUnusedPath();
            try
            {
                JobCheckDataSet source = CreateDataSet();
                var withoutApplication = new JobCheckDataSet(
                    source.Companies,
                    source.JobPostings,
                    Array.Empty<Application>(),
                    Array.Empty<ApplicationEvent>());
                JobCheckDataRepository.WriteSnapshot(root, withoutApplication);

                PersistenceStorageResult<JobPostingReadOnlyList> result =
                    JobPostingReadOnlyQuery.Load(root);

                Assert.That(result.IsSuccess, Is.True, FormatIssues(result.Issues));
                Assert.That(result.Value.Items[0].CurrentApplication, Is.Null);
                Assert.That(result.Value.Items[0].StatusCode, Is.EqualTo("not_viewed"));
            }
            finally
            {
                DeleteTestDirectory(root);
            }
        }

        [Test]
        public void Load_MissingRoot_ForwardsRepositoryErrorWithoutCreatingDirectory()
        {
            string root = CreateUnusedPath();

            PersistenceStorageResult<JobPostingReadOnlyList> result =
                JobPostingReadOnlyQuery.Load(root);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Issues.Select(item => item.Error),
                Does.Contain(PersistenceStorageError.DataRootNotFound));
            Assert.That(Directory.Exists(root), Is.False);
        }

        private static JobCheckDataSet CreateDataSet()
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
                Source = new JobSource
                {
                    Platform = "demo",
                    Url = "https://example.com/jobs/demo_job_001"
                },
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
                "JobCheckReadOnlyQueryTests_" + Guid.NewGuid().ToString("N"));
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
