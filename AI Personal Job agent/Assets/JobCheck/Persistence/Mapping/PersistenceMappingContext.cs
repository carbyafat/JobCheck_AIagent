using System;
using System.Collections.Generic;

namespace JobCheck.Persistence
{
    /// <summary>
    /// Mapper 共用的問題收集與時間、enum 轉換入口。
    /// </summary>
    internal sealed class PersistenceMappingContext
    {
        private readonly List<PersistenceConversionIssue> issues
            = new List<PersistenceConversionIssue>();

        public IReadOnlyList<PersistenceConversionIssue> Issues => issues;

        /// <summary>
        /// 加入一筆可定位的轉換問題。
        /// </summary>
        public void Add(
            PersistenceConversionError error,
            string fieldPath,
            string rawValue = null)
        {
            issues.Add(new PersistenceConversionIssue(error, fieldPath, rawValue));
        }

        /// <summary>
        /// 解析 required 時間並以 nullable 型別回傳，讓 Mapper 可直接指定給 Domain 欄位。
        /// </summary>
        public DateTimeOffset? ParseRequiredDateTime(string value, string fieldPath)
        {
            if (PersistenceDateTimeConverter.TryParseRequired(
                value,
                out DateTimeOffset parsed))
            {
                return parsed;
            }

            Add(PersistenceConversionError.InvalidDateTime, fieldPath, value);
            return null;
        }

        /// <summary>
        /// 解析 optional 時間；null 是合法缺漏，其他無效文字會留下 Issue。
        /// </summary>
        public DateTimeOffset? ParseOptionalDateTime(string value, string fieldPath)
        {
            if (PersistenceDateTimeConverter.TryParseOptional(
                value,
                out DateTimeOffset? parsed))
            {
                return parsed;
            }

            Add(PersistenceConversionError.InvalidDateTime, fieldPath, value);
            return null;
        }

        /// <summary>
        /// 解析 required enum；失敗時回傳 enum 預設值，但最終 Result 會因 Issue 而捨棄整個 Value。
        /// </summary>
        public TEnum ParseRequiredEnum<TEnum>(string value, string fieldPath)
            where TEnum : struct
        {
            if (PersistenceEnumConverter.TryParse(value, out TEnum parsed))
            {
                return parsed;
            }

            Add(PersistenceConversionError.InvalidEnum, fieldPath, value);
            return default(TEnum);
        }

        /// <summary>
        /// 解析 optional enum；null 是合法缺漏，空白或未知字串仍會留下 Issue。
        /// </summary>
        public TEnum? ParseOptionalEnum<TEnum>(string value, string fieldPath)
            where TEnum : struct
        {
            if (value == null)
            {
                return null;
            }

            if (PersistenceEnumConverter.TryParse(value, out TEnum parsed))
            {
                return parsed;
            }

            Add(PersistenceConversionError.InvalidEnum, fieldPath, value);
            return null;
        }

        /// <summary>
        /// 格式化已定義 enum；未定義值會轉為 Issue，而不是讓例外中斷整批轉換。
        /// </summary>
        public string FormatEnum<TEnum>(TEnum value, string fieldPath)
            where TEnum : struct
        {
            try
            {
                return PersistenceEnumConverter.Format(value);
            }
            catch (ArgumentException)
            {
                Add(PersistenceConversionError.InvalidEnum, fieldPath, value.ToString());
                return null;
            }
        }

        /// <summary>
        /// 格式化 optional enum；null 維持 null。
        /// </summary>
        public string FormatOptionalEnum<TEnum>(TEnum? value, string fieldPath)
            where TEnum : struct
        {
            return value.HasValue ? FormatEnum(value.Value, fieldPath) : null;
        }

        /// <summary>
        /// 建立結果；只要 Context 已收集 Issue，公開 Value 就會是 null。
        /// </summary>
        public PersistenceConversionResult<T> CreateResult<T>(T value)
            where T : class
        {
            return new PersistenceConversionResult<T>(value, issues);
        }
    }
}
