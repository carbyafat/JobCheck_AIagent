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
    }
}

