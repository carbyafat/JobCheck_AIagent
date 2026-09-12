using System;
using System.Collections.Generic;
using System.Linq;
using JobCheck.Domain;

namespace JobCheck.Persistence
{
    /// <summary>
    /// V0.2 職缺資料的建立入口。UI 只提供文字輸入，
    /// 此服務負責公司同名判定、ID、Domain 組裝、驗證與保存。
    /// </summary>
    public static class JobPostingCommandService
    {
        /// <summary>
        /// 建立一筆新職缺。正規化後同名的 Company 會被重用；
        /// 本操作不會自動建立 Application 或應徵事件。
        /// </summary>
        public static PersistenceStorageResult<JobPostingWriteSummary> Create(
            string dataRoot,
            JobPostingCreateRequest request)
        {
            if (request == null)
            {
                return Failure(null, "request", "新增職缺資料不可為 null。");
            }

            string companyName = TrimOrNull(request.CompanyName);
            string title = TrimOrNull(request.Title);
            string sourcePlatform = TrimOrNull(request.SourcePlatform);
            string sourceUrl = TrimOrNull(request.SourceUrl);
            string rawDescription = TrimOrNull(request.RawDescription);

            if (companyName == null)
            {
                return Failure(null, "company_name", "公司名稱為必填。");
            }

            if (title == null)
            {
                return Failure(null, "title", "職缺名稱為必填。");
            }

            if (sourcePlatform == null)
            {
                return Failure(null, "source.platform", "來源平台為必填。");
            }

            PersistenceStorageResult<JobCheckDataSet> load =
                JobCheckDataRepository.Load(dataRoot);
            if (!load.IsSuccess)
            {
                return Failure(load.Issues);
            }

            Company company = load.Value.Companies
                .Where(item => item != null
                    && CompanyNameNormalizer.AreEquivalent(item.Name, companyName))
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            bool companyCreated = company == null;
            DateTimeOffset capturedAt = request.CapturedAt ?? DateTimeOffset.Now;

            if (companyCreated)
            {
                company = new Company
                {
                    Id = CompanyIdGenerator.Create(),
                    SchemaVersion = Company.CurrentSchemaVersion,
                    Name = CompanyNameNormalizer.Normalize(companyName),
                    CreatedAt = capturedAt,
                    UpdatedAt = capturedAt
                };
            }

            var jobPosting = new JobPosting
            {
                Id = JobPostingIdGenerator.Create(),
                SchemaVersion = JobPosting.CurrentSchemaVersion,
                CompanyId = company.Id,
                Title = title,
                Source = new JobSource
                {
                    Platform = sourcePlatform,
                    Url = sourceUrl
                },
                CapturedAt = capturedAt,
                RawDescription = rawDescription
            };

            IReadOnlyList<JobPostingValidationError> errors =
                JobPostingValidator.ValidateNew(jobPosting);
            if (errors.Count > 0)
            {
                return Failure(
                    jobPosting.Id,
                    "job_posting",
                    "職缺資料驗證失敗：" + string.Join(", ", errors));
            }

            return JobCheckDataRepository.SaveNewJobPosting(
                dataRoot,
                companyCreated ? company : null,
                jobPosting);
        }

        /// <summary>
        /// 更新既有職缺的最小可編輯欄位，並保留原 JobPosting ID、收錄時間、
        /// 結構化內容以及所有既有 Application / ApplicationEvent 關聯。
        /// 公司名稱若改變，只會改連到同名公司或建立新公司，不會重新命名共用 Company。
        /// </summary>
        public static PersistenceStorageResult<JobPostingWriteSummary> Update(
            string dataRoot,
            JobPostingEditRequest request)
        {
            if (request == null)
            {
                return Failure(null, "request", "編輯職缺資料不可為 null。");
            }

            string jobPostingId = TrimOrNull(request.JobPostingId);
            string companyName = TrimOrNull(request.CompanyName);
            string title = TrimOrNull(request.Title);
            string sourcePlatform = TrimOrNull(request.SourcePlatform);
            string sourceUrl = TrimOrNull(request.SourceUrl);
            string rawDescription = TrimOrNull(request.RawDescription);

            if (jobPostingId == null)
            {
                return Failure(null, "job_posting_id", "職缺 ID 為必填。");
            }

            if (companyName == null)
            {
                return Failure(jobPostingId, "company_name", "公司名稱為必填。");
            }

            if (title == null)
            {
                return Failure(jobPostingId, "title", "職缺名稱為必填。");
            }

            if (sourcePlatform == null)
            {
                return Failure(jobPostingId, "source.platform", "來源平台為必填。");
            }

            PersistenceStorageResult<JobCheckDataSet> load =
                JobCheckDataRepository.Load(dataRoot);
            if (!load.IsSuccess)
            {
                return Failure(load.Issues);
            }

            JobPosting existing = load.Value.JobPostings.SingleOrDefault(item =>
                item != null
                && string.Equals(item.Id, jobPostingId, StringComparison.Ordinal));
            if (existing == null)
            {
                return Failure(jobPostingId, "job_posting_id", "找不到要編輯的職缺。");
            }

            Company company = load.Value.Companies
                .Where(item => item != null
                    && CompanyNameNormalizer.AreEquivalent(item.Name, companyName))
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            bool companyCreated = company == null;
            if (companyCreated)
            {
                DateTimeOffset createdAt = DateTimeOffset.Now;
                company = new Company
                {
                    Id = CompanyIdGenerator.Create(),
                    SchemaVersion = Company.CurrentSchemaVersion,
                    Name = CompanyNameNormalizer.Normalize(companyName),
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt
                };
            }

            existing.CompanyId = company.Id;
            existing.Title = title;
            existing.Source = new JobSource
            {
                Platform = sourcePlatform,
                Url = sourceUrl
            };
            existing.RawDescription = rawDescription;

            IReadOnlyList<JobPostingValidationError> errors =
                JobPostingValidator.ValidateNew(existing);
            if (errors.Count > 0)
            {
                return Failure(
                    existing.Id,
                    "job_posting",
                    "職缺資料驗證失敗：" + string.Join(", ", errors));
            }

            return JobCheckDataRepository.UpdateJobPosting(
                dataRoot,
                companyCreated ? company : null,
                existing);
        }

        private static string TrimOrNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static PersistenceStorageResult<JobPostingWriteSummary> Failure(
            IEnumerable<PersistenceStorageIssue> issues)
        {
            return new PersistenceStorageResult<JobPostingWriteSummary>(null, issues);
        }

        private static PersistenceStorageResult<JobPostingWriteSummary> Failure(
            string entityId,
            string fieldPath,
            string message)
        {
            return Failure(new[]
            {
                new PersistenceStorageIssue(
                    PersistenceStorageError.EntityValidationFailed,
                    entityId,
                    fieldPath,
                    message)
            });
        }
    }
}
