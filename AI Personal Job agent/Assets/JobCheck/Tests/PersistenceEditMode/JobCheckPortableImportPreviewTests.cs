using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace JobCheck.Persistence.Tests
{
    public sealed class JobCheckPortableImportPreviewTests
    {
        [Test]
        public void Preview_ValidExport_RestoresCountsAndLeavesSourceUnchanged()
        {
            string root = NewRoot();
            try
            {
                Directory.CreateDirectory(root);
                string source = Path.Combine(root, "source");
                string packagePath = Path.Combine(root, "transfer.jobcheck.json");
                Assert.That(JobCheckDataRepository.WriteSnapshot(
                    source, JobCheckPortableExportTests.CompleteDataSet()).IsSuccess, Is.True);
                Assert.That(JobCheckPortableExportService.Export(source, packagePath).IsSuccess, Is.True);
                string before = File.ReadAllText(packagePath);

                PersistenceStorageResult<JobCheckPortableImportPreview> result =
                    JobCheckPortableImportService.Preview(packagePath);

                Assert.That(result.IsSuccess, Is.True, FormatIssues(result));
                Assert.That(result.Value.CompanyCount, Is.EqualTo(1));
                Assert.That(result.Value.JobCount, Is.EqualTo(1));
                Assert.That(result.Value.ApplicationCount, Is.EqualTo(1));
                Assert.That(result.Value.EventCount, Is.EqualTo(1));
                Assert.That(File.ReadAllText(packagePath), Is.EqualTo(before));
                Assert.That(Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).Length,
                    Is.EqualTo(4));
            }
            finally { Delete(root); }
        }

        [Test]
        public void Preview_TamperedContent_RejectsChecksum()
        {
            WithExport((root, packagePath) =>
            {
                JobCheckPortablePackageDto package = Read(packagePath);
                package.companies[0].name = "修改公司";
                File.WriteAllText(packagePath, PersistenceJsonSerializer.Serialize(package));
                PersistenceStorageResult<JobCheckPortableImportPreview> result =
                    JobCheckPortableImportService.Preview(packagePath);
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Issues.Any(item => item.FieldPath == "content_sha256"), Is.True);
            });
        }

        [Test]
        public void Preview_UnsupportedVersion_RejectsEvenWithValidChecksum()
        {
            WithExport((root, packagePath) =>
            {
                JobCheckPortablePackageDto package = Read(packagePath);
                package.format_version = "0.3";
                Save(packagePath, package);
                PersistenceStorageResult<JobCheckPortableImportPreview> result =
                    JobCheckPortableImportService.Preview(packagePath);
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Issues.Any(item => item.Error ==
                    PersistenceStorageError.UnsupportedSchemaVersion), Is.True);
            });
        }

        [Test]
        public void Preview_DuplicateCompanyId_RejectsEvenWithValidChecksum()
        {
            WithExport((root, packagePath) =>
            {
                JobCheckPortablePackageDto package = Read(packagePath);
                package.companies.Add(package.companies[0]);
                package.company_count++;
                Save(packagePath, package);
                PersistenceStorageResult<JobCheckPortableImportPreview> result =
                    JobCheckPortableImportService.Preview(packagePath);
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Issues.Any(item => item.Error ==
                    PersistenceStorageError.DataSetValidationFailed), Is.True);
            });
        }

        [Test]
        public void Preview_InvalidJson_RejectsWithoutWriting()
        {
            string root = NewRoot();
            try
            {
                Directory.CreateDirectory(root);
                string packagePath = Path.Combine(root, "broken.jobcheck.json");
                File.WriteAllText(packagePath, "{broken");
                PersistenceStorageResult<JobCheckPortableImportPreview> result =
                    JobCheckPortableImportService.Preview(packagePath);
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(Directory.GetFiles(root).Length, Is.EqualTo(1));
            }
            finally { Delete(root); }
        }

        private static void WithExport(Action<string, string> assertion)
        {
            string root = NewRoot();
            try
            {
                Directory.CreateDirectory(root);
                string source = Path.Combine(root, "source");
                string packagePath = Path.Combine(root, "transfer.jobcheck.json");
                Assert.That(JobCheckDataRepository.WriteSnapshot(
                    source, JobCheckPortableExportTests.CompleteDataSet()).IsSuccess, Is.True);
                Assert.That(JobCheckPortableExportService.Export(source, packagePath).IsSuccess, Is.True);
                assertion(root, packagePath);
            }
            finally { Delete(root); }
        }

        private static JobCheckPortablePackageDto Read(string path)
        {
            Assert.That(PersistenceJsonSerializer.TryDeserialize(File.ReadAllText(path),
                out JobCheckPortablePackageDto package, out string error), Is.True, error);
            return package;
        }

        private static void Save(string path, JobCheckPortablePackageDto package)
        {
            package.content_sha256 = package.CalculateContentSha256();
            File.WriteAllText(path, PersistenceJsonSerializer.Serialize(package));
        }

        private static string NewRoot() => Path.Combine(
            Path.GetTempPath(), "JobCheckTransferPreviewTests_" + Guid.NewGuid().ToString("N"));
        private static void Delete(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        private static string FormatIssues(PersistenceStorageResult<JobCheckPortableImportPreview> result)
            => string.Join("; ", result.Issues.Select(item => item.Message));
    }
}
