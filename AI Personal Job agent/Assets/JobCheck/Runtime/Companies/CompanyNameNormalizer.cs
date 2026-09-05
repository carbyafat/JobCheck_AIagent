using System;
using System.Text;

namespace JobCheck.Domain
{
    /// <summary>
    /// 提供公司名稱的正規化與同名判定。
    /// 僅處理已定案的文字格式差異，不進行模糊比對、別名推測或跨語言翻譯。
    /// </summary>
    public static class CompanyNameNormalizer
    {
        /// <summary>
        /// 將公司名稱轉成可供精確比較的形式：使用 Unicode Form C 正規化、
        /// 移除頭尾空白，並將中間連續的各類空白統一為一個半形空格。
        /// 此方法不改變英文字母大小寫，也不移除「股份有限公司」等名稱內容。
        /// </summary>
        /// <param name="name">要正規化的原始公司名稱；不得為 null。</param>
        /// <returns>完成正規化的公司名稱；空字串或純空白輸入會得到空字串。</returns>
        /// <exception cref="ArgumentNullException">name 為 null 時拋出。</exception>
        public static string Normalize(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            string unicodeNormalized = name.Normalize(NormalizationForm.FormC);
            var result = new StringBuilder(unicodeNormalized.Length);
            bool hasPendingWhitespace = false;

            foreach (char character in unicodeNormalized)
            {
                if (char.IsWhiteSpace(character))
                {
                    hasPendingWhitespace = result.Length > 0;
                    continue;
                }

                if (hasPendingWhitespace)
                {
                    result.Append(' ');
                    hasPendingWhitespace = false;
                }

                result.Append(character);
            }

            return result.ToString();
        }

        /// <summary>
        /// 判斷兩個有效名稱在正規化後是否完全相同。
        /// 比較不考慮資料來源平台；null、空字串及純空白名稱一律視為無效，因此不會互相判定為同一公司。
        /// </summary>
        /// <param name="firstName">第一個原始公司名稱。</param>
        /// <param name="secondName">第二個原始公司名稱。</param>
        /// <returns>兩個有效名稱正規化後完全相同時為 true，否則為 false。</returns>
        public static bool AreEquivalent(string firstName, string secondName)
        {
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(secondName))
            {
                return false;
            }

            return string.Equals(
                Normalize(firstName),
                Normalize(secondName),
                StringComparison.Ordinal);
        }
    }
}
