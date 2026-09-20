using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace JobCheck.Persistence.Tests
{
    public sealed class CareerProfilePortableImportTests
    {
        [Test]
        public void Preview_ValidPackageIntoEmptyRoot_ReportsCreate()
        {
            string root = NewRoot();
            try
            {
                string package = ExportPackage(root, "來源內容");

                PersistenceStorageResult<CareerProfilePortableImportPreview> result =
                    CareerProfilePortableImportService.Preview(
                        package, Path.Combine(root, "target"));

                Assert.That(result.IsSuccess, Is.True, Issues(result));
                Assert.That(result.Value.Disposition,
                    Is.EqualTo(CareerProfileImportDisposition.Create));
                Assert.That(result.Value.Profile.Summary, Is.EqualTo("來源內容"));
            }
            finally
            {
                Delete(root);
            }
        }

        [Test]
        public void Preview_ModifiedPackage_RejectsChecksumMismatch()
        {
            string root = NewRoot();
            try
            {
                string packagePath = ExportPackage(root, "來源內容");
                string json = File.ReadAllText(packagePath).Replace("來源內容", "遭到修改");
                File.WriteAllText(packagePath, json);

                PersistenceStorageResult<CareerProfilePortableImportPreview> result =
                    CareerProfilePortableImportService.Preview(
                        packagePath, Path.Combine(root, "target"));

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Issues.Any(item =>
                    item.FieldPath == "content_sha256"), Is.True);
            }
            finally
            {
                Delete(root);
            }
        }

        [Test]
        public void Preview_UnknownPackageVersion_IsRejected()
        {
            string root = NewRoot();
            try
            {
                string packagePath = ExportPackage(root, "來源內容");
                CareerProfilePortablePackageDto package = ReadPackage(packagePath);
                package.format_version = "999";
                package.content_sha256 = package.CalculateContentSha256();
                File.WriteAllText(packagePath, PersistenceJsonSerializer.Serialize(package));

                PersistenceStorageResult<CareerProfilePortableImportPreview> result =
                    CareerProfilePortableImportService.Preview(
                        packagePath, Path.Combine(root, "target"));

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Issues.Select(item => item.Error),
                    Does.Contain(PersistenceStorageError.UnsupportedSchemaVersion));
            }
            finally
            {
                Delete(root);
            }
        }

        [Test]
        public void Import_EmptyRoot_CreatesAndReloadsProfile()
        {
            string root = NewRoot();
            try
            {
                string package = ExportPackage(root, "來源內容");
                string target = Path.Combine(root, "target");

                PersistenceStorageResult<CareerProfilePortableImportSummary> result =
                    CareerProfilePortableImportService.Import(package, target, false);

                Assert.That(result.IsSuccess, Is.True, Issues(result));
                Assert.That(result.Value.Disposition,
                    Is.EqualTo(CareerProfileImportDisposition.Create));
                Assert.That(CareerProfileRepository.Load(target).Value.Summary,
                    Is.EqualTo("來源內容"));
            }
            finally
            {
                Delete(root);
            }
        }

        [Test]
        public void Import_IdenticalProfile_SkipsWrite()
        {
            string root = NewRoot();
            try
            {
                string source = Path.Combine(root, "source");
                string target = Path.Combine(root, "target");
                var profile = CareerProfilePortableExportTests.Profile("相同內容");
                Assert.That(CareerProfileRepository.Save(source, profile).IsSuccess, Is.True);
                Assert.That(CareerProfileRepository.Save(target, profile).IsSuccess, Is.True);
                string package = Path.Combine(root, "profile.jobcheck-profile.json");
                Assert.That(CareerProfilePortableExportService.Export(source, package).IsSuccess,
                    Is.True);
                string targetPath = CareerProfileRepository.GetProfilePath(target);
                DateTime before = File.GetLastWriteTimeUtc(targetPath);

                PersistenceStorageResult<CareerProfilePortableImportSummary> result =
                    CareerProfilePortableImportService.Import(package, target, false);

                Assert.That(result.IsSuccess, Is.True, Issues(result));
                Assert.That(result.Value.Disposition,
                    Is.EqualTo(CareerProfileImportDisposition.Identical));
                Assert.That(result.Value.Changed, Is.False);
                Assert.That(File.GetLastWriteTimeUtc(targetPath), Is.EqualTo(before));
            }
            finally
            {
                Delete(root);
            }
        }

        [Test]
        public void Import_DifferentProfileWithoutConfirmation_LeavesLocalUntouched()
        {
            string root = NewRoot();
            try
            {
                string package = ExportPackage(root, "來源內容");
                string target = Path.Combine(root, "target");
                Assert.That(CareerProfileRepository.Save(target,
                    CareerProfilePortableExportTests.Profile("本機內容")).IsSuccess, Is.True);
                string targetPath = CareerProfileRepository.GetProfilePath(target);
                string before = File.ReadAllText(targetPath);

                PersistenceStorageResult<CareerProfilePortableImportSummary> result =
                    CareerProfilePortableImportService.Import(package, target, false);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Issues.Select(item => item.Error),
                    Does.Contain(PersistenceStorageError.DestinationNotEmpty));
                Assert.That(File.ReadAllText(targetPath), Is.EqualTo(before));
            }
            finally
            {
                Delete(root);
            }
        }

        [Test]
        public void Import_ConfirmedReplacement_BacksUpThenReplaces()
        {
            string root = NewRoot();
            try
            {
                string package = ExportPackage(root, "來源內容");
                string target = Path.Combine(root, "target");
                Assert.That(CareerProfileRepository.Save(target,
                    CareerProfilePortableExportTests.Profile("本機內容")).IsSuccess, Is.True);

                PersistenceStorageResult<CareerProfilePortableImportSummary> result =
                    CareerProfilePortableImportService.Import(package, target, true);

                Assert.That(result.IsSuccess, Is.True, Issues(result));
                Assert.That(result.Value.Disposition,
                    Is.EqualTo(CareerProfileImportDisposition.ReplaceRequired));
                Assert.That(File.Exists(result.Value.BackupPath), Is.True);
                Assert.That(CareerProfileRepository.Load(target).Value.Summary,
                    Is.EqualTo("來源內容"));
                Assert.That(File.ReadAllText(result.Value.BackupPath),
                    Does.Contain("本機內容"));
            }
            finally
            {
                Delete(root);
            }
        }

        private static string ExportPackage(string root, string summary)
        {
            Directory.CreateDirectory(root);
            string source = Path.Combine(root, "source");
            Assert.That(CareerProfileRepository.Save(source,
                CareerProfilePortableExportTests.Profile(summary)).IsSuccess, Is.True);
            string package = Path.Combine(root, "profile.jobcheck-profile.json");
            Assert.That(CareerProfilePortableExportService.Export(source, package).IsSuccess,
                Is.True);
            return package;
        }

        private static CareerProfilePortablePackageDto ReadPackage(string path)
        {
            Assert.That(PersistenceJsonSerializer.TryDeserialize(
                File.ReadAllText(path), out CareerProfilePortablePackageDto package,
                out string error), Is.True, error);
            return package;
        }

        private static string NewRoot()
        {
            return Path.Combine(Path.GetTempPath(),
                "JobCheckProfileImportTests_" + Guid.NewGuid().ToString("N"));
        }

        private static string Issues<T>(PersistenceStorageResult<T> result)
            where T : class
        {
            return string.Join(" | ", result.Issues.Select(item =>
                item.Error + ":" + item.FilePath + ":" + item.Message));
        }

        private static void Delete(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
}
