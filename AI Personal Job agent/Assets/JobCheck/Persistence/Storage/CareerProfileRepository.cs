using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JobCheck.Domain;

namespace JobCheck.Persistence
{
    /// <summary>個人履歷母資料的獨立檔案邊界，不與職缺搬運格式混用。</summary>
    public static class CareerProfileRepository
    {
        public const string ProfileDirectoryName = "profile";
        public const string ProfileFileName = "profile.json";

        public static string GetProfilePath(string personalDataRoot)
        {
            return Path.Combine(personalDataRoot, ProfileDirectoryName, ProfileFileName);
        }

        /// <summary>檔案尚不存在時回傳新的空白草稿，且不建立任何檔案或目錄。</summary>
        public static PersistenceStorageResult<CareerProfile> Load(string personalDataRoot)
        {
            var issues = new List<PersistenceStorageIssue>();
            if (string.IsNullOrWhiteSpace(personalDataRoot))
            {
                issues.Add(Issue(
                    PersistenceStorageError.DataRootNotFound,
                    personalDataRoot,
                    null,
                    "個人資料根目錄不可空白。"));
                return new PersistenceStorageResult<CareerProfile>(null, issues);
            }

            string path = GetProfilePath(personalDataRoot);
            if (!File.Exists(path))
            {
                DateTimeOffset now = DateTimeOffset.Now;
                return new PersistenceStorageResult<CareerProfile>(
                    new CareerProfile
                    {
                        Id = CareerProfileIdGenerator.CreateProfileId(),
                        CreatedAt = now,
                        UpdatedAt = now
                    },
                    issues);
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (!PersistenceJsonSerializer.TryDeserialize(
                    json,
                    out CareerProfileDto dto,
                    out string error))
                {
                    issues.Add(Issue(PersistenceStorageError.InvalidJson, path, null, error));
                    return new PersistenceStorageResult<CareerProfile>(null, issues);
                }

                PersistenceConversionResult<CareerProfile> conversion =
                    CareerProfileDtoMapper.ToDomain(dto);
                foreach (PersistenceConversionIssue item in conversion.Issues)
                {
                    issues.Add(Issue(
                        PersistenceStorageError.ConversionFailed,
                        path,
                        item.FieldPath,
                        item.Error.ToString()));
                }

                AddValidationIssues(conversion.Value, path, issues);
                return new PersistenceStorageResult<CareerProfile>(conversion.Value, issues);
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is ArgumentException
                || exception is NotSupportedException)
            {
                issues.Add(Issue(PersistenceStorageError.IoFailure, path, null, exception.Message));
                return new PersistenceStorageResult<CareerProfile>(null, issues);
            }
        }

        /// <summary>驗證、原子寫入，並重新載入核對實際落盤內容。</summary>
        public static PersistenceStorageResult<CareerProfile> Save(
            string personalDataRoot,
            CareerProfile profile)
        {
            var issues = new List<PersistenceStorageIssue>();
            string path = string.IsNullOrWhiteSpace(personalDataRoot)
                ? personalDataRoot
                : GetProfilePath(personalDataRoot);
            AddValidationIssues(profile, path, issues);

            PersistenceConversionResult<CareerProfileDto> conversion =
                CareerProfileDtoMapper.ToDto(profile);
            foreach (PersistenceConversionIssue item in conversion.Issues)
            {
                issues.Add(Issue(
                    PersistenceStorageError.ConversionFailed,
                    path,
                    item.FieldPath,
                    item.Error.ToString()));
            }

            if (issues.Count > 0 || conversion.Value == null)
            {
                return new PersistenceStorageResult<CareerProfile>(null, issues);
            }

            try
            {
                string directory = Path.GetDirectoryName(path);
                Directory.CreateDirectory(directory);
                WriteOrReplaceAtomically(path, PersistenceJsonSerializer.Serialize(conversion.Value));
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is ArgumentException
                || exception is NotSupportedException
                || exception is InvalidOperationException
                || exception is System.Runtime.Serialization.SerializationException)
            {
                issues.Add(Issue(PersistenceStorageError.IoFailure, path, null, exception.Message));
                return new PersistenceStorageResult<CareerProfile>(null, issues);
            }

            return Load(personalDataRoot);
        }

        private static void AddValidationIssues(
            CareerProfile profile,
            string path,
            ICollection<PersistenceStorageIssue> issues)
        {
            foreach (CareerProfileValidationIssue item in CareerProfileValidator.Validate(profile))
            {
                issues.Add(Issue(
                    PersistenceStorageError.EntityValidationFailed,
                    path,
                    item.FieldPath,
                    item.Error.ToString()));
            }
        }

        private static PersistenceStorageIssue Issue(
            PersistenceStorageError error,
            string path,
            string field,
            string message)
        {
            return new PersistenceStorageIssue(error, path, field, message);
        }

        private static void WriteOrReplaceAtomically(string path, string json)
        {
            string temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    File.Replace(temporaryPath, path, null);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }
}
