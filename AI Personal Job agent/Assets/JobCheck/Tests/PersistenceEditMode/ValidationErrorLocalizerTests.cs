using System;
using JobCheck.Domain;
using JobCheck.Persistence;
using NUnit.Framework;

namespace JobCheck.Tests
{
    /// <summary>
    /// 確保所有目前定義的 Domain 錯誤代碼都有可供 UI 顯示的中文訊息。
    /// </summary>
    public sealed class ValidationErrorLocalizerTests
    {
        [Test]
        public void JobPostingErrors_AllHaveChineseMessages()
        {
            foreach (JobPostingValidationError error in
                Enum.GetValues(typeof(JobPostingValidationError)))
            {
                Assert.IsTrue(ValidationErrorLocalizer.HasTranslation(error), error.ToString());
                AssertLocalized(error.ToString(), ValidationErrorLocalizer.ToChinese(error));
            }
        }

        [Test]
        public void ApplicationErrors_AllHaveChineseMessages()
        {
            foreach (ApplicationValidationError error in
                Enum.GetValues(typeof(ApplicationValidationError)))
            {
                Assert.IsTrue(ValidationErrorLocalizer.HasTranslation(error), error.ToString());
                AssertLocalized(error.ToString(), ValidationErrorLocalizer.ToChinese(error));
            }
        }

        [Test]
        public void ApplicationEventErrors_AllHaveChineseMessages()
        {
            foreach (ApplicationEventValidationError error in
                Enum.GetValues(typeof(ApplicationEventValidationError)))
            {
                Assert.IsTrue(ValidationErrorLocalizer.HasTranslation(error), error.ToString());
                AssertLocalized(error.ToString(), ValidationErrorLocalizer.ToChinese(error));
            }
        }

        [Test]
        public void ReductionIssues_AllHaveChineseMessages()
        {
            foreach (ApplicationStateReductionIssue issue in
                Enum.GetValues(typeof(ApplicationStateReductionIssue)))
            {
                Assert.IsTrue(ValidationErrorLocalizer.HasTranslation(issue), issue.ToString());
                AssertLocalized(issue.ToString(), ValidationErrorLocalizer.ToChinese(issue));
            }
        }

        [Test]
        public void SameNamedErrors_UseContextSpecificMessages()
        {
            string jobPostingMessage = ValidationErrorLocalizer.ToChinese(
                JobPostingValidationError.MissingId);
            string applicationMessage = ValidationErrorLocalizer.ToChinese(
                ApplicationValidationError.MissingId);
            string eventMessage = ValidationErrorLocalizer.ToChinese(
                ApplicationEventValidationError.MissingId);

            Assert.AreNotEqual(jobPostingMessage, applicationMessage);
            Assert.AreNotEqual(applicationMessage, eventMessage);
            Assert.AreNotEqual(jobPostingMessage, eventMessage);
        }

        private static void AssertLocalized(string errorCode, string message)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(message), errorCode);
            Assert.AreNotEqual(errorCode, message, errorCode);
            Assert.IsTrue(ContainsCjkCharacter(message), errorCode + ": " + message);
        }

        private static bool ContainsCjkCharacter(string value)
        {
            foreach (char character in value)
            {
                if (character >= '\u4e00' && character <= '\u9fff')
                {
                    return true;
                }
            }

            return false;
        }
    }
}
