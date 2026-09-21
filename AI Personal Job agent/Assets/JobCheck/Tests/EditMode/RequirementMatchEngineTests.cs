using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace JobCheck.Domain.Tests
{
    public sealed class RequirementMatchEngineTests
    {
        private static readonly DateTimeOffset EvaluatedAt =
            new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);

        [Test]
        public void Compare_AbsentRequiredSkill_IsNotEvidencedAndNotClaimedIncapable()
        {
            RequirementMatchResult result = RequirementMatchEngine.Compare(
                new CareerProfile { Id = "profile" },
                new JobRequirements
                {
                    Skills = new List<string> { "C#" }
                },
                EvaluatedAt);

            Assert.That(result.Items.Single().Status,
                Is.EqualTo(RequirementMatchStatus.NotEvidenced));
            StringAssert.Contains("沒有", result.Items.Single().Explanation);
        }

        [Test]
        public void Compare_SkillBelowExplicitLevel_IsConfirmedMismatch()
        {
            var profile = new CareerProfile
            {
                Id = "profile",
                Skills = new List<CareerSkill>
                {
                    new CareerSkill { Id = "skill", Name = "C Sharp", Level = SkillLevel.Basic }
                }
            };
            var requirements = new JobRequirements
            {
                SkillRequirements = new List<SkillRequirement>
                {
                    new SkillRequirement
                    {
                        Name = "C#",
                        MinimumLevel = SkillLevel.Advanced
                    }
                }
            };

            Assert.That(RequirementMatchEngine.Compare(profile, requirements, EvaluatedAt)
                .Items.Single().Status, Is.EqualTo(RequirementMatchStatus.ConfirmedMismatch));
        }

        [Test]
        public void CalculateCoveredMonths_MergesOverlappingEmployment()
        {
            var experiences = new[]
            {
                Experience("2020-01", "2022-01"),
                Experience("2021-01", "2023-01")
            };

            Assert.That(RequirementMatchEngine.CalculateCoveredMonths(
                experiences, EvaluatedAt), Is.EqualTo(36));
        }

        [Test]
        public void Compare_TotalExperience_UsesMergedMonthCount()
        {
            var profile = new CareerProfile
            {
                Id = "profile",
                Experiences = new List<CareerExperience>
                {
                    Experience("2020-01", "2022-01"),
                    Experience("2021-01", "2023-01")
                }
            };
            var requirements = new JobRequirements
            {
                ExperienceRequirements = new List<ExperienceRequirement>
                {
                    new ExperienceRequirement { Name = "總年資", MinimumMonths = 36 }
                }
            };

            RequirementMatchItem item = RequirementMatchEngine.Compare(
                profile, requirements, EvaluatedAt).Items.Single();
            Assert.That(item.Status, Is.EqualTo(RequirementMatchStatus.Match));
            Assert.That(item.ActualValue, Is.EqualTo("3 年"));
        }

        [Test]
        public void Compare_EducationLevelAndCompletion_AreBothRequired()
        {
            var profile = new CareerProfile
            {
                Id = "profile",
                Educations = new List<CareerEducation>
                {
                    new CareerEducation
                    {
                        Id = "education",
                        DegreeLevel = DegreeLevel.Master,
                        CompletionStatus = EducationCompletionStatus.InProgress
                    }
                }
            };
            var requirements = new JobRequirements
            {
                EducationRequirement = new EducationRequirement
                {
                    MinimumDegreeLevel = DegreeLevel.Bachelor,
                    AcceptsInProgress = false
                }
            };

            Assert.That(RequirementMatchEngine.Compare(profile, requirements, EvaluatedAt)
                .Items.Single().Status, Is.EqualTo(RequirementMatchStatus.ConfirmedMismatch));
        }

        [Test]
        public void Compare_UnstructuredExperience_IsRequirementUnclear()
        {
            var requirements = new JobRequirements { Experience = "三年以上" };

            Assert.That(RequirementMatchEngine.Compare(
                new CareerProfile { Id = "profile" }, requirements, EvaluatedAt)
                .Items.Single().Status, Is.EqualTo(RequirementMatchStatus.RequirementUnclear));
        }

        private static CareerExperience Experience(string start, string end)
        {
            return new CareerExperience
            {
                Id = Guid.NewGuid().ToString("N"),
                StartDate = start,
                EndDate = end
            };
        }
    }
}
