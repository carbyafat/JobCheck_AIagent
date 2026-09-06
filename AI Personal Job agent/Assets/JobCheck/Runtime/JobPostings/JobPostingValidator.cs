using System;
using System.Collections.Generic;
using System.Linq;

namespace JobCheck.Domain
{
    /// <summary>
    /// 驗證 JobPosting 的必要欄位、來源格式與不同建立情境下的 ID 規則。
    /// 此 Validator 只檢查單筆資料；ID 是否重複及 CompanyId 是否真的存在，需由持有完整集合的 Repository 驗證。
    /// </summary>
    public static class JobPostingValidator
    {
        /// <summary>
        /// 驗證一筆新建立的職缺。新 ID 必須符合 job_ 加上 32 位小寫十六進位 UUID。
        /// </summary>
        /// <param name="jobPosting">要驗證的新職缺；可以傳入 null 以取得 MissingJobPosting。</param>
        /// <returns>所有已發現的錯誤代碼；空集合表示此 Validator 沒有發現錯誤。</returns>
        public static IReadOnlyList<JobPostingValidationError> ValidateNew(JobPosting jobPosting)
        {
            var errors = ValidateRequiredFields(jobPosting);
            if (jobPosting == null)
            {
                return errors;
            }

            if (!string.IsNullOrWhiteSpace(jobPosting.Id) && !IsValidNewId(jobPosting.Id))
            {
                errors.Add(JobPostingValidationError.InvalidNewId);
            }

            return errors;
        }

        /// <summary>
        /// 驗證 migration 產生的職缺。既有 job ID 可以不符合新版格式，
        /// 但必須為非空白，且不得包含 /、\ 或 .. 等不安全路徑內容。
        /// </summary>
        /// <param name="jobPosting">要驗證的 migration 結果；可以傳入 null 以取得 MissingJobPosting。</param>
        /// <returns>所有已發現的錯誤代碼；空集合表示此 Validator 沒有發現錯誤。</returns>
        public static IReadOnlyList<JobPostingValidationError> ValidateForMigration(JobPosting jobPosting)
        {
            var errors = ValidateRequiredFields(jobPosting);
            if (jobPosting == null)
            {
                return errors;
            }

            if (!string.IsNullOrWhiteSpace(jobPosting.Id) && !IsSafeLegacyId(jobPosting.Id))
            {
                errors.Add(JobPostingValidationError.UnsafeLegacyId);
            }

            return errors;
        }

        /// <summary>
        /// 判斷 ID 是否符合新 JobPosting 的固定格式。
        /// </summary>
        /// <param name="id">要檢查的職缺 ID。</param>
        /// <returns>符合 job_ 加上 32 位小寫十六進位 UUID 時為 true。</returns>
        private static bool IsValidNewId(string id)
        {
            if (!id.StartsWith(JobPostingIdGenerator.Prefix, StringComparison.Ordinal) || id.Length != 36)
            {
                return false;
            }

            string uuidPart = id.Substring(JobPostingIdGenerator.Prefix.Length);
            return string.Equals(uuidPart, uuidPart.ToLowerInvariant(), StringComparison.Ordinal)
                && Guid.TryParseExact(uuidPart, "N", out _);
        }

        /// <summary>
        /// 判斷 migration 想保留的舊 ID 是否不會被誤作相對路徑或目錄跳脫內容。
        /// </summary>
        /// <param name="id">要檢查的舊職缺 ID。</param>
        /// <returns>不含路徑分隔符及 .. 時為 true。</returns>
        private static bool IsSafeLegacyId(string id)
        {
            return !id.Contains("/")
                && !id.Contains("\\")
                && !id.Contains("..");
        }

        /// <summary>
        /// 收集新資料與 migration 共用的必要欄位錯誤。
        /// </summary>
        /// <param name="jobPosting">要檢查的職缺。</param>
        /// <returns>可繼續加入情境專屬錯誤的清單。</returns>
        private static List<JobPostingValidationError> ValidateRequiredFields(JobPosting jobPosting)
        {
            var errors = new List<JobPostingValidationError>();

            if (jobPosting == null)
            {
                errors.Add(JobPostingValidationError.MissingJobPosting);
                return errors;
            }

            if (string.IsNullOrWhiteSpace(jobPosting.Id))
            {
                errors.Add(JobPostingValidationError.MissingId);
            }

            if (!string.Equals(
                jobPosting.SchemaVersion,
                JobPosting.CurrentSchemaVersion,
                StringComparison.Ordinal))
            {
                errors.Add(JobPostingValidationError.UnsupportedSchemaVersion);
            }

            if (string.IsNullOrWhiteSpace(jobPosting.CompanyId))
            {
                errors.Add(JobPostingValidationError.MissingCompanyId);
            }

            if (string.IsNullOrWhiteSpace(jobPosting.Title))
            {
                errors.Add(JobPostingValidationError.MissingTitle);
            }

            if (jobPosting.Source == null)
            {
                errors.Add(JobPostingValidationError.MissingSource);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(jobPosting.Source.Platform))
                {
                    errors.Add(JobPostingValidationError.MissingSourcePlatform);
                }

                if (!string.IsNullOrWhiteSpace(jobPosting.Source.Url)
                    && !IsValidWebUrl(jobPosting.Source.Url))
                {
                    errors.Add(JobPostingValidationError.InvalidSourceUrl);
                }
            }

