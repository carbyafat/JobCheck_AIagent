using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
