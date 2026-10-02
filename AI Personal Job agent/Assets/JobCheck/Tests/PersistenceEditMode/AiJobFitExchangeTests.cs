using System;
using System.Collections.Generic;
using System.IO;
using JobCheck.Domain;
using NUnit.Framework;

namespace JobCheck.Persistence.Tests
{
    public sealed class AiJobFitExchangeTests
    {
        private string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "JobCheckAiExchangeTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [Test]
        public void RequestFactory_UsesExplicitScopeAndRemovesInternalIds()
        {
            var company = new Company
            {
                Id = "company_private_id",
                Name = "測試公司",
                Industry = "資訊軟體業",
                RiskFlags = new List<string> { "面試時確認加班制度" }
            };
            var job = new JobPosting
            {
                Id = "job_test",
                CompanyId = company.Id,
                Title = "Unity 工程師",
                Source = new JobSource { Platform = "test", Url = "https://example.com/job" }
            };
            var profile = new CareerProfile
            {
                Id = "profile_private_id",
                Summary = "測試履歷",
                Skills = new List<CareerSkill>
                {
                    new CareerSkill { Id = "skill_private_id", Name = "Unity" }
                }
            };

            AiJobFitRequestCreationResult result = AiJobFitRequestFactory.Create(
                company,
                job,
                profile,
                null,
                null,
                null,
                null,
                new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero),
                "32f9d5e924554ec8bc2245dc8d80291f");

            Assert.That(result.IsSuccess, Is.True, string.Join("\n", result.Issues));
            Assert.That(result.Package.task_type,
                Is.EqualTo(AiExchangeContract.JobFitAnalysisTaskType));
            Assert.That(result.Package.payload.job.id, Is.Null);
            Assert.That(result.Package.payload.job.company_id, Is.Null);
            Assert.That(result.Package.payload.resume.id, Is.Null);
            Assert.That(result.Package.payload.resume.skills[0].id, Is.Null);
            Assert.That(result.Package.payload.excluded_data,
                Does.Contain("應徵事件歷程"));
            Assert.That(result.Package.payload.instructions.rules,
                Has.Some.Contains("不得建立另一個 0 到 100 的 AI 分數"));
        }

        [Test]
        public void Validator_AcceptsFencedValidResultAndRejectsOtherJob()
        {
            AiJobFitResultPackageDto package = Result("job_test");
            string fenced = "```json\n" + PersistenceJsonSerializer.Serialize(package) + "\n```";

            AiJobFitResultValidationResult valid = AiJobFitResultValidator.ParseAndValidate(
                fenced,
                package.request_id,
                "job_test");
            AiJobFitResultValidationResult wrongJob = AiJobFitResultValidator.ParseAndValidate(
                PersistenceJsonSerializer.Serialize(package),
                package.request_id,
                "another_job");

            Assert.That(valid.IsSuccess, Is.True, string.Join("\n", valid.Issues));
            Assert.That(valid.Value.result.recommendation, Is.EqualTo("consider"));
            Assert.That(wrongJob.IsSuccess, Is.False);
            Assert.That(wrongJob.Issues, Has.Some.Contains("其他職缺"));
        }

        [Test]
        public void Repository_SavesResultSeparatelyAndNeverOverwrites()
        {
            AiJobFitResultPackageDto result = Result("job_test");
            var request = new AiJobFitRequestPackageDto
            {
                format = AiExchangeContract.Format,
                schema_version = AiExchangeContract.SchemaVersion,
                message_type = AiExchangeContract.RequestMessageType,
                task_type = AiExchangeContract.JobFitAnalysisTaskType,
                request_id = result.request_id,
                job_id = result.job_id,
                created_at = DateTimeOffset.UtcNow.ToString("o"),
                payload = new AiJobFitRequestPayloadDto()
            };

            PersistenceStorageResult<AiJobFitRequestPackageDto> registered =
                AiJobFitAnalysisRepository.RegisterRequest(root, request);
            PersistenceStorageResult<AiJobFitAnalysisSaveSummary> first =
                AiJobFitAnalysisRepository.SaveResult(root, result);
            PersistenceStorageResult<AiJobFitAnalysisSaveSummary> second =
                AiJobFitAnalysisRepository.SaveResult(root, result);

            Assert.That(registered.IsSuccess, Is.True);
            Assert.That(first.IsSuccess, Is.True);
            Assert.That(File.Exists(first.Value.Path), Is.True);
            Assert.That(first.Value.Path, Does.Contain(Path.Combine("ai_analysis", "job_test")));
            Assert.That(second.IsSuccess, Is.False);
            Assert.That(second.Issues, Has.Some.Matches<PersistenceStorageIssue>(
                issue => issue.Message.Contains("不會覆蓋")));
        }

        private static AiJobFitResultPackageDto Result(string jobId)
        {
            return new AiJobFitResultPackageDto
            {
                format = AiExchangeContract.Format,
                schema_version = AiExchangeContract.SchemaVersion,
                message_type = AiExchangeContract.ResultMessageType,
                task_type = AiExchangeContract.JobFitAnalysisTaskType,
                request_id = "32f9d5e924554ec8bc2245dc8d80291f",
                job_id = jobId,
                status = "completed",
                producer = new AiResultProducerDto { provider = "test", model = "test" },
                result = new AiJobFitAnalysisDto
                {
                    recommendation = "consider",
                    confidence = "medium",
                    summary = "資料不足，建議面試時確認。",
                    strengths = new List<AiInsightDto>
                    {
                        new AiInsightDto
                        {
                            title = "技能相符",
                            detail = "履歷列有 Unity。",
                            evidence_refs = new List<string> { "payload.resume.skills[0]" }
                        }
                    }
                }
            };
        }
    }
}
