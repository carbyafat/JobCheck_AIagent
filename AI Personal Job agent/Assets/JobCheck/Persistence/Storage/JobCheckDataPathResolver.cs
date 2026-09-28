using System;
using System.IO;

namespace JobCheck.Persistence
{
    /// <summary>JobCheck 執行環境使用的 Demo 與個人資料位置。</summary>
    public sealed class JobCheckDataPathSet
    {
        public JobCheckDataPathSet(
            string dataContainerRoot,
            string demoRoot,
            string personalRoot)
        {
            DataContainerRoot = dataContainerRoot;
            DemoRoot = demoRoot;
            PersonalRoot = personalRoot;
        }

        public string DataContainerRoot { get; }
        public string DemoRoot { get; }
        public string PersonalRoot { get; }
    }

    /// <summary>
    /// 集中定義 Editor 與 Standalone Player 的資料位置。
    /// Editor 沿用儲存庫資料；Player 只使用執行檔旁的可攜式資料夾。
    /// </summary>
    public static class JobCheckDataPathResolver
    {
        public const string PortableDataDirectoryName = "JobCheckData";
        public const string PortableDemoDirectoryName = "demo";
        public const string PortablePersonalDirectoryName = "personal";
        public const string RepositoryDemoDirectoryName = "data";
        public const string RepositoryPersonalDirectoryName = "personal_data";

        public static JobCheckDataPathSet ResolveForEditor(string applicationDataPath)
        {
            string assetsRoot = RequireFullPath(applicationDataPath, nameof(applicationDataPath));
            string unityProjectRoot = RequireParent(assetsRoot, "Unity 專案根目錄");
            string repositoryRoot = RequireParent(unityProjectRoot, "JobCheck 儲存庫根目錄");

            return new JobCheckDataPathSet(
                repositoryRoot,
                Path.GetFullPath(Path.Combine(
                    repositoryRoot,
                    RepositoryDemoDirectoryName)),
                Path.GetFullPath(Path.Combine(
                    repositoryRoot,
                    RepositoryPersonalDirectoryName)));
        }

        public static JobCheckDataPathSet ResolveForPlayer(string applicationDataPath)
        {
            string playerDataRoot = RequireFullPath(applicationDataPath, nameof(applicationDataPath));
            string executableRoot = RequireParent(playerDataRoot, "執行檔目錄");
            string dataContainerRoot = Path.GetFullPath(Path.Combine(
                executableRoot,
                PortableDataDirectoryName));

            return new JobCheckDataPathSet(
                dataContainerRoot,
                Path.GetFullPath(Path.Combine(
                    dataContainerRoot,
                    PortableDemoDirectoryName)),
                Path.GetFullPath(Path.Combine(
                    dataContainerRoot,
                    PortablePersonalDirectoryName)));
        }

        private static string RequireFullPath(string path, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("路徑不可空白。", parameterName);
            }

            return Path.GetFullPath(path);
        }

        private static string RequireParent(string path, string description)
        {
            DirectoryInfo parent = Directory.GetParent(path);
            if (parent == null)
            {
                throw new InvalidOperationException("無法解析" + description + "：" + path);
            }

            return parent.FullName;
        }
    }
}