            if (!jobPosting.CapturedAt.HasValue)
            {
                errors.Add(JobPostingValidationError.MissingCapturedAt);
            }

            ValidateOptionalFields(jobPosting, errors);

            return errors;
        }

        /// <summary>
        /// 驗證 optional 詳細資料內的明顯矛盾與空白清單項目。
        /// 整個 optional 物件或集合為 null 代表來源缺漏，本層不把缺漏誤判成錯誤。
        /// </summary>
        private static void ValidateOptionalFields(
            JobPosting jobPosting,
            ICollection<JobPostingValidationError> errors)
        {
            ValidateCompensation(jobPosting.Compensation, errors);

            if (HasInvalidTextEntry(jobPosting.Responsibilities))
            {
                errors.Add(JobPostingValidationError.InvalidResponsibilityEntry);
            }

            if (HasInvalidTextEntry(jobPosting.RecruitmentProcess))
            {
                errors.Add(JobPostingValidationError.InvalidRecruitmentProcessEntry);
            }

            if (HasInvalidTextEntry(jobPosting.Tags))
            {
                errors.Add(JobPostingValidationError.InvalidTagEntry);
            }

            if (HasInvalidTextEntry(jobPosting.RiskFlags))
            {
                errors.Add(JobPostingValidationError.InvalidRiskFlagEntry);
            }

            ValidateRequirements(jobPosting.Requirements, errors);
            ValidateBenefits(jobPosting.Benefits, errors);
        }

        /// <summary>
        /// 薪資資料可以不完整，但已提供的數值不得為負數或形成反向範圍。
        /// </summary>
        private static void ValidateCompensation(
            JobCompensation compensation,
            ICollection<JobPostingValidationError> errors)
        {
            if (compensation == null)
            {
                return;
            }

            bool containsNegativeAmount =
                (compensation.Minimum.HasValue && compensation.Minimum.Value < 0)
                || (compensation.Maximum.HasValue && compensation.Maximum.Value < 0);
            if (containsNegativeAmount)
            {
                errors.Add(JobPostingValidationError.NegativeCompensationAmount);
            }

            if (compensation.Minimum.HasValue
                && compensation.Maximum.HasValue
                && compensation.Maximum.Value < compensation.Minimum.Value)
            {
                errors.Add(JobPostingValidationError.CompensationMaximumBelowMinimum);
            }
        }

        /// <summary>
        /// 驗證技能相關集合與語文條件；缺少整個 Requirements 物件仍屬合法 optional 缺漏。
        /// </summary>
        private static void ValidateRequirements(
            JobRequirements requirements,
            ICollection<JobPostingValidationError> errors)
        {
            if (requirements == null)
            {
                return;
            }

            if (HasInvalidTextEntry(requirements.Tools)
                || HasInvalidTextEntry(requirements.Skills)
                || HasInvalidTextEntry(requirements.OtherConditions))
            {
                errors.Add(JobPostingValidationError.InvalidRequirementEntry);
            }

            if (requirements.Languages == null)
            {
                return;
            }

            foreach (JobLanguageRequirement language in requirements.Languages)
            {
                if (language == null || IsEmptyLanguageRequirement(language))
                {
                    errors.Add(JobPostingValidationError.InvalidLanguageRequirement);
                    return;
                }
            }
        }

        /// <summary>
        /// 判斷語文條件是否完全沒有可保存的資訊。
        /// </summary>
        private static bool IsEmptyLanguageRequirement(JobLanguageRequirement language)
        {
            return string.IsNullOrWhiteSpace(language.Name)
                && string.IsNullOrWhiteSpace(language.Listening)
                && string.IsNullOrWhiteSpace(language.Speaking)
                && string.IsNullOrWhiteSpace(language.Reading)
                && string.IsNullOrWhiteSpace(language.Writing)
                && string.IsNullOrWhiteSpace(language.RawText);
        }

        /// <summary>
        /// 驗證所有福利分類的集合項目；整個 Benefits 或個別分類缺漏仍可由 migration 列 warning。
        /// </summary>
        private static void ValidateBenefits(
            JobBenefits benefits,
            ICollection<JobPostingValidationError> errors)
        {
            if (benefits == null)
            {
                return;
            }

            bool containsInvalidEntry =
                HasInvalidTextEntry(benefits.SalaryBonus)
                || HasInvalidTextEntry(benefits.InsuranceHealth)
                || HasInvalidTextEntry(benefits.Flexibility)
                || HasInvalidTextEntry(benefits.Training)
                || HasInvalidTextEntry(benefits.Life)
                || HasInvalidTextEntry(benefits.Other);
            if (containsInvalidEntry)
            {
                errors.Add(JobPostingValidationError.InvalidBenefitEntry);
            }
        }

        /// <summary>
        /// 判斷有提供的文字集合是否含有無意義的空白項目；null 代表 optional 欄位缺漏。
        /// </summary>
        private static bool HasInvalidTextEntry(IEnumerable<string> values)
        {
            return values != null && values.Any(string.IsNullOrWhiteSpace);
        }

        /// <summary>
        /// 判斷有填寫的來源網址是否為 HTTP 或 HTTPS 絕對網址。
        /// </summary>
        /// <param name="url">要檢查的來源網址。</param>
        /// <returns>可解析且使用 HTTP 或 HTTPS 時為 true。</returns>
        private static bool IsValidWebUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri parsedUrl))
            {
                return false;
            }

            return string.Equals(parsedUrl.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(parsedUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }
    }
}
