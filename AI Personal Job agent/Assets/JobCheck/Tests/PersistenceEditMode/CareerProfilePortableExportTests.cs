using System;
using System.IO;
using System.Linq;
using JobCheck.Domain;
using NUnit.Framework;

namespace JobCheck.Persistence.Tests
{
    public sealed class CareerProfilePortableExportTests
    {
        [Test]
        public void Export_SavedProfile_ProducesIndependentVerifiedFile()
        {
            string root = NewRoot();
            try
            {
                string personal = Path.Combine(root, "personal");
                string output = Path.Combine(root, "profile.jobcheck-profile.json");
                CareerProfile profile = Profile("匯出內容");
                Assert.That(CareerProfileRepository.Save(personal, profile).IsSuccess, Is.True);
                string sourcePath = CareerProfileRepository.GetProfilePath(personal);
                string before = File.ReadAllText(sourcePath);

                PersistenceStorageResult<CareerProfilePortableExportSummary> result =
                    CareerProfilePortableExportService.Export(personal, output);

                Assert.That(result.IsSuccess, Is.True, Issues(result));
                Assert.That(result.Value.ProfileId, Is.EqualTo(profile.Id));
                Assert.That(File.ReadAllText(sourcePath), Is.EqualTo(before));
                Assert.That(PersistenceJsonSerializer.TryDeserialize(
                    File.ReadAllText(output),
                    out CareerProfilePortablePackageDto package,
                    out string error), Is.True, error);
                Assert.That(package.format_version,
                    Is.EqualTo(CareerProfilePortablePackageDto.CurrentFormatVersion));
                Assert.That(package.profile.summary, Is.EqualTo("匯出內容"));
                Assert.That(package.content_sha256,
                    Is.EqualTo(package.CalculateContentSha256()));
                Assert.That(Directory.GetFiles(root, "*.tmp-*", SearchOption.AllDirectories),
                    Is.Empty);
            }
            finally
            {
                Delete(root);
            }
        }

        [Test]
        public void Export_ExistingDestination_DoesNotOverwrite()
        {
            string root = NewRoot();
            try
            {
                string personal = Path.Combine(root, "personal");
                string output = Path.Combine(root, "profile.jobcheck-profile.json");
                Assert.That(CareerProfileRepository.Save(personal, Profile("內容")).IsSuccess,
                    Is.True);
                File.WriteAllText(output, "keep");

                PersistenceStorageResult<CareerProfilePortableExportSummary> result =
                    CareerProfilePortableExportService.Export(personal, output);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Issues.Select(item => item.Error),
                    Does.Contain(PersistenceStorageError.DestinationNotEmpty));
                Assert.That(File.ReadAllText(output), Is.EqualTo("keep"));
            }
            finally
            {
                Delete(root);
            }
        }

        [Test]
        public void Export_MissingStoredProfile_DoesNotCreateOutput()
        {
            string root = NewRoot();
            try
            {
                Directory.CreateDirectory(root);
                string output = Path.Combine(root, "profile.jobcheck-profile.json");

                PersistenceStorageResult<CareerProfilePortableExportSummary> result =
                    CareerProfilePortableExportService.Export(
                        Path.Combine(root, "personal"), output);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(File.Exists(output), Is.False);
            }
            finally
            {
                Delete(root);
            }
        }

        [Test]
        public void Export_InsidePersonalDataRoot_IsRejected()
        {
            string root = NewRoot();
            try
            {
                Assert.That(CareerProfileRepository.Save(root, Profile("內容")).IsSuccess,
                    Is.True);
                string output = Path.Combine(root, "profile.jobcheck-profile.json");

                PersistenceStorageResult<CareerProfilePortableExportSummary> result =
                    CareerProfilePortableExportService.Export(root, output);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(File.Exists(output), Is.False);
            }
            finally
            {
                Delete(root);
            }
        }

        internal static CareerProfile Profile(string summary)
        {
            DateTimeOffset time = new DateTimeOffset(
                2026, 9, 20, 12, 0, 0, TimeSpan.FromHours(8));
            return new CareerProfile
            {
                Id = CareerProfileIdGenerator.CreateProfileId(),
                Summary = summary,
                CreatedAt = time,
                UpdatedAt = time,
                Skills =
                {
                    new CareerSkill
                    {
                        Id = CareerProfileIdGenerator.CreateSkillId(),
                        Name = "Unity"
                    }
                }
            };
        }

        private static string NewRoot()
        {
            return Path.Combine(Path.GetTempPath(),
                "JobCheckProfileExportTests_" + Guid.NewGuid().ToString("N"));
        }

        private static string Issues(
            PersistenceStorageResult<CareerProfilePortableExportSummary> result)
        {
            return string.Join(" | ", result.Issues.Select(item => item.Message));
        }

        private static void Delete(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
}
