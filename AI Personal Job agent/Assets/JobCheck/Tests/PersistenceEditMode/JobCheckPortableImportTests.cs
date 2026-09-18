using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace JobCheck.Persistence.Tests
{
    public sealed class JobCheckPortableImportTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void Import_EmptyTarget_PublishesCompleteSnapshot(bool targetAlreadyExists)
        {
            string root = NewRoot();
            try
            {
                string packagePath = CreatePackage(root);
                string target = Path.Combine(root, "personal");
                Assert.That(JobCheckPortableImportService.CanImportIntoEmptyRoot(target), Is.True);
                if (targetAlreadyExists)
                    Assert.That(JobCheckDataRepository.WriteSnapshot(target,
                        new JobCheck.Domain.JobCheckDataSet(null, null, null, null)).IsSuccess,
                        Is.True);
                string originalPackage = File.ReadAllText(packagePath);

                PersistenceStorageResult<JobCheckPortableImportSummary> result =
                    JobCheckPortableImportService.Import(packagePath, target);

                Assert.That(result.IsSuccess, Is.True, FormatIssues(result));
                Assert.That(result.Value.CleanupWarning, Is.Null);
                Assert.That(result.Value.EventCount, Is.EqualTo(1));
                PersistenceStorageResult<JobCheck.Domain.JobCheckDataSet> loaded =
                    JobCheckDataRepository.Load(target);
                Assert.That(loaded.IsSuccess, Is.True);
                Assert.That(loaded.Value.Companies.Count, Is.EqualTo(1));
                Assert.That(loaded.Value.JobPostings.Count, Is.EqualTo(1));
                Assert.That(loaded.Value.Applications.Count, Is.EqualTo(1));
                Assert.That(loaded.Value.ApplicationEvents.Count, Is.EqualTo(1));
                Assert.That(File.ReadAllText(packagePath), Is.EqualTo(originalPackage));
                Assert.That(Directory.GetDirectories(root, "personal.*"), Is.Empty);
            }
            finally { Delete(root); }
        }

        [Test]
        public void Import_NonemptyTarget_RefusesWithoutChangingIt()
        {
            string root = NewRoot();
            try
            {
                string packagePath = CreatePackage(root);
                string target = Path.Combine(root, "personal");
                Assert.That(JobCheckDataRepository.WriteSnapshot(target,
                    JobCheckPortableExportTests.CompleteDataSet()).IsSuccess, Is.True);
                string existingFile = Path.Combine(target, "companies",
                    "cmp_0123456789abcdef0123456789abcdef.json");
                string before = File.ReadAllText(existingFile);
                Assert.That(JobCheckPortableImportService.CanImportIntoEmptyRoot(target), Is.False);

                PersistenceStorageResult<JobCheckPortableImportSummary> result =
                    JobCheckPortableImportService.Import(packagePath, target);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Issues.Any(issue => issue.Error ==
                    PersistenceStorageError.DestinationNotEmpty), Is.True);
                Assert.That(File.ReadAllText(existingFile), Is.EqualTo(before));
                Assert.That(Directory.GetDirectories(root, "personal.*"), Is.Empty);
            }
            finally { Delete(root); }
        }

        [Test]
        public void Import_InvalidPackage_DoesNotCreateTarget()
        {
            string root = NewRoot();
            try
            {
                string packagePath = CreatePackage(root);
                Assert.That(PersistenceJsonSerializer.TryDeserialize(File.ReadAllText(packagePath),
                    out JobCheckPortablePackageDto package, out string error), Is.True, error);
                package.content_sha256 = "corrupt";
                File.WriteAllText(packagePath, PersistenceJsonSerializer.Serialize(package));
                string target = Path.Combine(root, "personal");

                PersistenceStorageResult<JobCheckPortableImportSummary> result =
                    JobCheckPortableImportService.Import(packagePath, target);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(Directory.Exists(target), Is.False);
            }
            finally { Delete(root); }
        }

        private static string CreatePackage(string root)
        {
            Directory.CreateDirectory(root);
            string source = Path.Combine(root, "source");
            string packagePath = Path.Combine(root, "transfer.jobcheck.json");
            Assert.That(JobCheckDataRepository.WriteSnapshot(source,
                JobCheckPortableExportTests.CompleteDataSet()).IsSuccess, Is.True);
            Assert.That(JobCheckPortableExportService.Export(source, packagePath).IsSuccess, Is.True);
            return packagePath;
        }

        private static string NewRoot() => Path.Combine(
            Path.GetTempPath(), "JobCheckTransferImportTests_" + Guid.NewGuid().ToString("N"));
        private static void Delete(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        private static string FormatIssues(PersistenceStorageResult<JobCheckPortableImportSummary> result)
            => string.Join("; ", result.Issues.Select(issue => issue.Message));
    }
}
