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
                Department = TrimOrNull(request.Department),
                Category = TrimOrNull(request.Category),
                Compensation = BuildCompensation(request),
                Location = BuildLocation(request),
                WorkConditions = BuildWorkConditions(request),
                Responsibilities = NormalizeTextEntries(request.Responsibilities),
                Requirements = BuildRequirements(request),
                RawDescription = rawDescription,
                Tags = NormalizeLabels(request.Tags),
                RiskFlags = NormalizeLabels(request.RiskFlags)
            };

            IReadOnlyList<JobPostingValidationError> errors =
                JobPostingValidator.ValidateNew(jobPosting);
            if (errors.Count > 0)
            {
                return Failure(
                    jobPosting.Id,
                    "job_posting",
                    "職缺資料驗證失敗：" + string.Join(
                        "、",
                        errors.Select(ValidationErrorLocalizer.ToChinese)));
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
            if (request.CapturedAt.HasValue)
            {
                existing.CapturedAt = request.CapturedAt;
            }

            existing.Department = TrimOrNull(request.Department);
            existing.Category = TrimOrNull(request.Category);
            existing.Compensation = UpdateCompensation(existing.Compensation, request);
            existing.Location = UpdateLocation(existing.Location, request);
            existing.WorkConditions = UpdateWorkConditions(existing.WorkConditions, request);
            existing.Responsibilities = NormalizeTextEntries(request.Responsibilities);
            existing.Requirements = UpdateRequirements(existing.Requirements, request);
            existing.RawDescription = rawDescription;
            existing.Tags = NormalizeLabels(request.Tags);
            existing.RiskFlags = NormalizeLabels(request.RiskFlags);

            IReadOnlyList<JobPostingValidationError> errors =
                JobPostingValidator.ValidateNew(existing);
            if (errors.Count > 0)
            {
                return Failure(
                    existing.Id,
                    "job_posting",
                    "職缺資料驗證失敗：" + string.Join(
                        "、",
                        errors.Select(ValidationErrorLocalizer.ToChinese)));
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

        private static JobCompensation BuildCompensation(JobPostingCreateRequest request)
        {
            var value = new JobCompensation
            {
                Type = TrimOrNull(request.CompensationType),
                Period = TrimOrNull(request.CompensationPeriod),
                Minimum = request.CompensationMinimum,
                Maximum = request.CompensationMaximum,
                Currency = TrimOrNull(request.CompensationCurrency),
                RawText = TrimOrNull(request.CompensationRawText)
            };
            return IsEmpty(value) ? null : value;
        }

        private static JobCompensation UpdateCompensation(
            JobCompensation existing,
            JobPostingEditRequest request)
        {
            var value = existing ?? new JobCompensation();
            value.Type = TrimOrNull(request.CompensationType);
            value.Period = TrimOrNull(request.CompensationPeriod);
            value.Minimum = request.CompensationMinimum;
            value.Maximum = request.CompensationMaximum;
            value.Currency = TrimOrNull(request.CompensationCurrency);
            value.RawText = TrimOrNull(request.CompensationRawText);
            return IsEmpty(value) ? null : value;
        }

        private static bool IsEmpty(JobCompensation value)
        {
            return value == null
                || (value.Type == null
                    && value.Period == null
                    && !value.Minimum.HasValue
                    && !value.Maximum.HasValue
                    && value.Currency == null
                    && value.RawText == null
                    && value.Notes == null);
        }

        private static JobLocation BuildLocation(JobPostingCreateRequest request)
        {
            var value = new JobLocation
            {
                WorkMode = TrimOrNull(request.WorkMode),
                RawText = TrimOrNull(request.LocationRawText)
            };
            return IsEmpty(value) ? null : value;
        }

        private static JobLocation UpdateLocation(
            JobLocation existing,
            JobPostingEditRequest request)
        {
            var value = existing ?? new JobLocation();
            value.WorkMode = TrimOrNull(request.WorkMode);
            value.RawText = TrimOrNull(request.LocationRawText);
            return IsEmpty(value) ? null : value;
        }

        private static bool IsEmpty(JobLocation value)
        {
            return value == null
                || (value.WorkMode == null
                    && value.City == null
                    && value.District == null
                    && value.Address == null
                    && !value.RemoteAllowed.HasValue
                    && value.RawText == null);
        }

        private static JobWorkConditions BuildWorkConditions(JobPostingCreateRequest request)
        {
            var value = new JobWorkConditions
            {
                EmploymentType = TrimOrNull(request.EmploymentType),
                WorkingHours = TrimOrNull(request.WorkingHours)
            };
            return IsEmpty(value) ? null : value;
        }

        private static JobWorkConditions UpdateWorkConditions(
            JobWorkConditions existing,
            JobPostingEditRequest request)
        {
            var value = existing ?? new JobWorkConditions();
            value.EmploymentType = TrimOrNull(request.EmploymentType);
            value.WorkingHours = TrimOrNull(request.WorkingHours);
            return IsEmpty(value) ? null : value;
        }

        private static bool IsEmpty(JobWorkConditions value)
        {
            return value == null
                || (value.EmploymentType == null
                    && value.WorkingHours == null
                    && value.BusinessTrip == null
                    && value.ManagementResponsibility == null
                    && value.LeavePolicy == null
                    && value.StartDate == null);
        }

        private static JobRequirements BuildRequirements(JobPostingCreateRequest request)
        {
            var value = new JobRequirements
            {
                Experience = TrimOrNull(request.Experience),
                Education = TrimOrNull(request.Education),
                Tools = NormalizeTextEntries(request.Tools),
                Skills = NormalizeTextEntries(request.Skills)
            };
            ApplyMeasurableRequirements(value);
            return IsEmpty(value) ? null : value;
        }

        private static JobRequirements UpdateRequirements(
            JobRequirements existing,
            JobPostingEditRequest request)
        {
            var value = existing ?? new JobRequirements();
            string previousExperience = value.Experience;
            string previousEducation = value.Education;
            value.Experience = TrimOrNull(request.Experience);
            value.Education = TrimOrNull(request.Education);
            value.Tools = NormalizeTextEntries(request.Tools);
            value.Skills = NormalizeTextEntries(request.Skills);
            if (!string.Equals(previousExperience, value.Experience, StringComparison.Ordinal))
                ApplyExperienceRequirement(value);
            if (!string.Equals(previousEducation, value.Education, StringComparison.Ordinal))
                ApplyEducationRequirement(value);
            return IsEmpty(value) ? null : value;
        }

        private static void ApplyMeasurableRequirements(JobRequirements value)
        {
            ApplyExperienceRequirement(value);
            ApplyEducationRequirement(value);
        }

        private static void ApplyExperienceRequirement(JobRequirements value)
        {
            value.ExperienceRequirements = new List<ExperienceRequirement>();
            if (RequirementInputParser.TryParseMinimumExperience(
                    value.Experience, out int months) && months > 0)
            {
                value.ExperienceRequirements.Add(new ExperienceRequirement
                {
                    Name = "總工作年資",
                    MinimumMonths = months
                });
            }

        }

        private static void ApplyEducationRequirement(JobRequirements value)
        {
            value.EducationRequirement = null;
            if (RequirementInputParser.TryParseMinimumEducation(
                    value.Education, out DegreeLevel degree, out bool acceptsInProgress))
            {
                value.EducationRequirement = new EducationRequirement
                {
                    MinimumDegreeLevel = degree,
                    AcceptsInProgress = acceptsInProgress
                };
            }
        }

        private static bool IsEmpty(JobRequirements value)
        {
            return value == null
                || (value.Experience == null
                    && value.Education == null
                    && value.Major == null
                    && (value.Languages == null || value.Languages.Count == 0)
                    && (value.Tools == null || value.Tools.Count == 0)
                    && (value.Skills == null || value.Skills.Count == 0)
                    && (value.OtherConditions == null || value.OtherConditions.Count == 0)
                    && (value.SkillRequirements == null || value.SkillRequirements.Count == 0)
                    && (value.ExperienceRequirements == null || value.ExperienceRequirements.Count == 0)
                    && value.EducationRequirement == null);
        }

        private static List<string> NormalizeTextEntries(IEnumerable<string> values)
        {
            if (values == null)
            {
                return new List<string>();
            }

            return values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> NormalizeLabels(IEnumerable<string> values)
        {
            if (values == null)
            {
                return new List<string>();
            }

            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                string normalized = value.Trim();
                if (seen.Add(normalized))
                {
                    result.Add(normalized);
                }
            }

            return result;
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
