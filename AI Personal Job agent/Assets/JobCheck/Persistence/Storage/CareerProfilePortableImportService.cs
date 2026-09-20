using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using JobCheck.Domain;

namespace JobCheck.Persistence
{
    public enum CareerProfileImportDisposition
    {
        Create,
        Identical,
        ReplaceRequired
    }

    public sealed class CareerProfilePortableImportPreview
    {
        internal CareerProfilePortableImportPreview(
            string path,
            DateTimeOffset exportedAt,
            CareerProfile profile,
            CareerProfileImportDisposition disposition)
        {
            Path = path;
            ExportedAt = exportedAt;
            Profile = profile;
            Disposition = disposition;
        }

        public string Path { get; }
        public DateTimeOffset ExportedAt { get; }
        public CareerProfile Profile { get; }
        public CareerProfileImportDisposition Disposition { get; }
    }

    public sealed class CareerProfilePortableImportSummary
    {
        internal CareerProfilePortableImportSummary(
            CareerProfile profile,
            CareerProfileImportDisposition disposition,
            string backupPath)
        {
            Profile = profile;
            Disposition = disposition;
            BackupPath = backupPath;
        }

        public CareerProfile Profile { get; }
        public CareerProfileImportDisposition Disposition { get; }
        public string BackupPath { get; }
        public bool Changed => Disposition != CareerProfileImportDisposition.Identical;
    }

    /// <summary>驗證、預覽並安全套用個人履歷搬運檔。</summary>
    public static class CareerProfilePortableImportService
    {
        private const long MaximumPackageBytes = 8L * 1024 * 1024;
        private const string BackupDirectoryName = "profile_backups";

        public static PersistenceStorageResult<CareerProfilePortableImportPreview> Preview(
            string packagePath,
            string personalDataRoot)
        {
            var issues = new List<PersistenceStorageIssue>();
            string fullPackagePath = ResolvePackagePath(packagePath, issues);
            string fullRoot = ResolvePersonalRoot(personalDataRoot, issues);
            if (issues.Count > 0)
            {
                return PreviewFailure(issues);
            }

            CareerProfilePortablePackageDto package = ReadAndValidatePackage(
                fullPackagePath, issues, out DateTimeOffset exportedAt,
                out CareerProfile incoming);
            if (package == null || issues.Count > 0)
            {
                return PreviewFailure(issues);
            }

            string profilePath = CareerProfileRepository.GetProfilePath(fullRoot);
            CareerProfileImportDisposition disposition;
            if (!File.Exists(profilePath))
            {
                disposition = CareerProfileImportDisposition.Create;
            }
            else
            {
                PersistenceStorageResult<CareerProfile> existing =
                    CareerProfileRepository.Load(fullRoot);
                if (!existing.IsSuccess)
                {
                    return new PersistenceStorageResult<CareerProfilePortableImportPreview>(
                        null, existing.Issues);
                }

                disposition = SameProfile(existing.Value, incoming)
                    ? CareerProfileImportDisposition.Identical
                    : CareerProfileImportDisposition.ReplaceRequired;
            }

            return new PersistenceStorageResult<CareerProfilePortableImportPreview>(
                new CareerProfilePortableImportPreview(
                    fullPackagePath, exportedAt, incoming, disposition),
                Array.Empty<PersistenceStorageIssue>());
        }

