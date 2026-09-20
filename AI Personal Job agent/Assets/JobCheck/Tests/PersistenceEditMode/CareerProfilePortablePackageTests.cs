using JobCheck.Domain;
using NUnit.Framework;

namespace JobCheck.Persistence.Tests
{
    public sealed class CareerProfilePortablePackageTests
    {
        [Test]
        public void Checksum_IgnoresChecksumFieldButDetectsProfileChanges()
        {
            var package = new CareerProfilePortablePackageDto
            {
                format_version = CareerProfilePortablePackageDto.CurrentFormatVersion,
                exported_at = "2026-09-20T12:00:00.0000000+00:00",
                profile = CareerProfileDtoMapper.ToDto(new CareerProfile
                {
                    Id = "prf_test",
                    Summary = "原始內容"
                }).Value
            };

            string first = package.CalculateContentSha256();
            package.content_sha256 = "placeholder";
            string second = package.CalculateContentSha256();
            package.profile.summary = "修改後內容";
            string changed = package.CalculateContentSha256();

            Assert.That(second, Is.EqualTo(first));
            Assert.That(changed, Is.Not.EqualTo(first));
        }

        [Test]
        public void FileExtension_IsSeparateFromJobDataPackage()
        {
            Assert.That(CareerProfilePortablePackageDto.FileExtension,
                Is.EqualTo(".jobcheck-profile.json"));
            Assert.That(CareerProfilePortablePackageDto.FileExtension,
                Is.Not.EqualTo(JobCheckPortablePackageDto.FileExtension));
        }
    }
}
