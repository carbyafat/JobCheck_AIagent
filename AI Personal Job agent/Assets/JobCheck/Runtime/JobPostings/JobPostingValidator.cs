using System;
using System.Collections.Generic;

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

            return errors;
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
