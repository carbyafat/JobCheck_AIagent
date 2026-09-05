using System;
using NUnit.Framework;

namespace JobCheck.Domain.Tests
{
    /// <summary>
    /// 驗證 Company ID 產生規則，以及第一版保守的公司名稱正規化與同名判定規則。
    /// </summary>
    public class CompanyTests
    {
        /// <summary>
        /// 確認新 Company ID 符合 cmp_ 加上 32 位小寫十六進位 UUID 的格式。
        /// </summary>
        [Test]
        public void CompanyIdGenerator_Create_ReturnsExpectedFormat()
        {
            string companyId = CompanyIdGenerator.Create();

            StringAssert.IsMatch("^cmp_[0-9a-f]{32}$", companyId);
        }

        /// <summary>
        /// 確認連續建立兩個 Company ID 時，不會得到同一個識別碼。
        /// </summary>
        [Test]
        public void CompanyIdGenerator_CreateTwice_ReturnsDifferentIds()
        {
            string firstId = CompanyIdGenerator.Create();
            string secondId = CompanyIdGenerator.Create();

            Assert.AreNotEqual(firstId, secondId);
        }

        /// <summary>
        /// 確認公司名稱正規化會移除開頭與結尾的空白。
        /// </summary>
        [Test]
        public void Normalize_LeadingAndTrailingWhitespace_RemovesWhitespace()
        {
            string normalized = CompanyNameNormalizer.Normalize("  OpenAI Taiwan  ");

            Assert.AreEqual("OpenAI Taiwan", normalized);
        }

        /// <summary>
        /// 確認空格、Tab 與換行等連續空白，會被統一成一個半形空格。
        /// </summary>
        [Test]
        public void Normalize_ConsecutiveWhitespace_CollapsesToSingleSpace()
        {
            string normalized = CompanyNameNormalizer.Normalize("OpenAI \t \r\n Taiwan");

            Assert.AreEqual("OpenAI Taiwan", normalized);
        }

        /// <summary>
        /// 確認視覺上相同但 Unicode 組合方式不同的文字，正規化後會得到相同結果。
        /// </summary>
        [Test]
        public void Normalize_CanonicallyEquivalentUnicode_ReturnsSameText()
        {
            string composed = CompanyNameNormalizer.Normalize("Caf\u00E9");
            string decomposed = CompanyNameNormalizer.Normalize("Cafe\u0301");

            Assert.AreEqual(composed, decomposed);
        }

        /// <summary>
        /// 確認名稱只有無意義的空白格式差異時，會被判定為同一公司。
        /// 此判定不需要也不接受平台資訊，因此跨平台時規則相同。
        /// </summary>
        [Test]
        public void AreEquivalent_OnlyWhitespaceFormattingDiffers_ReturnsTrue()
        {
            bool result = CompanyNameNormalizer.AreEquivalent(
                "  OpenAI   Taiwan ",
                "OpenAI Taiwan");

            Assert.IsTrue(result);
        }

        /// <summary>
        /// 確認明顯不同的公司名稱不會被判定為同一公司。
        /// </summary>
        [Test]
        public void AreEquivalent_DifferentNames_ReturnsFalse()
        {
            bool result = CompanyNameNormalizer.AreEquivalent("OpenAI", "Google");

            Assert.IsFalse(result);
        }

        /// <summary>
        /// 確認 null、空字串與純空白不是有效公司名稱，也不會彼此誤判為同一公司。
        /// </summary>
        [Test]
        public void AreEquivalent_InvalidNames_ReturnsFalse()
        {
            Assert.IsFalse(CompanyNameNormalizer.AreEquivalent(null, "OpenAI"));
            Assert.IsFalse(CompanyNameNormalizer.AreEquivalent("", ""));
            Assert.IsFalse(CompanyNameNormalizer.AreEquivalent("   ", "\t"));
        }

        /// <summary>
        /// 確認第一版不忽略英文字母大小寫，避免自動合併尚未確認的名稱。
        /// </summary>
        [Test]
        public void AreEquivalent_DifferentLetterCase_ReturnsFalse()
        {
            bool result = CompanyNameNormalizer.AreEquivalent("OpenAI", "OPENAI");

            Assert.IsFalse(result);
        }

        /// <summary>
        /// 確認第一版不自行推測公司簡稱或別名，疑似同公司資料必須留待人工確認。
        /// </summary>
        [Test]
        public void AreEquivalent_PossibleAlias_ReturnsFalse()
        {
            bool result = CompanyNameNormalizer.AreEquivalent(
                "台積電",
                "台灣積體電路股份有限公司");

            Assert.IsFalse(result);
        }

        /// <summary>
        /// 確認直接正規化 null 時會明確回報呼叫錯誤，而不是靜默產生空名稱。
        /// </summary>
        [Test]
        public void Normalize_Null_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => CompanyNameNormalizer.Normalize(null));
        }
    }
}
