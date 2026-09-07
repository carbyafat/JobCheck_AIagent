using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JobCheck.Persistence
{
    public enum JobCheckMigrationError
    {
        SourceDirectoryNotFound,
        NoLegacyJobsFound,
        InvalidLegacyJson,
        UnsupportedLegacySchema,
        NonDemoSourceRejected,
        MissingRequiredLegacyField,
        InvalidLegacyDateTime,
        ConflictingLegacyValue,
        LegacyFieldDeferred,
        TrackingJobMismatch,
        UnknownLegacyStatus,
        TargetAlreadyExists,
        SnapshotWriteFailed,
        SnapshotValidationFailed,
        IoFailure
    }

    public enum JobCheckMigrationSeverity
    {
        Warning,
        Error
    }

    /// <summary>
    /// Migration 問題同時保存穩定代碼、檔案與人類可讀說明。
    /// </summary>
    public sealed class JobCheckMigrationIssue
    {
        public JobCheckMigrationIssue(
            JobCheckMigrationSeverity severity,
            JobCheckMigrationError error,
            string filePath,
            string fieldPath,
            string message)
        {
            Severity = severity;
            Error = error;
            FilePath = filePath;
            FieldPath = fieldPath;
            Message = message;
        }

        public JobCheckMigrationSeverity Severity { get; }
        public JobCheckMigrationError Error { get; }
        public string FilePath { get; }
        public string FieldPath { get; }
        public string Message { get; }
    }

    /// <summary>
    /// Migration 完成或失敗結果。失敗時仍保留 report/work 路徑供追查。
    /// </summary>
    public sealed class JobCheckMigrationResult
    {
        public JobCheckMigrationResult(
            string dataRoot,
            string workRoot,
            int companyCount,
            int jobPostingCount,
            int applicationCount,
            IEnumerable<JobCheckMigrationIssue> issues)
        {
            DataRoot = dataRoot;
            WorkRoot = workRoot;
            CompanyCount = companyCount;
            JobPostingCount = jobPostingCount;
            ApplicationCount = applicationCount;
            var snapshot = issues == null
                ? Array.Empty<JobCheckMigrationIssue>()
                : new List<JobCheckMigrationIssue>(issues).ToArray();
            Issues = new ReadOnlyCollection<JobCheckMigrationIssue>(snapshot);
        }

        public string DataRoot { get; }
        public string WorkRoot { get; }
        public int CompanyCount { get; }
        public int JobPostingCount { get; }
        public int ApplicationCount { get; }
        public IReadOnlyList<JobCheckMigrationIssue> Issues { get; }
        public bool IsSuccess
        {
            get
            {
                foreach (JobCheckMigrationIssue issue in Issues)
                {
                    if (issue.Severity == JobCheckMigrationSeverity.Error)
                    {
                        return false;
                    }
                }

                return !string.IsNullOrWhiteSpace(DataRoot);
            }
        }
    }

    /// <summary>
    /// 寫入 data/migration 的機器可讀執行紀錄。
    /// </summary>
    [Serializable]
    public sealed class MigrationManifestDto
    {
        public string migration_version;
        public string status;
        public string source_root;
        public string target_root;
        public string executed_at;
        public List<MigrationSourceFileDto> source_files = new List<MigrationSourceFileDto>();
        public List<MigrationIdMappingDto> id_mappings = new List<MigrationIdMappingDto>();
        public int company_count;
        public int job_posting_count;
        public int application_count;
        public int application_event_count;
    }

    [Serializable]
    public sealed class MigrationSourceFileDto
    {
        public string relative_path;
        public long size_bytes;
        public string sha256;
    }

    [Serializable]
    public sealed class MigrationIdMappingDto
    {
        public string entity_type;
        public string legacy_key;
        public string new_id;
    }

    [Serializable]
    public sealed class MigrationReportDto
    {
        public string migration_version;
        public string status;
        public string executed_at;
        public int source_job_count;
        public int source_tracking_count;
        public int output_company_count;
        public int output_job_count;
        public int output_application_count;
        public int output_event_count;
        public List<MigrationReportIssueDto> issues = new List<MigrationReportIssueDto>();
    }

    [Serializable]
    public sealed class MigrationReportIssueDto
    {
        public string severity;
        public string error;
        public string file_path;
        public string field_path;
        public string message;
    }
}
