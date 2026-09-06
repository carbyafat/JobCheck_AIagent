using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace JobCheck.Persistence
{
    /// <summary>
    /// DateTimeOffset 與 V0.2 ISO 8601 JSON 字串的共通轉換工具。
    /// 讀取時要求明確的 Z 或 ±HH:mm，避免缺少時區的文字被系統自動套用本機時區。
    /// </summary>
    public static class PersistenceDateTimeConverter
    {
        private static readonly Regex ExplicitOffsetPattern = new Regex(
            "(?:[zZ]|[+-][0-9]{2}:[0-9]{2})$",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// 將 nullable 時間寫成保留完整 offset 的 round-trip 格式；沒有值時回傳 null。
        /// </summary>
        public static string Format(DateTimeOffset? value)
        {
            return value.HasValue
                ? value.Value.ToString("O", CultureInfo.InvariantCulture)
                : null;
        }

        /// <summary>
        /// 解析 required 時間。字串必須是有效 ISO 8601，且明確包含 UTC Z 或數字 offset。
        /// </summary>
        public static bool TryParseRequired(string value, out DateTimeOffset result)
        {
            result = default(DateTimeOffset);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmedValue = value.Trim();
            if (!ExplicitOffsetPattern.IsMatch(trimmedValue))
            {
                return false;
            }

            return DateTimeOffset.TryParse(
                trimmedValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result);
        }

        /// <summary>
        /// 解析 optional 時間。null 表示欄位缺漏並成功得到 null；空白或錯誤格式仍視為失敗。
        /// </summary>
        public static bool TryParseOptional(string value, out DateTimeOffset? result)
        {
            result = null;
            if (value == null)
            {
                return true;
            }

            if (!TryParseRequired(value, out DateTimeOffset parsedValue))
            {
                return false;
            }

            result = parsedValue;
            return true;
        }
    }
}
