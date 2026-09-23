using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace JobCheck.Domain.Tests
{
    public sealed class RequirementInputParserTests
    {
        [TestCase("3年以上", 36)]
        [TestCase("至少 2 年", 24)]
        [TestCase("18 個月以上", 18)]
        [TestCase("不拘", 0)]
        public void Experience_ExplicitText_ConvertsToMonths(string text, int expected)
        {
            Assert.That(RequirementInputParser.TryParseMinimumExperience(
                text, out int months), Is.True);
            Assert.That(months, Is.EqualTo(expected));
        }

        [TestCase("三年以上")]
        [TestCase("具相關經驗")]
        [TestCase("0 年")]
        public void Experience_AmbiguousText_IsNotInvented(string text)
        {
            Assert.That(RequirementInputParser.TryParseMinimumExperience(
                text, out int _), Is.False);
        }

        [Test]
        public void Education_ExplicitDegreeAndInProgress_AreParsed()
        {
            bool parsed = RequirementInputParser.TryParseMinimumEducation(
                "學士以上（在學可）", out DegreeLevel level,
                out bool acceptsInProgress);

            Assert.That(parsed, Is.True);
            Assert.That(level, Is.EqualTo(DegreeLevel.Bachelor));
            Assert.That(acceptsInProgress, Is.True);
        }

        [Test]
        public void Education_VagueMajor_IsNotParsedAsDegree()
        {
            Assert.That(RequirementInputParser.TryParseMinimumEducation(
                "相關科系", out DegreeLevel _, out bool _), Is.False);
        }

        [Test]
        public void ResumeFields_RejectInvalidQuantities()
        {
            Assert.That(RequirementInputParser.TryParseSkillLevel("6", out SkillLevel? _),
                Is.False);
            Assert.That(RequirementInputParser.TryParseLanguageLevel("7",
                out LanguageProficiency? _), Is.False);
            Assert.That(RequirementInputParser.TryParseOptionalMonths("-1", out int? _),
                Is.False);
            Assert.That(RequirementInputParser.TryParseDegree("相關科系", out DegreeLevel _),
                Is.False);
        }

        [Test]
        public void Compare_LegacyText_UsesExplicitYearsAndDegree()
        {
            var profile = new CareerProfile
            {
                Experiences = new List<CareerExperience>
                {
                    new CareerExperience
                    {
                        StartDate = "2022/01",
                        EndDate = "2025/01"
                    }
                },
                Educations = new List<CareerEducation>
                {
                    new CareerEducation
                    {
                        DegreeLevel = DegreeLevel.Bachelor,
                        CompletionStatus = EducationCompletionStatus.Completed
                    }
                }
            };

            RequirementMatchResult result = RequirementMatchEngine.Compare(
                profile,
                new JobRequirements { Experience = "3 年以上", Education = "大學以上" },
                new DateTimeOffset(2026, 9, 23, 0, 0, 0, TimeSpan.Zero));

            Assert.That(result.Items.Count, Is.EqualTo(2));
            Assert.That(result.Items.All(item => item.Status == RequirementMatchStatus.Match),
                Is.True);
        }
    }
}
