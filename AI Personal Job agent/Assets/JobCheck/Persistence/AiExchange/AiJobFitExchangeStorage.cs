using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace JobCheck.Persistence
{
    public sealed class AiJobFitRequestExportSummary
    {
        public AiJobFitRequestExportSummary(string path, string requestId)
        {
            Path = path;
            RequestId = requestId;
        }

        public string Path { get; }
        public string RequestId { get; }
    }

    public sealed class AiJobFitAnalysisSaveSummary
    {
        public AiJobFitAnalysisSaveSummary(string path, string requestId, string jobId)
        {
            Path = path;
            RequestId = requestId;
            JobId = jobId;
        }

        public string Path { get; }
        public string RequestId { get; }
        public string JobId { get; }
    }

    /// <summary>處理使用者可攜出的 AI request 檔，以及外部 result 檔的唯讀載入。</summary>
    public static class AiJobFitExchangeFileService
    {
        public static PersistenceStorageResult<AiJobFitRequestExportSummary> ExportRequest(
            AiJobFitRequestPackageDto package,
            string destinationPath)
        {
            var issues = new List<PersistenceStorageIssue>();
            if (package == null)
                return Failure<AiJobFitRequestExportSummary>(destinationPath, "沒有可匯出的 AI 分析需求。");
            if (!string.Equals(package.format, AiExchangeContract.Format, StringComparison.Ordinal)
                || !string.Equals(package.message_type,
                    AiExchangeContract.RequestMessageType,
                    StringComparison.Ordinal)
                || !string.Equals(package.task_type,
                    AiExchangeContract.JobFitAnalysisTaskType,
                    StringComparison.Ordinal))
            {
                return Failure<AiJobFitRequestExportSummary>(destinationPath, "AI 分析需求封包識別不正確。");
            }

            string outputPath;
            try
            {
                if (string.IsNullOrWhiteSpace(destinationPath)
                    || !destinationPath.EndsWith(
                        AiExchangeContract.RequestFileExtension,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("匯出路徑須為 .jobcheck-ai-request.json 檔案。");
                }

                outputPath = Path.GetFullPath(destinationPath);
                string directory = Path.GetDirectoryName(outputPath);
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                    throw new DirectoryNotFoundException("AI 匯出資料夾不存在。");
                if (File.Exists(outputPath) || Directory.Exists(outputPath))
                    return Failure<AiJobFitRequestExportSummary>(outputPath, "匯出檔已存在，不會覆蓋。");
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is NotSupportedException
                || exception is PathTooLongException || exception is DirectoryNotFoundException)
            {
                return Failure<AiJobFitRequestExportSummary>(destinationPath, exception.Message);
            }

            string temporaryPath = outputPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                WriteNewJson(package, temporaryPath, outputPath);
                return new PersistenceStorageResult<AiJobFitRequestExportSummary>(
                    new AiJobFitRequestExportSummary(outputPath, package.request_id),
                    Array.Empty<PersistenceStorageIssue>());
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                issues.Add(Issue(outputPath, exception.Message));
                return new PersistenceStorageResult<AiJobFitRequestExportSummary>(null, issues);
            }
            finally
            {
                TryDeleteTemporary(temporaryPath, issues);
            }
        }

        public static PersistenceStorageResult<string> ReadResultText(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)
                    || !path.EndsWith(
                        AiExchangeContract.ResultFileExtension,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Failure<string>(path, "請選擇 .jobcheck-ai-result.json 檔案。");
                }

                string fullPath = Path.GetFullPath(path);
                if (!File.Exists(fullPath))
                    return Failure<string>(fullPath, "找不到 AI 分析結果檔。");
                var info = new FileInfo(fullPath);
                if (info.Length > AiJobFitResultValidator.MaximumJsonCharacters * 4L)
                    return Failure<string>(fullPath, "AI 分析結果檔過大，已拒絕讀取。");
                string json = File.ReadAllText(fullPath, Encoding.UTF8);
                return new PersistenceStorageResult<string>(
                    json,
                    Array.Empty<PersistenceStorageIssue>());
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return Failure<string>(path, exception.Message);
            }
        }

        private static void WriteNewJson<T>(T value, string temporaryPath, string outputPath)
            where T : class
        {
            File.WriteAllText(
                temporaryPath,
                PersistenceJsonSerializer.Serialize(value),
                new UTF8Encoding(false));
            File.Move(temporaryPath, outputPath);
        }

        internal static bool IsStorageException(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException || exception is NotSupportedException
                || exception is InvalidOperationException
                || exception is System.Runtime.Serialization.SerializationException;
        }

        internal static void TryDeleteTemporary(
            string temporaryPath,
            ICollection<PersistenceStorageIssue> issues)
        {
            if (!File.Exists(temporaryPath)) return;
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException)
            {
                issues?.Add(Issue(temporaryPath, "無法移除未完成的暫存檔：" + exception.Message));
            }
        }

        internal static PersistenceStorageIssue Issue(string path, string message)
        {
            return new PersistenceStorageIssue(
                PersistenceStorageError.IoFailure,
                path,
                null,
                message);
        }

        private static PersistenceStorageResult<T> Failure<T>(string path, string message)
            where T : class
        {
            return new PersistenceStorageResult<T>(null, new[] { Issue(path, message) });
        }
    }

    /// <summary>
    /// 在個人資料區另存 AI request 登記與分析結果；不修改職缺、履歷或應徵母資料。
    /// </summary>
    public static class AiJobFitAnalysisRepository
    {
        private const string ExchangeDirectoryName = "ai_exchange";
        private const string RequestDirectoryName = "requests";
        private const string AnalysisDirectoryName = "ai_analysis";

        public static PersistenceStorageResult<AiJobFitRequestPackageDto> RegisterRequest(
            string personalDataRoot,
            AiJobFitRequestPackageDto package)
        {
            if (package == null || !Guid.TryParse(package.request_id, out _))
                return Failure<AiJobFitRequestPackageDto>(personalDataRoot, "AI request_id 無效。");
            if (string.IsNullOrWhiteSpace(package.job_id))
                return Failure<AiJobFitRequestPackageDto>(personalDataRoot, "AI request 缺少 job_id。");

            string path;
            try
            {
                string root = ResolveRoot(personalDataRoot);
                string directory = Path.Combine(root, ExchangeDirectoryName, RequestDirectoryName);
                Directory.CreateDirectory(directory);
                path = Path.Combine(directory,
                    package.request_id + AiExchangeContract.RequestFileExtension);
                if (File.Exists(path))
                    return Failure<AiJobFitRequestPackageDto>(path, "相同 request_id 已經登記，不會覆蓋。");
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException || exception is NotSupportedException
                || exception is PathTooLongException)
            {
                return Failure<AiJobFitRequestPackageDto>(personalDataRoot, exception.Message);
            }

            var issues = new List<PersistenceStorageIssue>();
            string temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporaryPath,
                    PersistenceJsonSerializer.Serialize(package),
                    new UTF8Encoding(false));
                File.Move(temporaryPath, path);
                return new PersistenceStorageResult<AiJobFitRequestPackageDto>(
                    package,
                    Array.Empty<PersistenceStorageIssue>());
            }
            catch (Exception exception) when (
                AiJobFitExchangeFileService.IsStorageException(exception))
            {
                issues.Add(AiJobFitExchangeFileService.Issue(path, exception.Message));
                return new PersistenceStorageResult<AiJobFitRequestPackageDto>(null, issues);
            }
            finally
            {
                AiJobFitExchangeFileService.TryDeleteTemporary(temporaryPath, issues);
            }
        }

        public static PersistenceStorageResult<AiJobFitRequestPackageDto> LoadRequest(
            string personalDataRoot,
            string requestId)
        {
            if (!Guid.TryParse(requestId, out _))
                return Failure<AiJobFitRequestPackageDto>(personalDataRoot, "request_id 不是有效 UUID。");
            try
            {
                string path = Path.Combine(
                    ResolveRoot(personalDataRoot),
                    ExchangeDirectoryName,
                    RequestDirectoryName,
                    requestId + AiExchangeContract.RequestFileExtension);
                if (!File.Exists(path))
                    return Failure<AiJobFitRequestPackageDto>(path, "找不到對應的 AI 分析需求登記。" );
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (!PersistenceJsonSerializer.TryDeserialize(
                    json,
                    out AiJobFitRequestPackageDto value,
                    out string error))
                {
                    return Failure<AiJobFitRequestPackageDto>(path, "AI 分析需求登記損壞：" + error);
                }

                return new PersistenceStorageResult<AiJobFitRequestPackageDto>(
                    value,
                    Array.Empty<PersistenceStorageIssue>());
            }
            catch (Exception exception) when (
                AiJobFitExchangeFileService.IsStorageException(exception))
            {
                return Failure<AiJobFitRequestPackageDto>(personalDataRoot, exception.Message);
            }
        }

        public static PersistenceStorageResult<AiJobFitAnalysisSaveSummary> SaveResult(
            string personalDataRoot,
            AiJobFitResultPackageDto result)
        {
            if (result == null)
                return Failure<AiJobFitAnalysisSaveSummary>(personalDataRoot, "沒有可儲存的 AI 分析結果。" );
            PersistenceStorageResult<AiJobFitRequestPackageDto> request =
                LoadRequest(personalDataRoot, result.request_id);
            if (!request.IsSuccess)
                return new PersistenceStorageResult<AiJobFitAnalysisSaveSummary>(null, request.Issues);

            AiJobFitResultValidationResult validated =
                AiJobFitResultValidator.ParseAndValidate(
                    PersistenceJsonSerializer.Serialize(result),
                    request.Value.request_id,
                    request.Value.job_id);
            if (!validated.IsSuccess)
            {
                return Failure<AiJobFitAnalysisSaveSummary>(
                    personalDataRoot,
                    string.Join("\n", validated.Issues));
            }

            string outputPath;
            try
            {
                string root = ResolveRoot(personalDataRoot);
                string jobDirectory = Path.Combine(
                    root,
                    AnalysisDirectoryName,
                    SafeJobDirectory(result.job_id));
                Directory.CreateDirectory(jobDirectory);
                outputPath = Path.Combine(jobDirectory,
                    result.request_id + AiExchangeContract.ResultFileExtension);
                if (File.Exists(outputPath))
                    return Failure<AiJobFitAnalysisSaveSummary>(outputPath, "這次 AI 分析已經儲存，不會覆蓋。" );
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException || exception is NotSupportedException
                || exception is PathTooLongException)
            {
                return Failure<AiJobFitAnalysisSaveSummary>(personalDataRoot, exception.Message);
            }

            var issues = new List<PersistenceStorageIssue>();
            string temporaryPath = outputPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                validated.Value.imported_at = DateTimeOffset.UtcNow.ToString("o");
                File.WriteAllText(temporaryPath,
                    PersistenceJsonSerializer.Serialize(validated.Value),
                    new UTF8Encoding(false));
                File.Move(temporaryPath, outputPath);
                return new PersistenceStorageResult<AiJobFitAnalysisSaveSummary>(
                    new AiJobFitAnalysisSaveSummary(
                        outputPath,
                        validated.Value.request_id,
                        validated.Value.job_id),
                    Array.Empty<PersistenceStorageIssue>());
            }
            catch (Exception exception) when (
                AiJobFitExchangeFileService.IsStorageException(exception))
            {
                issues.Add(AiJobFitExchangeFileService.Issue(outputPath, exception.Message));
                return new PersistenceStorageResult<AiJobFitAnalysisSaveSummary>(null, issues);
            }
            finally
            {
                AiJobFitExchangeFileService.TryDeleteTemporary(temporaryPath, issues);
            }
        }

        public static PersistenceStorageResult<AiJobFitResultPackageDto> LoadLatestResult(
            string personalDataRoot,
            string jobId)
        {
            try
            {
                string directory = Path.Combine(
                    ResolveRoot(personalDataRoot),
                    AnalysisDirectoryName,
                    SafeJobDirectory(jobId));
                if (!Directory.Exists(directory))
                    return Failure<AiJobFitResultPackageDto>(directory, "目前職缺尚無已儲存的 AI 分析。" );

                foreach (string path in Directory.GetFiles(
                    directory,
                    "*" + AiExchangeContract.ResultFileExtension)
                    .OrderByDescending(File.GetLastWriteTimeUtc))
                {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    AiJobFitResultValidationResult validation =
                        AiJobFitResultValidator.ParseAndValidate(json, null, jobId);
                    if (validation.IsSuccess)
                    {
                        return new PersistenceStorageResult<AiJobFitResultPackageDto>(
                            validation.Value,
                            Array.Empty<PersistenceStorageIssue>());
                    }
                }

                return Failure<AiJobFitResultPackageDto>(directory, "AI 分析檔存在，但沒有可讀取的有效結果。" );
            }
            catch (Exception exception) when (
                AiJobFitExchangeFileService.IsStorageException(exception))
            {
                return Failure<AiJobFitResultPackageDto>(personalDataRoot, exception.Message);
            }
        }

        private static string ResolveRoot(string personalDataRoot)
        {
            if (string.IsNullOrWhiteSpace(personalDataRoot))
                throw new ArgumentException("個人資料目錄不可空白。" );
            string root = Path.GetFullPath(personalDataRoot).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException("找不到個人資料目錄：" + root);
            return root;
        }

        private static string SafeJobDirectory(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId))
                throw new ArgumentException("job_id 不可空白。" );
            string value = jobId.Trim();
            if (value != "." && value != ".."
                && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && value.IndexOf(Path.DirectorySeparatorChar) < 0
                && value.IndexOf(Path.AltDirectorySeparatorChar) < 0)
            {
                return value;
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                return "job_" + string.Concat(hash.Take(12).Select(item => item.ToString("x2")));
            }
        }

        private static PersistenceStorageResult<T> Failure<T>(string path, string message)
            where T : class
        {
            return new PersistenceStorageResult<T>(null, new[]
            {
                new PersistenceStorageIssue(
                    PersistenceStorageError.IoFailure,
                    path,
                    null,
                    message)
            });
        }
    }
}
