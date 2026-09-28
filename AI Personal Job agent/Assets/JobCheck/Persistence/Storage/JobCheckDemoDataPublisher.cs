using System;
using System.IO;

namespace JobCheck.Persistence
{
    /// <summary>
    /// 將儲存庫 Demo 發佈到可攜式 Build。只替換 demo，不讀寫 personal。
    /// </summary>
    public static class JobCheckDemoDataPublisher
    {
        public static string Publish(string sourceRoot, string buildOutputDirectory)
        {
            string source = RequireFullPath(sourceRoot, nameof(sourceRoot));
            string output = RequireFullPath(
                buildOutputDirectory,
                nameof(buildOutputDirectory));

            ValidateSource(source);

            string container = Path.GetFullPath(Path.Combine(
                output,
                JobCheckDataPathResolver.PortableDataDirectoryName));
            string destination = Path.GetFullPath(Path.Combine(
                container,
                JobCheckDataPathResolver.PortableDemoDirectoryName));
            ValidateDestination(output, container, destination);
            ReplaceDemoDirectory(source, container, destination);
            return destination;
        }

        private static void ValidateSource(string source)
        {
            if (!Directory.Exists(source))
            {
                throw new DirectoryNotFoundException(
                    "找不到 JobCheck Demo 來源：" + source);
            }

            RequireSourceDirectory(source, JobCheckDataRepository.CompaniesDirectoryName);
            RequireSourceDirectory(source, JobCheckDataRepository.JobsDirectoryName);
            RequireSourceDirectory(source, JobCheckDataRepository.ApplicationsDirectoryName);
        }

        private static void RequireSourceDirectory(string source, string childName)
        {
            string path = Path.Combine(source, childName);
            if (!Directory.Exists(path))
            {
                throw new InvalidDataException("JobCheck Demo 缺少必要目錄：" + path);
            }
        }

        private static void ValidateDestination(
            string output,
            string container,
            string destination)
        {
            string expectedContainer = Path.GetFullPath(Path.Combine(
                output,
                JobCheckDataPathResolver.PortableDataDirectoryName));
            string expectedDestination = Path.GetFullPath(Path.Combine(
                expectedContainer,
                JobCheckDataPathResolver.PortableDemoDirectoryName));

            if (!string.Equals(container, expectedContainer, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    destination,
                    expectedDestination,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "拒絕寫入非預期的 Demo 目錄：" + destination);
            }
        }

        private static void ReplaceDemoDirectory(
            string source,
            string container,
            string destination)
        {
            Directory.CreateDirectory(container);

            string operationId = Guid.NewGuid().ToString("N");
            string staging = Path.Combine(container, ".demo-staging-" + operationId);
            string backup = Path.Combine(container, ".demo-backup-" + operationId);

            try
            {
                CopyDirectory(source, staging);

                if (Directory.Exists(destination))
                {
                    Directory.Move(destination, backup);
                }

                try
                {
                    Directory.Move(staging, destination);
                }
                catch
                {
                    if (Directory.Exists(backup) && !Directory.Exists(destination))
                    {
                        Directory.Move(backup, destination);
                    }

                    throw;
                }

                if (Directory.Exists(backup))
                {
                    Directory.Delete(backup, true);
                }
            }
            finally
            {
                if (Directory.Exists(staging))
                {
                    Directory.Delete(staging, true);
                }
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (string directory in Directory.GetDirectories(
                source,
                "*",
                SearchOption.AllDirectories))
            {
                string relative = directory.Substring(source.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }

            foreach (string file in Directory.GetFiles(
                source,
                "*",
                SearchOption.AllDirectories))
            {
                string relative = file.Substring(source.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string target = Path.Combine(destination, relative);
                string targetDirectory = Path.GetDirectoryName(target);
                if (string.IsNullOrWhiteSpace(targetDirectory))
                {
                    throw new InvalidOperationException("無法解析 Demo 檔案目錄：" + target);
                }

                Directory.CreateDirectory(targetDirectory);
                File.Copy(file, target, true);
            }
        }

        private static string RequireFullPath(string path, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("路徑不可空白。", parameterName);
            }

            return Path.GetFullPath(path);
        }
    }
}
