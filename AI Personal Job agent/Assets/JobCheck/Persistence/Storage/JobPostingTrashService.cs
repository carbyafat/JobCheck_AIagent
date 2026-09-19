using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JobCheck.Domain;

namespace JobCheck.Persistence
{
    /// <summary>
    /// 將誤建的職缺與其應徵紀錄移入本機回收區。公司資料刻意保留，
    /// 避免刪除同一公司底下的其他職缺，亦方便日後復原關聯。
    /// </summary>
    public static class JobPostingTrashService
    {
        public const string TrashDirectoryName = "trash";

        /// <summary>唯讀列出回收區；任何損壞項目都會回報，不提供半套清單。</summary>
        public static PersistenceStorageResult<JobPostingTrashCatalog> List(string dataRoot)
        {
            var issues = new List<PersistenceStorageIssue>();
            string trashRoot;
            try
            {
                trashRoot = GetSafeTrashRoot(dataRoot);
                if (!Directory.Exists(trashRoot))
                    return new PersistenceStorageResult<JobPostingTrashCatalog>(
                        new JobPostingTrashCatalog(Array.Empty<JobPostingTrashEntry>()), issues);
                if (IsLink(trashRoot)) throw new IOException("回收區不可為檔案連結。");
                var entries = new List<JobPostingTrashEntry>();
                foreach (string directory in Directory.GetDirectories(trashRoot)
                    .OrderByDescending(path => path, StringComparer.Ordinal))
                {
                    JobPostingTrashEntry entry = ReadEntry(directory, issues);
                    if (entry != null) entries.Add(entry);
                }
                return new PersistenceStorageResult<JobPostingTrashCatalog>(
                    issues.Count == 0 ? new JobPostingTrashCatalog(entries) : null, issues);
            }
            catch (Exception exception) when (IsFileException(exception))
            {
                return FailureCatalog(dataRoot, exception.Message);
            }
        }

