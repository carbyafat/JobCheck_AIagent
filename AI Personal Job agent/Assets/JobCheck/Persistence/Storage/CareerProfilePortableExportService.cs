using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JobCheck.Domain;

namespace JobCheck.Persistence
{
    public sealed class CareerProfilePortableExportSummary
    {
        public CareerProfilePortableExportSummary(string path, string profileId,
            DateTimeOffset exportedAt)
        {
            Path = path;
            ProfileId = profileId;
            ExportedAt = exportedAt;
        }

        public string Path { get; }
        public string ProfileId { get; }
        public DateTimeOffset ExportedAt { get; }
    }

    /// <summary>將已儲存的個人履歷輸出為獨立搬運檔；不修改來源或覆蓋目的檔。</summary>
    public static class CareerProfilePortableExportService
    {
        public static PersistenceStorageResult<CareerProfilePortableExportSummary> Export(
            string personalDataRoot,
            string destinationPath)
        {
            var issues = new List<PersistenceStorageIssue>();
            string sourceRoot;
            string outputPath;
            try
            {
                if (string.IsNullOrWhiteSpace(personalDataRoot))
                {
                    throw new ArgumentException("個人資料目錄不可空白。");
                }

                if (string.IsNullOrWhiteSpace(destinationPath)
                    || !destinationPath.EndsWith(
                        CareerProfilePortablePackageDto.FileExtension,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        "匯出路徑須為 .jobcheck-profile.json 檔案。");
                }

                sourceRoot = Path.GetFullPath(personalDataRoot).TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                outputPath = Path.GetFullPath(destinationPath);
                if (outputPath.StartsWith(sourceRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("履歷匯出檔不可放在個人資料目錄內。");
                }
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is NotSupportedException
                || exception is PathTooLongException)
            {
                issues.Add(Issue(PersistenceStorageError.IoFailure,
                    destinationPath, exception.Message));
                return Failure(issues);
            }

            string sourcePath = CareerProfileRepository.GetProfilePath(sourceRoot);
            if (!File.Exists(sourcePath))
            {
                issues.Add(Issue(PersistenceStorageError.DataRootNotFound,
                    sourcePath, "尚未儲存個人履歷，沒有可匯出的資料。"));
                return Failure(issues);
            }

            if (File.Exists(outputPath) || Directory.Exists(outputPath))
            {
                issues.Add(Issue(PersistenceStorageError.DestinationNotEmpty,
                    outputPath, "匯出檔已存在，不會覆蓋。"));
                return Failure(issues);
            }

            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(outputDirectory) || !Directory.Exists(outputDirectory))
            {
                issues.Add(Issue(PersistenceStorageError.IoFailure,
                    outputPath, "匯出資料夾不存在。"));
                return Failure(issues);
            }

            PersistenceStorageResult<CareerProfile> loaded =
                CareerProfileRepository.Load(sourceRoot);
            if (!loaded.IsSuccess)
            {
                return new PersistenceStorageResult<CareerProfilePortableExportSummary>(
                    null, loaded.Issues);
            }

            PersistenceConversionResult<CareerProfileDto> mapped =
                CareerProfileDtoMapper.ToDto(loaded.Value);
            foreach (PersistenceConversionIssue issue in mapped.Issues)
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.ConversionFailed,
                    sourcePath,
                    issue.FieldPath,
                    issue.Error.ToString()));
            }

            if (mapped.Value == null || issues.Count > 0)
            {
                return Failure(issues);
            }

            DateTimeOffset exportedAt = DateTimeOffset.UtcNow;
            var package = new CareerProfilePortablePackageDto
            {
                format_version = CareerProfilePortablePackageDto.CurrentFormatVersion,
                exported_at = exportedAt.ToString("o"),
                profile = mapped.Value
            };
            string temporaryPath = outputPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                package.content_sha256 = package.CalculateContentSha256();
                File.WriteAllText(temporaryPath,
                    PersistenceJsonSerializer.Serialize(package), new UTF8Encoding(false));
                File.Move(temporaryPath, outputPath);
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException || exception is NotSupportedException
                || exception is InvalidOperationException
                || exception is System.Runtime.Serialization.SerializationException)
            {
                issues.Add(Issue(PersistenceStorageError.IoFailure,
                    outputPath, exception.Message));
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch (Exception exception) when (
                        exception is IOException || exception is UnauthorizedAccessException)
                    {
                        issues.Add(Issue(PersistenceStorageError.IoFailure, temporaryPath,
                            "無法移除未完成的暫存檔：" + exception.Message));
                    }
                }
            }

            return new PersistenceStorageResult<CareerProfilePortableExportSummary>(
                issues.Count == 0
                    ? new CareerProfilePortableExportSummary(
                        outputPath, loaded.Value.Id, exportedAt)
                    : null,
                issues);
        }

        private static PersistenceStorageResult<CareerProfilePortableExportSummary> Failure(
            IEnumerable<PersistenceStorageIssue> issues)
        {
            return new PersistenceStorageResult<CareerProfilePortableExportSummary>(null, issues);
        }

        private static PersistenceStorageIssue Issue(
            PersistenceStorageError error, string path, string message)
        {
            return new PersistenceStorageIssue(error, path, null, message);
        }
    }
}
