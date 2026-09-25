using System.Collections.Generic;
using JobCheck.Domain;
using NUnit.Framework;

namespace JobCheck.Persistence.Tests
{
    public sealed class V027RequirementPersistenceTests
    {
        [Test]
        public void CareerProfile_StructuredMatchFields_RoundTrip()
        {
            var profile = new CareerProfile
            {
                Id = "profile_test",
                Skills = new List<CareerSkill>
                {
                    new CareerSkill
                    {
                        Id = "skill_test",
                        Name = "C#",
                        SkillId = "csharp",
                        Level = SkillLevel.Advanced,
                        ClaimedMonths = 36
                    }
                },
                Experiences = new List<CareerExperience>
                {
                    new CareerExperience
                    {
                        Id = "experience_test",
                        SkillIds = new List<string> { "csharp", "unity" }
                    }
                },
                Educations = new List<CareerEducation>
                {
                    new CareerEducation
                    {
                        Id = "education_test",
                        DegreeLevel = DegreeLevel.Bachelor,
                        CompletionStatus = EducationCompletionStatus.Completed,
                        FieldTags = new List<string> { "資訊工程" }
                    }
                },
                Languages = new List<CareerLanguage>
                {
                    new CareerLanguage
                    {
                        Id = "language_test", Name = "英文", LanguageId = "en",
                        Proficiency = LanguageProficiency.None
                    }
                }
            };

            CareerProfileDto dto = CareerProfileDtoMapper.ToDto(profile).Value;
            CareerProfile mapped = CareerProfileDtoMapper.ToDomain(dto).Value;

            Assert.That(mapped.Skills[0].SkillId, Is.EqualTo("csharp"));
            Assert.That(mapped.Skills[0].Level, Is.EqualTo(SkillLevel.Advanced));
            Assert.That(mapped.Skills[0].ClaimedMonths, Is.EqualTo(36));
            CollectionAssert.AreEqual(
                new[] { "csharp", "unity" },
                mapped.Experiences[0].SkillIds);
            Assert.That(mapped.Educations[0].DegreeLevel, Is.EqualTo(DegreeLevel.Bachelor));
            Assert.That(mapped.Educations[0].CompletionStatus,
                Is.EqualTo(EducationCompletionStatus.Completed));
            Assert.That(dto.languages[0].proficiency, Is.EqualTo("none"));
            Assert.That(mapped.Languages[0].Proficiency, Is.EqualTo(LanguageProficiency.None));
        }

        [Test]
        public void JobRequirements_StructuredFields_RoundTrip()
        {
            var source = new JobPosting
            {
                Id = "job_test",
                Source = new JobSource { Platform = "test", Url = "https://example.com" },
                Requirements = new JobRequirements
                {
                    SkillRequirements = new List<SkillRequirement>
                    {
                        new SkillRequirement
                        {
                            SkillId = "csharp",
                            Name = "C#",
                            MinimumLevel = SkillLevel.Intermediate,
                            MinimumMonths = 24
                        }
                    },
                    ExperienceRequirements = new List<ExperienceRequirement>
                    {
                        new ExperienceRequirement { MinimumMonths = 36, Name = "總年資" }
                    },
                    EducationRequirement = new EducationRequirement
                    {
                        MinimumDegreeLevel = DegreeLevel.Bachelor,
                        AcceptsInProgress = true
                    }
                }
            };

            JobPostingDto dto = JobPostingDtoMapper.ToDto(source).Value;
            JobPosting mapped = JobPostingDtoMapper.ToDomain(dto).Value;

            Assert.That(mapped.Requirements.SkillRequirements[0].MinimumMonths,
                Is.EqualTo(24));
            Assert.That(mapped.Requirements.ExperienceRequirements[0].MinimumMonths,
                Is.EqualTo(36));
            Assert.That(mapped.Requirements.EducationRequirement.MinimumDegreeLevel,
                Is.EqualTo(DegreeLevel.Bachelor));
            Assert.That(mapped.Requirements.EducationRequirement.AcceptsInProgress, Is.True);
        }

        [Test]
        public void LegacyDtos_MissingStructuredFields_KeepUnknownState()
        {
            CareerProfile profile = CareerProfileDtoMapper.ToDomain(new CareerProfileDto
            {
                id = "profile_test",
                schema_version = CareerProfile.CurrentSchemaVersion,
                skills = new List<CareerSkillDto>
                {
                    new CareerSkillDto { id = "skill_test", name = "Unity" }
                }
            }).Value;

            Assert.That(profile.Skills[0].Level, Is.Null);
            Assert.That(profile.Skills[0].ClaimedMonths, Is.Null);
            Assert.That(profile.Skills[0].SkillId, Is.Null);
        }

        [Test]
        public void JobSearchPreferences_RoundTripAndLegacyDefaults()
        {
            var profile = new CareerProfile
            {
                Id = "profile_preferences",
                JobPreferences = new JobSearchPreferences
                {
                    TargetRoles = new List<string> { "後端工程師", "Unity 工程師" },
                    Industries = new List<string> { "資訊軟體業" },
                    AcceptedRegions = new List<string> { "台北市", "新北市" },
                    WorkSchedules = new List<string> { "一般日班", "彈性工時" },
                    MinimumSalary = 45000,
                    DesiredSalary = 60000,
                    MaximumCommuteMinutes = 45,
                    TargetImportance = PreferenceImportance.Required,
                    ScheduleImportance = PreferenceImportance.Preferred
                }
            };

            CareerProfileDto dto = CareerProfileDtoMapper.ToDto(profile).Value;
            CareerProfile mapped = CareerProfileDtoMapper.ToDomain(dto).Value;

            CollectionAssert.AreEqual(
                new[] { "後端工程師", "Unity 工程師" }, mapped.JobPreferences.TargetRoles);
            CollectionAssert.AreEqual(
                new[] { "台北市", "新北市" }, mapped.JobPreferences.AcceptedRegions);
            CollectionAssert.AreEqual(
                new[] { "一般日班", "彈性工時" }, mapped.JobPreferences.WorkSchedules);
            Assert.That(mapped.JobPreferences.MinimumSalary, Is.EqualTo(45000));
            Assert.That(mapped.JobPreferences.DesiredSalary, Is.EqualTo(60000));
            Assert.That(mapped.JobPreferences.MaximumCommuteMinutes, Is.EqualTo(45));
            Assert.That(mapped.JobPreferences.TargetImportance,
                Is.EqualTo(PreferenceImportance.Required));

            CareerProfile legacy = CareerProfileDtoMapper.ToDomain(new CareerProfileDto
            {
                id = "legacy_profile",
                schema_version = CareerProfile.CurrentSchemaVersion
            }).Value;
            Assert.That(legacy.JobPreferences, Is.Not.Null);
            Assert.That(legacy.JobPreferences.TargetRoles, Is.Empty);
        }
    }
}
