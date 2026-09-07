using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JobCheck.Persistence
{
    /// <summary>
    /// Repository 可穩定判斷的檔案與資料錯誤種類。
    /// </summary>
    public enum PersistenceStorageError
    {
        DataRootNotFound,
        InvalidJson,
        UnsupportedSchemaVersion,
        ConversionFailed,
        EntityValidationFailed,
        DataSetValidationFailed,
        UnsafeEntityId,
        FileNameIdMismatch,
        DestinationNotEmpty,
        IoFailure
    }

    /// <summary>
    /// 一筆包含檔案與欄位位置的 Repository 問題。
    /// </summary>
    public sealed class PersistenceStorageIssue
    {
        public PersistenceStorageIssue(
            PersistenceStorageError error,
            string filePath,
            string fieldPath,
            string message)
        {
            Error = error;
            FilePath = filePath;
            FieldPath = fieldPath;
            Message = message;
        }

        public PersistenceStorageError Error { get; }
        public string FilePath { get; }
        public string FieldPath { get; }
        public string Message { get; }
    }

    /// <summary>
    /// Repository 操作結果。只要有 Issue，Value 就不可使用，避免載入半套資料。
    /// </summary>
    public sealed class PersistenceStorageResult<T>
        where T : class
    {
        public PersistenceStorageResult(
            T value,
            IEnumerable<PersistenceStorageIssue> issues)
        {
            var snapshot = issues == null
                ? Array.Empty<PersistenceStorageIssue>()
                : new List<PersistenceStorageIssue>(issues).ToArray();
            Issues = new ReadOnlyCollection<PersistenceStorageIssue>(snapshot);
            Value = snapshot.Length == 0 ? value : null;
        }

        public T Value { get; }
        public IReadOnlyList<PersistenceStorageIssue> Issues { get; }
        public bool IsSuccess => Value != null && Issues.Count == 0;
    }

    /// <summary>
    /// 成功寫出完整資料快照時的筆數摘要。
    /// </summary>
    public sealed class PersistenceWriteSummary
    {
        public PersistenceWriteSummary(int companies, int jobs, int applications)
        {
            CompanyCount = companies;
            JobPostingCount = jobs;
            ApplicationCount = applications;
        }

        public int CompanyCount { get; }
        public int JobPostingCount { get; }
        public int ApplicationCount { get; }
    }
}
