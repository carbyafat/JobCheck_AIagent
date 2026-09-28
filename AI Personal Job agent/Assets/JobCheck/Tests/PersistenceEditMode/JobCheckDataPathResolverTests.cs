using System;
using System.IO;
using JobCheck.Persistence;
using NUnit.Framework;

namespace JobCheck.Persistence.Tests
{
    public sealed class JobCheckDataPathResolverTests
    {
        [Test]
        public void ResolveForEditor_UsesRepositoryDataDirectories()
        {
            string repositoryRoot = Path.Combine(Path.GetTempPath(), "JobCheckPathTests");
            string applicationDataPath = Path.Combine(
                repositoryRoot,
                "AI Personal Job agent",
                "Assets");

            JobCheckDataPathSet paths =
                JobCheckDataPathResolver.ResolveForEditor(applicationDataPath);

            Assert.AreEqual(
                Path.GetFullPath(repositoryRoot),
                paths.DataContainerRoot);
            Assert.AreEqual(
                Path.GetFullPath(Path.Combine(repositoryRoot, "data")),
                paths.DemoRoot);
            Assert.AreEqual(
                Path.GetFullPath(Path.Combine(repositoryRoot, "personal_data")),
                paths.PersonalRoot);
        }

        [Test]
        public void ResolveForPlayer_UsesPortableDirectoryBesideExecutable()
        {
            string buildRoot = Path.Combine(
                Path.GetTempPath(),
                "JobCheckPathTests",
                "nested",
                "Build_0927");
            string applicationDataPath = Path.Combine(
                buildRoot,
                "AI Personal Job agent_Data");

            JobCheckDataPathSet paths =
                JobCheckDataPathResolver.ResolveForPlayer(applicationDataPath);

            string portableRoot = Path.GetFullPath(Path.Combine(buildRoot, "JobCheckData"));
            Assert.AreEqual(portableRoot, paths.DataContainerRoot);
            Assert.AreEqual(
                Path.Combine(portableRoot, "demo"),
                paths.DemoRoot);
            Assert.AreEqual(
                Path.Combine(portableRoot, "personal"),
                paths.PersonalRoot);
        }

        [Test]
        public void ResolveForPlayer_DoesNotDependOnRepositoryDepth()
        {
            string applicationDataPath = Path.Combine(
                Path.GetTempPath(),
                "arbitrary",
                "portable",
                "JobCheck_Data");

            JobCheckDataPathSet paths =
                JobCheckDataPathResolver.ResolveForPlayer(applicationDataPath);

            Assert.AreEqual(
                Path.GetFullPath(Path.Combine(
                    Path.GetTempPath(),
                    "arbitrary",
                    "portable",
                    "JobCheckData",
                    "demo")),
                paths.DemoRoot);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ResolveForPlayer_RejectsMissingApplicationDataPath(string path)
        {
            Assert.Throws<ArgumentException>(
                () => JobCheckDataPathResolver.ResolveForPlayer(path));
        }
    }
}