        /// <summary>還原單筆職缺及相關應徵；遇到相同 ID 或資料異常時完全不覆蓋。</summary>
        public static PersistenceStorageResult<JobPostingTrashSummary> Restore(
            string dataRoot, string entryDirectory)
        {
            var issues = new List<PersistenceStorageIssue>();
            string entryPath;
            try { entryPath = GetSafeEntryPath(dataRoot, entryDirectory); }
            catch (Exception exception) when (IsFileException(exception))
            {
                return Failure(dataRoot, null, exception.Message);
            }
            JobPostingTrashEntry entry = ReadEntry(entryPath, issues);
            if (entry == null || issues.Count > 0)
                return new PersistenceStorageResult<JobPostingTrashSummary>(null, issues);

            PersistenceStorageResult<JobCheckDataSet> active = JobCheckDataRepository.Load(dataRoot);
            if (!active.IsSuccess)
                return new PersistenceStorageResult<JobPostingTrashSummary>(null, active.Issues);
            if (active.Value.JobPostings.Any(item => item.Id == entry.JobPostingId)
                || entry.ApplicationIds.Any(id => active.Value.Applications.Any(item => item.Id == id)))
                return Failure(entryPath, "id", "主資料區已有相同 ID，未覆蓋也未還原。");

            var candidate = new JobCheckDataSet(
                active.Value.Companies,
                active.Value.JobPostings.Concat(new[] { entry.JobPosting }),
                active.Value.Applications.Concat(entry.Applications),
                active.Value.ApplicationEvents.Concat(entry.Events));
            issues.AddRange(JobCheckDataRepository.ValidateSnapshot(candidate));
            if (issues.Count > 0)
                return new PersistenceStorageResult<JobPostingTrashSummary>(null, issues);

            var moves = BuildRestoreMoves(dataRoot, entry);
            foreach (FileMove move in moves)
                if (File.Exists(move.DestinationPath))
                    return Failure(move.DestinationPath, null, "還原位置已有檔案，未覆蓋也未還原。");
            var completed = new List<FileMove>();
            try
            {
                foreach (FileMove move in moves)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(move.DestinationPath));
                    File.Move(move.SourcePath, move.DestinationPath);
                    completed.Add(move);
                }
                PersistenceStorageResult<JobCheckDataSet> check = JobCheckDataRepository.Load(dataRoot);
                if (!check.IsSuccess
                    || !check.Value.JobPostings.Any(item => item.Id == entry.JobPostingId))
                    throw new IOException("還原後重新讀取驗證失敗。");
                Directory.Delete(entryPath, true);
            }
            catch (Exception exception) when (IsFileException(exception))
            {
                Rollback(completed, issues, "還原失敗，且檔案回復失敗：");
                issues.Add(new PersistenceStorageIssue(PersistenceStorageError.IoFailure,
                    entryPath, null, "無法還原職缺，已嘗試回復回收區：" + exception.Message));
                return new PersistenceStorageResult<JobPostingTrashSummary>(null, issues);
            }
            return new PersistenceStorageResult<JobPostingTrashSummary>(
                new JobPostingTrashSummary(entry.JobPostingId, entry.CompanyId,
                    entry.ApplicationCount, entryPath), Array.Empty<PersistenceStorageIssue>());
        }

        /// <summary>永久移除一個已驗證的回收項目。</summary>
        public static PersistenceStorageResult<JobPostingTrashDeleteSummary> DeletePermanently(
            string dataRoot, string entryDirectory)
        {
            var issues = new List<PersistenceStorageIssue>();
            try
            {
                string entryPath = GetSafeEntryPath(dataRoot, entryDirectory);
                JobPostingTrashEntry entry = ReadEntry(entryPath, issues);
                if (entry == null || issues.Count > 0)
                    return new PersistenceStorageResult<JobPostingTrashDeleteSummary>(null, issues);
                Directory.Delete(entryPath, true);
                return new PersistenceStorageResult<JobPostingTrashDeleteSummary>(
                    new JobPostingTrashDeleteSummary(1), issues);
            }
            catch (Exception exception) when (IsFileException(exception))
            {
                issues.Add(new PersistenceStorageIssue(PersistenceStorageError.IoFailure,
                    entryDirectory, null, exception.Message));
                return new PersistenceStorageResult<JobPostingTrashDeleteSummary>(null, issues);
            }
        }

        /// <summary>永久清除所有可驗證回收項目；先完整驗證再開始刪除。</summary>
        public static PersistenceStorageResult<JobPostingTrashDeleteSummary> Empty(string dataRoot)
        {
            PersistenceStorageResult<JobPostingTrashCatalog> catalog = List(dataRoot);
            if (!catalog.IsSuccess)
                return new PersistenceStorageResult<JobPostingTrashDeleteSummary>(null, catalog.Issues);
            var issues = new List<PersistenceStorageIssue>();
            int deleted = 0;
            foreach (JobPostingTrashEntry entry in catalog.Value.Entries)
            {
                try { Directory.Delete(entry.DirectoryPath, true); deleted++; }
                catch (Exception exception) when (IsFileException(exception))
                {
                    issues.Add(new PersistenceStorageIssue(PersistenceStorageError.IoFailure,
                        entry.DirectoryPath, null, exception.Message));
                    break;
                }
            }
            return new PersistenceStorageResult<JobPostingTrashDeleteSummary>(
                issues.Count == 0 ? new JobPostingTrashDeleteSummary(deleted) : null, issues);
        }

        /// <summary>
        /// 把指定 JobPosting 及所有參照它的 Application JSON 移到時間戳記資料夾。
        /// ApplicationEvent 內嵌於 Application JSON，因此會跟著一起移動。
        /// 任一檔案移動失敗時，已移動的檔案會回復原位。
        /// </summary>
        public static PersistenceStorageResult<JobPostingTrashSummary> MoveToTrash(
            string dataRoot,
            string jobPostingId)
        {
            if (string.IsNullOrWhiteSpace(jobPostingId))
            {
                return Failure(dataRoot, "job_posting_id", "職缺 ID 不可為空白。");
            }

            PersistenceStorageResult<JobCheckDataSet> load = JobCheckDataRepository.Load(dataRoot);
            if (!load.IsSuccess)
            {
                return new PersistenceStorageResult<JobPostingTrashSummary>(null, load.Issues);
            }

            JobPosting jobPosting = load.Value.JobPostings.SingleOrDefault(item =>
                item != null
                && string.Equals(item.Id, jobPostingId, StringComparison.Ordinal));
            if (jobPosting == null)
            {
                return Failure(jobPostingId, "job_posting_id", "找不到要刪除的職缺。");
            }

            List<Application> relatedApplications = load.Value.Applications
                .Where(item => item != null && string.Equals(
                    item.JobPostingId,
                    jobPostingId,
                    StringComparison.Ordinal))
                .ToList();

            string trashDirectory = Path.Combine(
                dataRoot,
                TrashDirectoryName,
                DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss_fff")
                    + "_" + jobPostingId
                    + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            var moves = new List<FileMove>();
            moves.Add(new FileMove(
                Path.Combine(dataRoot, JobCheckDataRepository.JobsDirectoryName, jobPosting.Id + ".json"),
                Path.Combine(trashDirectory, JobCheckDataRepository.JobsDirectoryName, jobPosting.Id + ".json")));
            foreach (Application application in relatedApplications)
            {
                moves.Add(new FileMove(
                    Path.Combine(dataRoot, JobCheckDataRepository.ApplicationsDirectoryName, application.Id + ".json"),
                    Path.Combine(trashDirectory, JobCheckDataRepository.ApplicationsDirectoryName, application.Id + ".json")));
            }

            foreach (FileMove move in moves)
            {
                if (!File.Exists(move.SourcePath))
                {
                    return Failure(
                        move.SourcePath,
                        null,
                        "準備刪除時找不到預期的資料檔，未移動任何資料。");
                }
            }

            var completedMoves = new List<FileMove>();
            try
            {
                foreach (FileMove move in moves)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(move.DestinationPath));
                    File.Move(move.SourcePath, move.DestinationPath);
                    completedMoves.Add(move);
                }
            }
            catch (Exception exception) when (IsFileException(exception))
            {
                var issues = new List<PersistenceStorageIssue>();
                for (int index = completedMoves.Count - 1; index >= 0; index--)
                {
                    FileMove completed = completedMoves[index];
                    try
                    {
                        if (File.Exists(completed.DestinationPath)
                            && !File.Exists(completed.SourcePath))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(completed.SourcePath));
                            File.Move(completed.DestinationPath, completed.SourcePath);
                        }
                    }
                    catch (Exception rollbackException) when (IsFileException(rollbackException))
                    {
                        issues.Add(new PersistenceStorageIssue(
                            PersistenceStorageError.IoFailure,
                            completed.DestinationPath,
                            null,
                            "刪除失敗，且檔案回復失敗：" + rollbackException.Message));
                    }
                }

                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.IoFailure,
                    trashDirectory,
                    null,
                    "無法把職缺移入回收區，已嘗試回復原檔案：" + exception.Message));
                return new PersistenceStorageResult<JobPostingTrashSummary>(null, issues);
            }

            return new PersistenceStorageResult<JobPostingTrashSummary>(
                new JobPostingTrashSummary(
                    jobPosting.Id,
                    jobPosting.CompanyId,
                    relatedApplications.Count,
                    trashDirectory),
                Array.Empty<PersistenceStorageIssue>());
        }

        private static JobPostingTrashEntry ReadEntry(string directory,
            ICollection<PersistenceStorageIssue> issues)
        {
            try
            {
                if (!Directory.Exists(directory) || IsLink(directory))
                    throw new IOException("回收項目不存在或是檔案連結。");
                string jobs = Path.Combine(directory, JobCheckDataRepository.JobsDirectoryName);
                string applications = Path.Combine(directory,
                    JobCheckDataRepository.ApplicationsDirectoryName);
                string[] rootEntries = Directory.GetFileSystemEntries(directory);
                if (rootEntries.Any(path => !string.Equals(path, jobs, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(path, applications, StringComparison.OrdinalIgnoreCase)))
                    throw new IOException("回收項目包含未知檔案，為避免誤刪已停止操作。");
                if (!Directory.Exists(jobs) || IsLink(jobs)
                    || (Directory.Exists(applications) && IsLink(applications)))
                    throw new IOException("回收項目資料夾結構無效。");
                string[] jobEntries = Directory.GetFileSystemEntries(jobs);
                string[] jobFiles = Directory.GetFiles(jobs, "*.json", SearchOption.TopDirectoryOnly);
                if (jobEntries.Length != jobFiles.Length)
                    throw new IOException("職缺回收資料夾包含未知檔案，已停止操作。");
                if (jobFiles.Length != 1) throw new IOException("回收項目必須包含一筆職缺。");
                JobPosting job = ReadJob(jobFiles[0], issues);
                if (job == null) return null;
                var applicationItems = new List<Application>();
                var events = new List<ApplicationEvent>();
                if (Directory.Exists(applications))
                {
                    string[] applicationEntries = Directory.GetFileSystemEntries(applications);
                    string[] applicationFiles = Directory.GetFiles(applications, "*.json",
                        SearchOption.TopDirectoryOnly);
                    if (applicationEntries.Length != applicationFiles.Length)
                        throw new IOException("應徵回收資料夾包含未知檔案，已停止操作。");
                    foreach (string path in applicationFiles.OrderBy(
                        item => item, StringComparer.Ordinal))
                    {
                        ApplicationPersistenceBundle bundle = ReadApplication(path, issues);
                        if (bundle == null) continue;
                        if (bundle.Application.JobPostingId != job.Id)
                        {
                            AddIssue(issues, path, "job_posting_id", "回收項目的應徵不屬於此職缺。");
                            continue;
                        }
                        applicationItems.Add(bundle.Application);
                        events.AddRange(bundle.Events);
                    }
                }
                if (issues.Count > 0) return null;
                DateTimeOffset deletedAt = Directory.GetCreationTimeUtc(directory);
                string prefix = Path.GetFileName(directory);
                if (prefix.Length >= 19 && DateTimeOffset.TryParseExact(prefix.Substring(0, 19),
                    "yyyyMMdd_HHmmss_fff", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeLocal, out DateTimeOffset parsed))
                    deletedAt = parsed;
                return new JobPostingTrashEntry(directory, job, applicationItems, events, deletedAt);
            }
            catch (Exception exception) when (IsFileException(exception))
            {
                AddIssue(issues, directory, null, exception.Message);
                return null;
            }
        }

        private static JobPosting ReadJob(string path, ICollection<PersistenceStorageIssue> issues)
        {
            if (!TryRead(path, out JobPostingDto dto, issues)) return null;
            if (!string.Equals(dto.schema_version, JobPosting.CurrentSchemaVersion,
                StringComparison.Ordinal))
            {
                AddIssue(issues, path, "schema_version", "職缺資料版本不支援。");
                return null;
            }
            PersistenceConversionResult<JobPosting> mapped = JobPostingDtoMapper.ToDomain(dto);
            AddMappingIssues(path, mapped.Issues, issues);
            if (!mapped.IsSuccess || Path.GetFileNameWithoutExtension(path) != mapped.Value.Id)
            {
                if (mapped.IsSuccess) AddIssue(issues, path, "id", "職缺 ID 與檔名不一致。");
                return null;
            }
            return mapped.Value;
        }

        private static ApplicationPersistenceBundle ReadApplication(string path,
            ICollection<PersistenceStorageIssue> issues)
        {
            if (!TryRead(path, out ApplicationDto dto, issues)) return null;
            if (!string.Equals(dto.schema_version, Application.CurrentSchemaVersion,
                StringComparison.Ordinal))
            {
                AddIssue(issues, path, "schema_version", "應徵資料版本不支援。");
                return null;
            }
            PersistenceConversionResult<ApplicationPersistenceBundle> mapped =
                ApplicationDtoMapper.ToDomain(dto);
            AddMappingIssues(path, mapped.Issues, issues);
            if (!mapped.IsSuccess
                || Path.GetFileNameWithoutExtension(path) != mapped.Value.Application.Id)
            {
                if (mapped.IsSuccess) AddIssue(issues, path, "id", "應徵 ID 與檔名不一致。");
                return null;
            }
            return mapped.Value;
        }

        private static bool TryRead<T>(string path, out T dto,
            ICollection<PersistenceStorageIssue> issues) where T : class
        {
            dto = null;
            try
            {
                if (PersistenceJsonSerializer.TryDeserialize(
                    File.ReadAllText(path, Encoding.UTF8), out dto, out string error)) return true;
                issues.Add(new PersistenceStorageIssue(PersistenceStorageError.InvalidJson,
                    path, null, error));
            }
            catch (Exception exception) when (IsFileException(exception))
            {
                AddIssue(issues, path, null, exception.Message);
            }
            return false;
        }

        private static List<FileMove> BuildRestoreMoves(string dataRoot,
            JobPostingTrashEntry entry)
        {
            var moves = new List<FileMove> {
                new FileMove(Path.Combine(entry.DirectoryPath,
                    JobCheckDataRepository.JobsDirectoryName, entry.JobPostingId + ".json"),
                    Path.Combine(dataRoot, JobCheckDataRepository.JobsDirectoryName,
                        entry.JobPostingId + ".json"))
            };
            moves.AddRange(entry.ApplicationIds.Select(id => new FileMove(
                Path.Combine(entry.DirectoryPath, JobCheckDataRepository.ApplicationsDirectoryName,
                    id + ".json"),
                Path.Combine(dataRoot, JobCheckDataRepository.ApplicationsDirectoryName,
                    id + ".json"))));
            return moves;
        }

        private static void Rollback(IEnumerable<FileMove> completed,
            ICollection<PersistenceStorageIssue> issues, string prefix)
        {
            foreach (FileMove move in completed.Reverse())
            {
                try
                {
                    if (File.Exists(move.DestinationPath) && !File.Exists(move.SourcePath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(move.SourcePath));
                        File.Move(move.DestinationPath, move.SourcePath);
                    }
                }
                catch (Exception exception) when (IsFileException(exception))
                {
                    AddIssue(issues, move.DestinationPath, null, prefix + exception.Message);
                }
            }
        }

        private static string GetSafeTrashRoot(string dataRoot)
        {
            if (string.IsNullOrWhiteSpace(dataRoot)) throw new ArgumentException("資料目錄不可空白。");
            string root = Path.GetFullPath(dataRoot).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(root, Path.GetPathRoot(root), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("資料目錄不可為磁碟根目錄。");
            if (Directory.Exists(root) && IsLink(root))
                throw new IOException("個人資料目錄不可為檔案連結。");
            string trashRoot = Path.Combine(root, TrashDirectoryName);
            if (Directory.Exists(trashRoot) && IsLink(trashRoot))
                throw new IOException("回收區不可為檔案連結。");
            return trashRoot;
        }

        private static string GetSafeEntryPath(string dataRoot, string entryDirectory)
        {
            if (string.IsNullOrWhiteSpace(entryDirectory))
                throw new ArgumentException("請選擇回收項目。");
            string trashRoot = GetSafeTrashRoot(dataRoot);
            string entry = Path.GetFullPath(entryDirectory).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(Path.GetDirectoryName(entry), trashRoot,
                StringComparison.OrdinalIgnoreCase) || !Directory.Exists(entry) || IsLink(entry))
                throw new IOException("選取的資料夾不是安全的回收項目。");
            return entry;
        }

        private static bool IsLink(string path) =>
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

        private static void AddMappingIssues(string path,
            IEnumerable<PersistenceConversionIssue> mappingIssues,
            ICollection<PersistenceStorageIssue> issues)
        {
            foreach (PersistenceConversionIssue issue in mappingIssues)
                issues.Add(new PersistenceStorageIssue(PersistenceStorageError.ConversionFailed,
                    path, issue.FieldPath, issue.Error.ToString()));
        }

        private static void AddIssue(ICollection<PersistenceStorageIssue> issues,
            string path, string field, string message) => issues.Add(new PersistenceStorageIssue(
                PersistenceStorageError.IoFailure, path, field, message));

        private static PersistenceStorageResult<JobPostingTrashCatalog> FailureCatalog(
            string path, string message) => new PersistenceStorageResult<JobPostingTrashCatalog>(
                null, new[] { new PersistenceStorageIssue(
                    PersistenceStorageError.IoFailure, path, null, message) });

        private static PersistenceStorageResult<JobPostingTrashSummary> Failure(
            string filePath,
            string fieldPath,
            string message)
        {
            return new PersistenceStorageResult<JobPostingTrashSummary>(
                null,
                new[]
                {
                    new PersistenceStorageIssue(
                        PersistenceStorageError.EntityValidationFailed,
                        filePath,
                        fieldPath,
                        message)
                });
        }

        private static bool IsFileException(Exception exception)
        {
            return exception is IOException
                || exception is UnauthorizedAccessException
                || exception is ArgumentException
                || exception is NotSupportedException;
        }

        private sealed class FileMove
        {
            public FileMove(string sourcePath, string destinationPath)
            {
                SourcePath = sourcePath;
                DestinationPath = destinationPath;
            }

            public string SourcePath { get; }
            public string DestinationPath { get; }
        }
    }

    public sealed class JobPostingTrashCatalog
    {
        public JobPostingTrashCatalog(IEnumerable<JobPostingTrashEntry> entries) =>
            Entries = entries.ToList().AsReadOnly();
        public IReadOnlyList<JobPostingTrashEntry> Entries { get; }
    }

    public sealed class JobPostingTrashEntry
    {
        internal JobPostingTrashEntry(string directoryPath, JobPosting jobPosting,
            IEnumerable<Application> applications, IEnumerable<ApplicationEvent> events,
            DateTimeOffset deletedAt)
        {
            DirectoryPath = directoryPath;
            JobPosting = jobPosting;
            Applications = applications.ToList().AsReadOnly();
            Events = events.ToList().AsReadOnly();
            DeletedAt = deletedAt;
        }
        public string DirectoryPath { get; }
        public string JobPostingId => JobPosting.Id;
        public string CompanyId => JobPosting.CompanyId;
        public string Title => JobPosting.Title;
        public DateTimeOffset DeletedAt { get; }
        public int ApplicationCount => Applications.Count;
        public IReadOnlyList<string> ApplicationIds => Applications.Select(item => item.Id).ToList();
        internal JobPosting JobPosting { get; }
        internal IReadOnlyList<Application> Applications { get; }
        internal IReadOnlyList<ApplicationEvent> Events { get; }
    }

    public sealed class JobPostingTrashDeleteSummary
    {
        public JobPostingTrashDeleteSummary(int deletedCount) => DeletedCount = deletedCount;
        public int DeletedCount { get; }
    }

    /// <summary>
    /// 成功移入回收區後回傳給 UI 的影響範圍。
    /// </summary>
    public sealed class JobPostingTrashSummary
    {
        public JobPostingTrashSummary(
            string jobPostingId,
            string companyId,
            int applicationCount,
            string trashDirectory)
        {
            JobPostingId = jobPostingId;
            CompanyId = companyId;
            ApplicationCount = applicationCount;
            TrashDirectory = trashDirectory;
        }

        public string JobPostingId { get; }
        public string CompanyId { get; }
        public int ApplicationCount { get; }
        public string TrashDirectory { get; }
    }
}
