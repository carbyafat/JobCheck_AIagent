using System.Collections.Generic;
using NUnit.Framework;

namespace JobCheck.Domain.Tests
{
    public sealed class RequirementScoreTests
    {
        [Test]
        public void Calculate_NoAssessableConditions_DoesNotInventZeroScore()
        {
            var match = new RequirementMatchResult
            {
                Items = new List<RequirementMatchItem>
                {
                    Item(RequirementCategory.Experience,
                        RequirementMatchStatus.RequirementUnclear)
                }
            };

            RequirementScore result = RequirementScoreCalculator.Calculate(match);

            Assert.That(result.Score, Is.Null);
            Assert.That(result.UnclearCount, Is.EqualTo(1));
        }

        [Test]
        public void Calculate_NormalizesOnlyCategoriesWithAssessableConditions()
        {
            var match = new RequirementMatchResult
            {
                Items = new List<RequirementMatchItem>
                {
                    Item(RequirementCategory.Skill, RequirementMatchStatus.Match),
                    Item(RequirementCategory.Skill, RequirementMatchStatus.NotEvidenced),
                    Item(RequirementCategory.Experience, RequirementMatchStatus.Match)
                }
            };

            RequirementScore result = RequirementScoreCalculator.Calculate(match);

            // 技能 20/40 + 年資 30/30，使用中的總權重為 70。
            Assert.That(result.Score, Is.EqualTo(71));
            Assert.That(result.StrictMatchRate, Is.EqualTo(2d / 3d).Within(0.001));
            Assert.That(result.NotEvidencedCount, Is.EqualTo(1));
        }

        [Test]
        public void Calculate_RequiredNotEvidenced_IsHardWarning()
        {
            RequirementMatchItem warning = Item(
                RequirementCategory.Education,
                RequirementMatchStatus.NotEvidenced);
            var match = new RequirementMatchResult
            {
                Items = new List<RequirementMatchItem> { warning }
            };

            RequirementScore result = RequirementScoreCalculator.Calculate(match);

            CollectionAssert.AreEqual(new[] { warning }, result.HardRequirementWarnings);
        }

        private static RequirementMatchItem Item(
            RequirementCategory category,
            RequirementMatchStatus status)
        {
            return new RequirementMatchItem
            {
                Category = category,
                Status = status,
                Importance = RequirementImportance.Required,
                Label = "測試"
            };
        }
    }
}
