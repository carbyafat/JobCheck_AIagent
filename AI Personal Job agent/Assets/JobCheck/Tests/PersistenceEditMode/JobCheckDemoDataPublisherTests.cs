using System;
using System.IO;
using JobCheck.Persistence;
using NUnit.Framework;

namespace JobCheck.Persistence.Tests
{
    public sealed class JobCheckDemoDataPublisherTests
    {
        private string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(
                Path.GetTempPath(),
                "JobCheckDemoPublisherTests_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void Publish_ReplacesDemoAndPreservesPersonal()
        {
            string source = CreateValidSource();
            string output = Path.Combine(root, "nested", "Build_0927");
            string container = Path.Combine(output, "JobCheckData");
            string oldDemo = Path.Combine(container, "demo");
            string personal = Path.Combine(container, "personal");
            Directory.CreateDirectory(oldDemo);
            Directory.CreateDirectory(personal);
            File.WriteAllText(Path.Combine(oldDemo, "old.txt"), "old");
            File.WriteAllText(Path.Combine(personal, "keep.txt"), "personal");

            string published = JobCheckDemoDataPublisher.Publish(source, output);

            Assert.AreEqual(Path.GetFullPath(oldDemo), published);
            Assert.IsFalse(File.Exists(Path.Combine(oldDemo, "old.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(
                oldDemo,
                JobCheckDataRepository.CompaniesDirectoryName,
                "company.json")));
            Assert.AreEqual(
                "personal",
                File.ReadAllText(Path.Combine(personal, "keep.txt")));
        }

        [Test]
        public void Publish_RejectsSourceWithoutRequiredDirectories()
        {
            string source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => JobCheckDemoDataPublisher.Publish(
                    source,
                    Path.Combine(root, "build")));

            StringAssert.Contains(
                JobCheckDataRepository.CompaniesDirectoryName,
                exception.Message);
        }

        private string CreateValidSource()
        {
            string source = Path.Combine(root, "source");
            string companies = Path.Combine(
                source,
                JobCheckDataRepository.CompaniesDirectoryName);
            Directory.CreateDirectory(companies);
            Directory.CreateDirectory(Path.Combine(
                source,
                JobCheckDataRepository.JobsDirectoryName));
            Directory.CreateDirectory(Path.Combine(
                source,
                JobCheckDataRepository.ApplicationsDirectoryName));
            File.WriteAllText(Path.Combine(companies, "company.json"), "{}");
            return source;
        }
    }
}