        /// <summary>
        /// 套用前重新完整預覽。既有內容不同時必須明確允許取代，並先保存原始 JSON。
        /// </summary>
        public static PersistenceStorageResult<CareerProfilePortableImportSummary> Import(
            string packagePath,
            string personalDataRoot,
            bool allowReplace)
        {
            PersistenceStorageResult<CareerProfilePortableImportPreview> preview =
                Preview(packagePath, personalDataRoot);
            if (!preview.IsSuccess)
            {
                return new PersistenceStorageResult<CareerProfilePortableImportSummary>(
                    null, preview.Issues);
            }

            CareerProfilePortableImportPreview plan = preview.Value;
            if (plan.Disposition == CareerProfileImportDisposition.Identical)
            {
                return Success(plan.Profile, plan.Disposition, null);
            }

            string fullRoot = Path.GetFullPath(personalDataRoot);
            string profilePath = CareerProfileRepository.GetProfilePath(fullRoot);
            if (plan.Disposition == CareerProfileImportDisposition.ReplaceRequired
                && !allowReplace)
            {
                return ImportFailure(new PersistenceStorageIssue(
                    PersistenceStorageError.DestinationNotEmpty,
                    profilePath,
                    null,
                    "本機已有不同的履歷；必須明確確認取代才會匯入。"));
            }

            if (plan.Disposition == CareerProfileImportDisposition.Create
                && File.Exists(profilePath))
            {
                return ImportFailure(new PersistenceStorageIssue(
                    PersistenceStorageError.DestinationNotEmpty,
                    profilePath,
                    null,
                    "預覽後本機已建立履歷，請重新預覽。"));
            }

            string backupPath = null;
            if (plan.Disposition == CareerProfileImportDisposition.ReplaceRequired)
            {
                PersistenceStorageResult<CareerProfilePortableImportSummary> backupFailure =
                    TryCreateBackup(profilePath, out backupPath);
                if (backupFailure != null)
                {
                    return backupFailure;
                }
            }

            PersistenceStorageResult<CareerProfile> saved =
                CareerProfileRepository.Save(fullRoot, plan.Profile);
            if (!saved.IsSuccess)
            {
                return new PersistenceStorageResult<CareerProfilePortableImportSummary>(
                    null, saved.Issues);
            }

            if (!SameProfile(saved.Value, plan.Profile))
            {
                if (!string.IsNullOrEmpty(backupPath))
                {
                    TryRestoreBackup(backupPath, profilePath);
                }

                return ImportFailure(new PersistenceStorageIssue(
                    PersistenceStorageError.DataSetValidationFailed,
                    profilePath,
                    null,
                    "匯入後重新載入核對失敗；已嘗試保留原始履歷。"));
            }

            return Success(saved.Value, plan.Disposition, backupPath);
        }

        private static string ResolvePackagePath(
            string packagePath,
            ICollection<PersistenceStorageIssue> issues)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(packagePath)
                    || !packagePath.EndsWith(
                        CareerProfilePortablePackageDto.FileExtension,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        "請選擇 .jobcheck-profile.json 履歷搬運檔。");
                }

                string fullPath = Path.GetFullPath(packagePath);
                var info = new FileInfo(fullPath);
                if (!info.Exists || info.Length == 0 || info.Length > MaximumPackageBytes)
                {
                    throw new IOException("履歷搬運檔不存在、為空，或超過 8 MB。");
                }

