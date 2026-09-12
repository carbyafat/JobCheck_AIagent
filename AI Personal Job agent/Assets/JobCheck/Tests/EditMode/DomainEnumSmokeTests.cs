using System;
using NUnit.Framework;

namespace JobCheck.Domain.Tests
{
    /// <summary>
    /// 驗證 EditMode 測試組件能引用 Domain 組件，並保護幾項最基本的 enum 語意。
    /// </summary>
    public class DomainEnumSmokeTests
    {
        /// <summary>
        /// 確認測試組件可以存取 JobCheck.Domain 中定義的應徵階段。
        /// </summary>
        [Test]
        public void DomainAssembly_CanBeReferenced()
        {
            ApplicationStage stage = ApplicationStage.Applied;

            Assert.AreEqual(ApplicationStage.Applied, stage);
        }

        /// <summary>
        /// 確認公司拒絕與本人主動放棄是兩個不同的結案階段。
        /// </summary>
        [Test]
        public void RejectedAndClosed_AreDifferentStages()
        {
            Assert.AreNotEqual(
                ApplicationStage.RejectedByCompany,
                ApplicationStage.ClosedByCandidate);
        }

        /// <summary>
        /// 確認外部參考報告不會與使用者自己的應徵紀錄使用相同來源類型。
        /// </summary>
        [Test]
        public void ExternalReport_IsNotMyApplication()
        {
            Assert.AreNotEqual(
                SourceType.MyApplication,
                SourceType.ExternalReport);
        }

        /// <summary>
        /// 確認本人結案原因保留 Other，供未列出的原因搭配文字說明使用。
        /// </summary>
        [Test]
        public void OtherCloseReason_IsAvailable()
        {
            Assert.IsTrue(
                Enum.IsDefined(
                    typeof(CandidateCloseReason),
                    CandidateCloseReason.Other));
        }

        /// <summary>
        /// 確認 UI 可以用中文名稱取得穩定的 Tag 持久化值。
        /// </summary>
        [TestCase("Unity", JobPostingLabelCatalog.TagUnity)]
        [TestCase("C#", JobPostingLabelCatalog.TagCSharp)]
        [TestCase("遠端工作", JobPostingLabelCatalog.TagRemote)]
        public void JobTagCatalog_ResolvesDisplayName(string input, string expected)
        {
            Assert.IsTrue(JobPostingLabelCatalog.TryResolveTag(input, out string value));
            Assert.AreEqual(expected, value);
        }

        /// <summary>
        /// 確認博弈產業與其他風險中文名稱能轉成穩定的 RiskFlag 值。
        /// </summary>
        [TestCase("博弈產業", JobPostingLabelCatalog.RiskGamblingIndustry)]
        [TestCase("週末值班", JobPostingLabelCatalog.RiskWeekendDuty)]
        [TestCase("薪資不透明", JobPostingLabelCatalog.RiskSalaryOpaque)]
        public void JobRiskFlagCatalog_ResolvesDisplayName(string input, string expected)
        {
            Assert.IsTrue(JobPostingLabelCatalog.TryResolveRiskFlag(input, out string value));
            Assert.AreEqual(expected, value);
        }

        /// <summary>
        /// 確認未知舊值不會被轉成空字串，詳細頁仍能顯示原始內容。
        /// </summary>
        [Test]
        public void JobLabelCatalog_UnknownValue_RemainsVisible()
        {
            Assert.AreEqual(
                "legacy_custom_tag",
                JobPostingLabelCatalog.GetTagDisplayName("legacy_custom_tag"));
            Assert.AreEqual(
                "legacy_custom_risk",
                JobPostingLabelCatalog.GetRiskFlagDisplayName("legacy_custom_risk"));
        }

        /// <summary>
        /// 確認 UI 不會接受受控清單之外的新自由文字值。
        /// </summary>
        [Test]
        public void JobLabelCatalog_UnknownInput_IsRejected()
        {
            Assert.IsFalse(JobPostingLabelCatalog.TryResolveTag("隨手亂填", out _));
            Assert.IsFalse(JobPostingLabelCatalog.TryResolveRiskFlag("不明風險", out _));
        }
    }
}

