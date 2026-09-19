using System;
using System.Linq;
using NUnit.Framework;

namespace JobCheck.Domain.Tests
{
    public sealed class CareerProfileTests
    {
        [Test]
        public void IdGenerators_ReturnStableTypePrefixes()
        {
            StringAssert.IsMatch("^prf_[0-9a-f]{32}$", CareerProfileIdGenerator.CreateProfileId());
            StringAssert.IsMatch("^lnk_[0-9a-f]{32}$", CareerProfileIdGenerator.CreateLinkId());
            StringAssert.IsMatch("^skl_[0-9a-f]{32}$", CareerProfileIdGenerator.CreateSkillId());
            StringAssert.IsMatch("^exp_[0-9a-f]{32}$", CareerProfileIdGenerator.CreateExperienceId());
            StringAssert.IsMatch("^prj_[0-9a-f]{32}$", CareerProfileIdGenerator.CreateProjectId());
            StringAssert.IsMatch("^edu_[0-9a-f]{32}$", CareerProfileIdGenerator.CreateEducationId());
            StringAssert.IsMatch("^lng_[0-9a-f]{32}$", CareerProfileIdGenerator.CreateLanguageId());
        }

        [Test]
        public void Validate_EmptyDraft_AllowsBlankContent()
        {
            var profile = new CareerProfile
            {
                Id = CareerProfileIdGenerator.CreateProfileId()
            };

            Assert.That(CareerProfileValidator.Validate(profile), Is.Empty);
        }

        [Test]
        public void Validate_DuplicateIdsAcrossSections_ReportsIssue()
        {
            const string duplicateId = "shared-id";
            var profile = new CareerProfile
            {
                Id = CareerProfileIdGenerator.CreateProfileId(),
                Links = { new CareerProfileLink { Id = duplicateId } },
                Skills = { new CareerSkill { Id = duplicateId } }
            };

            CareerProfileValidationIssue issue = CareerProfileValidator
                .Validate(profile)
                .Single(item => item.Error == CareerProfileValidationError.DuplicateItemId);

            Assert.That(issue.FieldPath, Is.EqualTo("skills[0].id"));
        }

        [Test]
        public void Validate_UpdatedBeforeCreated_ReportsIssue()
        {
            var profile = new CareerProfile
            {
                Id = CareerProfileIdGenerator.CreateProfileId(),
                CreatedAt = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero)
            };

            Assert.That(
                CareerProfileValidator.Validate(profile).Any(
                    item => item.Error == CareerProfileValidationError.UpdatedBeforeCreated),
                Is.True);
        }
    }
}