                return fullPath;
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is NotSupportedException
                || exception is PathTooLongException || exception is IOException
                || exception is UnauthorizedAccessException)
            {
                issues.Add(Issue(PersistenceStorageError.IoFailure,
                    packagePath, null, exception.Message));
                return null;
            }
        }

        private static string ResolvePersonalRoot(
            string personalDataRoot,
            ICollection<PersistenceStorageIssue> issues)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(personalDataRoot))
                {
                    throw new ArgumentException("個人資料目錄不可空白。");
                }

                string root = Path.GetFullPath(personalDataRoot);
                if (string.Equals(root, Path.GetPathRoot(root),
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("不可使用磁碟根目錄作為個人資料區。");
                }

                return root;
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is NotSupportedException
                || exception is PathTooLongException)
            {
                issues.Add(Issue(PersistenceStorageError.IoFailure,
                    personalDataRoot, null, exception.Message));
                return null;
            }
        }

        private static CareerProfilePortablePackageDto ReadAndValidatePackage(
            string fullPath,
            ICollection<PersistenceStorageIssue> issues,
            out DateTimeOffset exportedAt,
            out CareerProfile profile)
        {
            exportedAt = default;
            profile = null;
            CareerProfilePortablePackageDto package;
            try
            {
                string json = File.ReadAllText(fullPath, Encoding.UTF8);
                if (!PersistenceJsonSerializer.TryDeserialize(
                    json, out package, out string error))
                {
                    issues.Add(Issue(PersistenceStorageError.InvalidJson,
                        fullPath, null, error));
                    return null;
                }
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException)
            {
                issues.Add(Issue(PersistenceStorageError.IoFailure,
                    fullPath, null, exception.Message));
                return null;
            }

            if (package == null)
            {
                issues.Add(Issue(PersistenceStorageError.InvalidJson,
                    fullPath, null, "履歷搬運檔內容為空。"));
                return null;
            }

            if (package.format_version != CareerProfilePortablePackageDto.CurrentFormatVersion)
            {
                issues.Add(Issue(PersistenceStorageError.UnsupportedSchemaVersion,
                    fullPath, "format_version", "不支援的履歷搬運檔版本。"));
            }

            if (!DateTimeOffset.TryParse(package.exported_at,
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out exportedAt))
            {
                issues.Add(Issue(PersistenceStorageError.DataSetValidationFailed,
                    fullPath, "exported_at", "匯出時間格式無效。"));
            }

            if (package.profile == null)
            {
                issues.Add(Issue(PersistenceStorageError.DataSetValidationFailed,
                    fullPath, "profile", "搬運檔沒有履歷資料。"));
            }

            try
            {
                if (string.IsNullOrWhiteSpace(package.content_sha256)
                    || !string.Equals(package.content_sha256,
                        package.CalculateContentSha256(), StringComparison.Ordinal))
                {
                    issues.Add(Issue(PersistenceStorageError.DataSetValidationFailed,
                        fullPath, "content_sha256",
                        "校驗碼不符；檔案可能已損壞或被修改。"));
                }
            }
            catch (Exception exception) when (
                exception is InvalidOperationException
                || exception is System.Runtime.Serialization.SerializationException
                || exception is ArgumentException)
            {
                issues.Add(Issue(PersistenceStorageError.DataSetValidationFailed,
                    fullPath, "content_sha256", exception.Message));
            }

            if (issues.Count > 0)
            {
                return null;
            }

            PersistenceConversionResult<CareerProfile> mapped =
                CareerProfileDtoMapper.ToDomain(package.profile);
            foreach (PersistenceConversionIssue issue in mapped.Issues)
            {
                issues.Add(Issue(PersistenceStorageError.ConversionFailed,
                    fullPath, issue.FieldPath, issue.Error.ToString()));
            }

            if (mapped.Value != null)
            {
                foreach (CareerProfileValidationIssue issue in
                    CareerProfileValidator.Validate(mapped.Value))
                {
                    issues.Add(Issue(PersistenceStorageError.EntityValidationFailed,
                        fullPath, issue.FieldPath, issue.Error.ToString()));
                }
            }

            profile = issues.Count == 0 ? mapped.Value : null;
            return profile == null ? null : package;
        }

        private static PersistenceStorageResult<CareerProfilePortableImportSummary>
            TryCreateBackup(string sourcePath, out string backupPath)
        {
            backupPath = null;
            try
            {
                string profileDirectory = Path.GetDirectoryName(sourcePath);
                string personalDataRoot = Path.GetDirectoryName(profileDirectory);
                string directory = Path.Combine(personalDataRoot, BackupDirectoryName);
                Directory.CreateDirectory(directory);
                backupPath = Path.Combine(directory,
                    "profile-before-import-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")
                    + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json");
                byte[] originalBytes = File.ReadAllBytes(sourcePath);
                using (var stream = new FileStream(
                    backupPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(originalBytes, 0, originalBytes.Length);
                    stream.Flush();
                }
                return null;
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException || exception is NotSupportedException)
            {
                return ImportFailure(Issue(PersistenceStorageError.IoFailure,
                    backupPath ?? sourcePath, null,
                    "無法建立原履歷備份，已取消匯入：" + exception.Message));
            }
        }

        private static void TryRestoreBackup(string backupPath, string destinationPath)
        {
            try
            {
                File.Copy(backupPath, destinationPath, true);
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException || exception is NotSupportedException)
            {
                // 原備份仍保留於 backupPath，交由使用者或後續修復流程處理。
            }
        }

        private static bool SameProfile(CareerProfile left, CareerProfile right)
        {
            if (left == null || right == null) return false;
            PersistenceConversionResult<CareerProfileDto> a =
                CareerProfileDtoMapper.ToDto(left);
            PersistenceConversionResult<CareerProfileDto> b =
                CareerProfileDtoMapper.ToDto(right);
            return a.IsSuccess && b.IsSuccess
                && string.Equals(PersistenceJsonSerializer.Serialize(a.Value),
                    PersistenceJsonSerializer.Serialize(b.Value), StringComparison.Ordinal);
        }

        private static PersistenceStorageResult<CareerProfilePortableImportPreview>
            PreviewFailure(IEnumerable<PersistenceStorageIssue> issues)
        {
            return new PersistenceStorageResult<CareerProfilePortableImportPreview>(null, issues);
        }

        private static PersistenceStorageResult<CareerProfilePortableImportSummary>
            ImportFailure(params PersistenceStorageIssue[] issues)
        {
            return new PersistenceStorageResult<CareerProfilePortableImportSummary>(null, issues);
        }

        private static PersistenceStorageResult<CareerProfilePortableImportSummary> Success(
            CareerProfile profile,
            CareerProfileImportDisposition disposition,
            string backupPath)
        {
            return new PersistenceStorageResult<CareerProfilePortableImportSummary>(
                new CareerProfilePortableImportSummary(profile, disposition, backupPath),
                Array.Empty<PersistenceStorageIssue>());
        }

        private static PersistenceStorageIssue Issue(
            PersistenceStorageError error,
            string path,
            string field,
            string message)
        {
            return new PersistenceStorageIssue(error, path, field, message);
        }
    }
}
