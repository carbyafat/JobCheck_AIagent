using System;
using System.Text;

namespace JobCheck.Persistence
{
    /// <summary>
    /// 將 C# enum 名稱與 V0.2 小寫 snake_case 字串互相轉換。
    /// 只接受 enum 已定義值，無法辨識時不得偷偷使用數值 0 當預設答案。
    /// </summary>
    public static class PersistenceEnumConverter
    {
        /// <summary>
        /// 將已定義 enum 值格式化為小寫 snake_case；未定義值或非 enum 型別會拋出 ArgumentException。
        /// </summary>
        public static string Format<TEnum>(TEnum value)
            where TEnum : struct
        {
            Type enumType = typeof(TEnum);
            if (!enumType.IsEnum || !Enum.IsDefined(enumType, value))
            {
                throw new ArgumentException("Value must be a defined enum member.", nameof(value));
            }

            return ConvertNameToSnakeCase(value.ToString());
        }

        /// <summary>
        /// 將小寫 snake_case 字串解析成已定義 enum；大小寫或格式不符時回傳 false。
        /// </summary>
        public static bool TryParse<TEnum>(string value, out TEnum result)
            where TEnum : struct
        {
            result = default(TEnum);
            Type enumType = typeof(TEnum);
            if (!enumType.IsEnum || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (object candidateObject in Enum.GetValues(enumType))
            {
                var candidate = (TEnum)candidateObject;
                if (string.Equals(
                    Format(candidate),
                    value,
                    StringComparison.Ordinal))
                {
                    result = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 將 PascalCase 名稱轉成 snake_case，並正確處理連續大寫字母的單字邊界。
        /// </summary>
        private static string ConvertNameToSnakeCase(string name)
        {
            var builder = new StringBuilder(name.Length + 8);
            for (int index = 0; index < name.Length; index++)
            {
                char current = name[index];
                bool currentIsUpper = char.IsUpper(current);
                bool previousIsLowerOrDigit = index > 0
                    && (char.IsLower(name[index - 1]) || char.IsDigit(name[index - 1]));
                bool startsLastWordOfAcronym = index > 0
                    && currentIsUpper
                    && char.IsUpper(name[index - 1])
                    && index + 1 < name.Length
                    && char.IsLower(name[index + 1]);

                if (currentIsUpper && (previousIsLowerOrDigit || startsLastWordOfAcronym))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
            }

            return builder.ToString();
        }
    }
}
