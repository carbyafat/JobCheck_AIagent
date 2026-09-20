using System;
using System.IO;
using System.Linq;
using JobCheck.Domain;
using NUnit.Framework;

namespace JobCheck.Persistence.Tests
{
    public sealed class CareerProfileRepositoryTests
    {
        [Test]
        public void Load_MissingFile_ReturnsDraftWithoutWritingDisk()
        {
            string root = NewRoot();

            PersistenceStorageResult<CareerProfile> result = CareerProfileRepository.Load(root);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Id, Does.StartWith("prf_"));
            Assert.That(Directory.Exists(root), Is.False);
        }

        [Test]
        public void SaveThenLoad_RoundTripsEverySection()
        {
            string root = NewRoot();
            try
            {
                CareerProfile source = CompleteProfile();

                PersistenceStorageResult<CareerProfile> saved =
                    CareerProfileRepository.Save(root, source);
                PersistenceStorageResult<CareerProfile> loaded =
                    CareerProfileRepository.Load(root);

                Assert.That(saved.IsSuccess, Is.True, Issues(saved));
                Assert.That(loaded.IsSuccess, Is.True, Issues(loaded));
                Assert.That(loaded.Value.Summary, Is.EqualTo("Unity 與工具開發"));
                Assert.That(loaded.Value.Links.Single().Url, Is.EqualTo("https://example.com"));
                Assert.That(loaded.Value.Skills.Single().Name, Is.EqualTo("C#"));
                Assert.That(loaded.Value.Experiences.Single().Role, Is.EqualTo("工程師"));
                Assert.That(loaded.Value.Projects.Single().Technologies, Does.Contain("Unity"));
                Assert.That(loaded.Value.Educations.Single().Institution, Is.EqualTo("測試大學"));
                Assert.That(loaded.Value.Languages.Single().Level, Is.EqualTo("母語"));
                Assert.That(Directory.GetFiles(root, "*.tmp-*", SearchOption.AllDirectories), Is.Empty);
            }
            finally
            {
                Delete(root);
            }
        }

        [Test]
        public void Load_CorruptFile_FailsWithoutChangingContent()
        {
            string root = NewRoot();
            string path = CareerProfileRepository.GetProfilePath(root);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "{ broken }");

                PersistenceStorageResult<CareerProfile> result =
                    CareerProfileRepository.Load(root);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Issues.Select(item => item.Error),
                    Does.Contain(PersistenceStorageError.InvalidJson));
                Assert.That(File.ReadAllText(path), Is.EqualTo("{ broken }"));
            }
            finally
            {
                Delete(root);
            }
        }

        [Test]
        public void Save_InvalidProfile_DoesNotCreateFile()
        {
            string root = NewRoot();

            PersistenceStorageResult<CareerProfile> result =
                CareerProfileRepository.Save(root, new CareerProfile());

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(File.Exists(CareerProfileRepository.GetProfilePath(root)), Is.False);
        }

        [Test]
        public void Save_ExistingProfile_ReplacesAndReloadsContent()
        {
            string root = NewRoot();
            try
            {
                CareerProfile first = CompleteProfile();
                Assert.That(CareerProfileRepository.Save(root, first).IsSuccess, Is.True);
                first.Summary = "第二版內容";
                first.UpdatedAt = first.UpdatedAt.Value.AddMinutes(1);

                PersistenceStorageResult<CareerProfile> saved =
                    CareerProfileRepository.Save(root, first);

                Assert.That(saved.IsSuccess, Is.True, Issues(saved));
                Assert.That(saved.Value.Summary, Is.EqualTo("第二版內容"));
                Assert.That(Directory.GetFiles(root, "*.rollback-*", SearchOption.AllDirectories),
                    Is.Empty);
            }
            finally
            {
                Delete(root);
            }
        }

        private static CareerProfile CompleteProfile()
        {
            DateTimeOffset time = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.FromHours(8));
            return new CareerProfile
            {
                Id = CareerProfileIdGenerator.CreateProfileId(),
                Summary = "Unity 與工具開發",
                CreatedAt = time,
                UpdatedAt = time,
                Links = { new CareerProfileLink
                    { Id = CareerProfileIdGenerator.CreateLinkId(), Label = "作品集", Url = "https://example.com" } },
                Skills = { new CareerSkill
                    { Id = CareerProfileIdGenerator.CreateSkillId(), Name = "C#", Notes = "Unity" } },
                Experiences = { new CareerExperience
                    { Id = CareerProfileIdGenerator.CreateExperienceId(), Organization = "測試公司", Role = "工程師" } },
                Projects = { new CareerProject
                    { Id = CareerProfileIdGenerator.CreateProjectId(), Name = "JobCheck", Technologies = { "Unity" } } },
                Educations = { new CareerEducation
                    { Id = CareerProfileIdGenerator.CreateEducationId(), Institution = "測試大學" } },
                Languages = { new CareerLanguage
                    { Id = CareerProfileIdGenerator.CreateLanguageId(), Name = "中文", Level = "母語" } }
            };
        }

        private static string NewRoot()
        {
            return Path.Combine(Path.GetTempPath(), "JobCheckProfileTests_" + Guid.NewGuid().ToString("N"));
        }

        private static string Issues(PersistenceStorageResult<CareerProfile> result)
        {
            return string.Join(" | ", result.Issues.Select(item => item.Error + ":" + item.Message));
        }

        private static void Delete(string root)
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
