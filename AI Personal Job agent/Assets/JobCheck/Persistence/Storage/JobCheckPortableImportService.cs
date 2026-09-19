using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using JobCheck.Domain;
using DomainApplication = JobCheck.Domain.Application;

namespace JobCheck.Persistence
{
    public sealed class JobCheckPortableImportPreview
    {
        public JobCheckPortableImportPreview(string path, DateTimeOffset exportedAt,
            JobCheckDataSet dataSet)
        {
            Path = path;
            ExportedAt = exportedAt;
            DataSet = dataSet;
        }

        public string Path { get; }
        public DateTimeOffset ExportedAt { get; }
        public int CompanyCount => DataSet.Companies.Count;
        public int JobCount => DataSet.JobPostings.Count;
        public int ApplicationCount => DataSet.Applications.Count;
        public int EventCount => DataSet.ApplicationEvents.Count;
        internal JobCheckDataSet DataSet { get; }
    }

    public sealed class JobCheckPortableImportSummary
    {
        public JobCheckPortableImportSummary(string destinationRoot,
            JobCheckPortableImportPreview preview, string cleanupWarning)
        {
            DestinationRoot = destinationRoot;
            CompanyCount = preview.CompanyCount;
            JobCount = preview.JobCount;
            ApplicationCount = preview.ApplicationCount;
            EventCount = preview.EventCount;
            CleanupWarning = cleanupWarning;
        }

        public string DestinationRoot { get; }
        public int CompanyCount { get; }
        public int JobCount { get; }
        public int ApplicationCount { get; }
        public int EventCount { get; }
        public string CleanupWarning { get; }
    }

    /// <summary>同一份個人資料的增量匯入預覽；同 ID 內容不同時不產生可執行計畫。</summary>
    public sealed class JobCheckPortableSyncPreview
    {
        internal JobCheckPortableSyncPreview(JobCheckPortableImportPreview source,
            JobCheckDataSet candidate, IReadOnlyList<string> newCompanyIds,
            IReadOnlyList<string> newJobIds, IReadOnlyList<string> newApplicationIds)
        {
            Source = source;
            Candidate = candidate;
            NewCompanyIds = newCompanyIds;
            NewJobIds = newJobIds;
            NewApplicationIds = newApplicationIds;
        }

        public JobCheckPortableImportPreview Source { get; }
        public int NewCompanyCount => NewCompanyIds.Count;
        public int NewJobCount => NewJobIds.Count;
        public int NewApplicationCount => NewApplicationIds.Count;
        public int NewEventCount => Source.DataSet.ApplicationEvents.Count(
            item => NewApplicationIds.Contains(item.ApplicationId));
        public string CleanupWarning { get; internal set; }
        internal JobCheckDataSet Candidate { get; }
        internal IReadOnlyList<string> NewCompanyIds { get; }
        internal IReadOnlyList<string> NewJobIds { get; }
        internal IReadOnlyList<string> NewApplicationIds { get; }
    }

    /// <summary>只讀取、驗證搬運檔；預覽失敗時不提供部分資料。</summary>
    public static class JobCheckPortableImportService
    {
        private const long MaximumPackageBytes = 64L * 1024 * 1024;

        /// <summary>供 UI 唯讀判斷目前個人資料目錄能否接收搬運檔。</summary>
        public static bool CanImportIntoEmptyRoot(string destinationRoot)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(destinationRoot)) return false;
                string fullTarget = Path.GetFullPath(destinationRoot);
                if (string.Equals(fullTarget, Path.GetPathRoot(fullTarget),
                    StringComparison.OrdinalIgnoreCase)) return false;
                return CanReplaceEmptyRoot(fullTarget);
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is NotSupportedException
                || exception is PathTooLongException || exception is IOException
                || exception is UnauthorizedAccessException)
            {
                return false;
            }
        }

        public static PersistenceStorageResult<JobCheckPortableImportPreview> Preview(string packagePath)
        {
            var issues = new List<PersistenceStorageIssue>();
            string fullPath;
            try
            {
                if (string.IsNullOrWhiteSpace(packagePath)
                    || !packagePath.EndsWith(JobCheckPortablePackageDto.FileExtension,
                        StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("請貼上以 .jobcheck.json 結尾的完整檔案路徑。");
                fullPath = System.IO.Path.GetFullPath(packagePath);
                var info = new FileInfo(fullPath);
                if (!info.Exists || info.Length == 0 || info.Length > MaximumPackageBytes)
                    throw new IOException("搬運檔不存在、為空，或超過 64 MB。");
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is NotSupportedException
                || exception is PathTooLongException || exception is IOException
                || exception is UnauthorizedAccessException)
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.IoFailure, packagePath, null, exception.Message));
                return new PersistenceStorageResult<JobCheckPortableImportPreview>(null, issues);
            }

            JobCheckPortablePackageDto package;
            try
            {
                string json = File.ReadAllText(fullPath, Encoding.UTF8);
                if (!PersistenceJsonSerializer.TryDeserialize(json, out package, out string error))
                {
                    issues.Add(new PersistenceStorageIssue(
                        PersistenceStorageError.InvalidJson, fullPath, null, error));
                    return new PersistenceStorageResult<JobCheckPortableImportPreview>(null, issues);
                }
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException)
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.IoFailure, fullPath, null, exception.Message));
                return new PersistenceStorageResult<JobCheckPortableImportPreview>(null, issues);
            }

            if (package.format_version != JobCheckPortablePackageDto.CurrentFormatVersion)
                AddIssue(issues, fullPath, "format_version", "不支援的搬運檔版本。",
                    PersistenceStorageError.UnsupportedSchemaVersion);
            if (!DateTimeOffset.TryParse(package.exported_at, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out DateTimeOffset exportedAt))
                AddIssue(issues, fullPath, "exported_at", "匯出時間格式無效。");
            if (package.companies == null || package.jobs == null || package.applications == null
                || package.company_count < 0 || package.job_count < 0
                || package.application_count < 0
                || package.company_count != package.companies.Count
                || package.job_count != package.jobs.Count
                || package.application_count != package.applications.Count)
                AddIssue(issues, fullPath, "counts", "資料筆數與內容不一致。");
            if (issues.Count > 0)
                return new PersistenceStorageResult<JobCheckPortableImportPreview>(null, issues);

            try
            {
                if (string.IsNullOrWhiteSpace(package.content_sha256)
                    || !string.Equals(package.content_sha256, package.CalculateContentSha256(),
                        StringComparison.Ordinal))
                    AddIssue(issues, fullPath, "content_sha256", "校驗碼不符；檔案可能已損壞或被修改。");
            }
            catch (Exception exception) when (
                exception is InvalidOperationException
                || exception is System.Runtime.Serialization.SerializationException
                || exception is ArgumentException)
            {
                AddIssue(issues, fullPath, "content_sha256", exception.Message);
            }
            if (issues.Count > 0)
                return new PersistenceStorageResult<JobCheckPortableImportPreview>(null, issues);

            var companies = new List<Company>();
            var jobs = new List<JobPosting>();
            var applications = new List<DomainApplication>();
            var events = new List<ApplicationEvent>();
            foreach (CompanyDto dto in package.companies)
            {
                if (dto == null || dto.schema_version != Company.CurrentSchemaVersion)
                {
                    AddIssue(issues, fullPath, "companies", "公司資料版本無效。");
                    continue;
                }
                PersistenceConversionResult<Company> result = CompanyDtoMapper.ToDomain(dto);
                AddMappingIssues(issues, fullPath, result.Issues);
                if (result.IsSuccess) companies.Add(result.Value);
            }
            foreach (JobPostingDto dto in package.jobs)
            {
                if (dto == null || dto.schema_version != Company.CurrentSchemaVersion)
                {
                    AddIssue(issues, fullPath, "jobs", "職缺資料版本無效。");
                    continue;
                }
                PersistenceConversionResult<JobPosting> result = JobPostingDtoMapper.ToDomain(dto);
                AddMappingIssues(issues, fullPath, result.Issues);
                if (result.IsSuccess) jobs.Add(result.Value);
            }
            foreach (ApplicationDto dto in package.applications)
            {
                if (dto == null || dto.schema_version != Company.CurrentSchemaVersion)
                {
                    AddIssue(issues, fullPath, "applications", "應徵資料版本無效。");
                    continue;
                }
                PersistenceConversionResult<ApplicationPersistenceBundle> result =
                    ApplicationDtoMapper.ToDomain(dto);
                AddMappingIssues(issues, fullPath, result.Issues);
                if (!result.IsSuccess) continue;
                applications.Add(result.Value.Application);
                events.AddRange(result.Value.Events);
            }
            if (issues.Count > 0)
                return new PersistenceStorageResult<JobCheckPortableImportPreview>(null, issues);

            var dataSet = new JobCheckDataSet(companies, jobs, applications, events);
            issues.AddRange(JobCheckDataRepository.ValidateSnapshot(dataSet));
            return new PersistenceStorageResult<JobCheckPortableImportPreview>(
                issues.Count == 0 ? new JobCheckPortableImportPreview(fullPath, exportedAt, dataSet) : null,
                issues);
        }

        /// <summary>唯讀比對：相同 ID 且內容相同略過；不同 ID 新增；同 ID 內容不同拒絕。</summary>
        public static PersistenceStorageResult<JobCheckPortableSyncPreview> PreviewSync(
            string packagePath, string destinationRoot)
        {
            PersistenceStorageResult<JobCheckPortableImportPreview> source = Preview(packagePath);
            if (!source.IsSuccess)
                return new PersistenceStorageResult<JobCheckPortableSyncPreview>(null, source.Issues);

            var issues = new List<PersistenceStorageIssue>();
            string target;
            try
            {
                if (string.IsNullOrWhiteSpace(destinationRoot))
                    throw new ArgumentException("個人資料目錄不可為空白。");
                target = Path.GetFullPath(destinationRoot);
                if (string.Equals(target, Path.GetPathRoot(target), StringComparison.OrdinalIgnoreCase)
                    || File.Exists(target)
                    || (Directory.Exists(target) &&
                        (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0))
                    throw new ArgumentException("個人資料目錄不是安全的資料目錄。");
                if (Directory.Exists(target))
                {
                    foreach (string directory in new[] {
                        JobCheckDataRepository.CompaniesDirectoryName,
                        JobCheckDataRepository.JobsDirectoryName,
                        JobCheckDataRepository.ApplicationsDirectoryName })
                    {
                        string child = Path.Combine(target, directory);
                        if (Directory.Exists(child) &&
                            (File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                            throw new ArgumentException("個人資料子目錄不可為連結：" + child);
                    }
                }
                if (source.Value.Path.StartsWith(target.TrimEnd(
                        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("搬運檔不可放在個人資料目錄內。");
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is NotSupportedException
                || exception is PathTooLongException || exception is IOException
                || exception is UnauthorizedAccessException)
            {
                AddIssue(issues, destinationRoot, null, exception.Message,
                    PersistenceStorageError.IoFailure);
                return new PersistenceStorageResult<JobCheckPortableSyncPreview>(null, issues);
            }

            JobCheckDataSet existing;
            if (Directory.Exists(target))
            {
                PersistenceStorageResult<JobCheckDataSet> loaded = JobCheckDataRepository.Load(target);
                if (!loaded.IsSuccess)
                    return new PersistenceStorageResult<JobCheckPortableSyncPreview>(null, loaded.Issues);
                existing = loaded.Value;
            }
            else existing = new JobCheckDataSet(null, null, null, null);

            JobCheckPortablePackageDto oldPackage =
                JobCheckPortableExportService.BuildPackage(existing, issues);
            JobCheckPortablePackageDto newPackage =
                JobCheckPortableExportService.BuildPackage(source.Value.DataSet, issues);
            if (issues.Count > 0)
                return new PersistenceStorageResult<JobCheckPortableSyncPreview>(null, issues);

            var newCompanies = new List<string>();
            var newJobs = new List<string>();
            var newApplications = new List<string>();
            CompareById(oldPackage.companies, newPackage.companies, item => item.id,
                "公司", source.Value.Path, newCompanies, issues);
            CompareById(oldPackage.jobs, newPackage.jobs, item => item.id,
                "職缺", source.Value.Path, newJobs, issues);
            // 應徵 DTO 包含完整事件歷程；新增事件或修改舊事件屬於同 ID 衝突。
            CompareById(oldPackage.applications, newPackage.applications, item => item.id,
                "應徵／事件", source.Value.Path, newApplications, issues);
            if (issues.Count > 0)
                return new PersistenceStorageResult<JobCheckPortableSyncPreview>(null, issues);

            var merged = new JobCheckDataSet(
                existing.Companies.Concat(source.Value.DataSet.Companies
                    .Where(item => newCompanies.Contains(item.Id))).ToList(),
                existing.JobPostings.Concat(source.Value.DataSet.JobPostings
                    .Where(item => newJobs.Contains(item.Id))).ToList(),
                existing.Applications.Concat(source.Value.DataSet.Applications
                    .Where(item => newApplications.Contains(item.Id))).ToList(),
                existing.ApplicationEvents.Concat(source.Value.DataSet.ApplicationEvents
                    .Where(item => newApplications.Contains(item.ApplicationId))).ToList());
            issues.AddRange(JobCheckDataRepository.ValidateSnapshot(merged));
            return new PersistenceStorageResult<JobCheckPortableSyncPreview>(
                issues.Count == 0 ? new JobCheckPortableSyncPreview(
                    source.Value, merged, newCompanies, newJobs, newApplications) : null, issues);
        }

        /// <summary>只發布新增的實體檔；不覆寫、刪除既有紀錄或 trash。</summary>
        public static PersistenceStorageResult<JobCheckPortableSyncPreview> Sync(
            string packagePath, string destinationRoot)
        {
            PersistenceStorageResult<JobCheckPortableSyncPreview> planned =
                PreviewSync(packagePath, destinationRoot);
            if (!planned.IsSuccess) return planned;
            JobCheckPortableSyncPreview plan = planned.Value;
            if (plan.NewCompanyCount + plan.NewJobCount + plan.NewApplicationCount == 0)
                return planned;

            string target = Path.GetFullPath(destinationRoot);
            if (CanReplaceEmptyRoot(target))
            {
                PersistenceStorageResult<JobCheckPortableImportSummary> restored =
                    Import(packagePath, target);
                if (restored.IsSuccess) plan.CleanupWarning = restored.Value.CleanupWarning;
                return new PersistenceStorageResult<JobCheckPortableSyncPreview>(
                    restored.IsSuccess ? plan : null, restored.Issues);
            }

            string staging = target + ".sync-staging-" + Guid.NewGuid().ToString("N");
            var issues = new List<PersistenceStorageIssue>();
            var moved = new List<string>();
            bool committed = false;
            try
            {
                PersistenceStorageResult<JobCheckDataSet> original =
                    JobCheckDataRepository.Load(target);
                if (!original.IsSuccess)
                    return new PersistenceStorageResult<JobCheckPortableSyncPreview>(null,
                        original.Issues);
                PersistenceStorageResult<PersistenceWriteSummary> staged =
                    JobCheckDataRepository.WriteSnapshot(staging, plan.Candidate);
                if (!staged.IsSuccess)
                    return new PersistenceStorageResult<JobCheckPortableSyncPreview>(null,
                        staged.Issues);
                PersistenceStorageResult<JobCheckDataSet> stagedLoad =
                    JobCheckDataRepository.Load(staging);
                if (!stagedLoad.IsSuccess || !SameContent(stagedLoad.Value, plan.Candidate))
                    throw new IOException("暫存資料重新讀取驗證失敗。");
                PersistenceStorageResult<JobCheckDataSet> current =
                    JobCheckDataRepository.Load(target);
                if (!current.IsSuccess || !SameContent(original.Value, current.Value))
                    throw new IOException("同步期間個人資料已變更，請重新預覽後再試。");

                MoveNewFiles(staging, target, JobCheckDataRepository.CompaniesDirectoryName,
                    plan.NewCompanyIds, moved);
                MoveNewFiles(staging, target, JobCheckDataRepository.JobsDirectoryName,
                    plan.NewJobIds, moved);
                MoveNewFiles(staging, target, JobCheckDataRepository.ApplicationsDirectoryName,
                    plan.NewApplicationIds, moved);
                PersistenceStorageResult<JobCheckDataSet> check =
                    JobCheckDataRepository.Load(target);
                if (!check.IsSuccess || !SameContent(check.Value, plan.Candidate))
                    throw new IOException("新增資料重新讀取驗證失敗。");
                committed = true;
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException || exception is NotSupportedException)
            {
                AddIssue(issues, target, null, exception.Message, PersistenceStorageError.IoFailure);
            }
            finally
            {
                if (!committed)
                {
                    for (int i = moved.Count - 1; i >= 0; i--)
                    {
                        try
                        {
                            string destination = moved[i];
                            string relative = destination.Substring(target.Length + 1);
                            string stagedPath = Path.Combine(staging, relative);
                            File.Move(destination, stagedPath);
                        }
                        catch (Exception exception) when (
                            exception is IOException || exception is UnauthorizedAccessException)
                        {
                            AddIssue(issues, moved[i], null,
                                "新增檔案回復失敗，請保留並檢查：" + exception.Message,
                                PersistenceStorageError.IoFailure);
                        }
                    }
                }
                if (Directory.Exists(staging) && (committed || moved.All(path => !File.Exists(path))))
                {
                    try { Directory.Delete(staging, true); }
                    catch (Exception exception) when (
                        exception is IOException || exception is UnauthorizedAccessException)
                    {
                        if (committed)
                            plan.CleanupWarning = "暫存目錄未能清理：" + staging;
                        else
                            AddIssue(issues, staging, null,
                                "暫存目錄未能清理：" + exception.Message,
                                PersistenceStorageError.IoFailure);
                    }
                }
            }
            return new PersistenceStorageResult<JobCheckPortableSyncPreview>(
                committed && issues.Count == 0 ? plan : null, issues);
        }

        private static void CompareById<T>(IEnumerable<T> existing, IEnumerable<T> incoming,
            Func<T, string> getId, string kind, string sourcePath,
            ICollection<string> additions, ICollection<PersistenceStorageIssue> issues)
            where T : class
        {
            Dictionary<string, T> byId = existing.ToDictionary(getId, StringComparer.Ordinal);
            foreach (T item in incoming)
            {
                string id = getId(item);
                if (!byId.TryGetValue(id, out T old)) additions.Add(id);
                else if (!string.Equals(PersistenceJsonSerializer.Serialize(old),
                    PersistenceJsonSerializer.Serialize(item), StringComparison.Ordinal))
                    AddIssue(issues, sourcePath, kind + "." + id,
                        kind + " ID 相同但內容不同；未覆蓋，請先處理衝突。");
            }
        }

        private static bool SameContent(JobCheckDataSet left, JobCheckDataSet right)
        {
            if (left == null || right == null) return false;
            var issues = new List<PersistenceStorageIssue>();
            JobCheckPortablePackageDto a = JobCheckPortableExportService.BuildPackage(left, issues);
            JobCheckPortablePackageDto b = JobCheckPortableExportService.BuildPackage(right, issues);
            if (issues.Count > 0) return false;
            a.exported_at = b.exported_at = null;
            return string.Equals(PersistenceJsonSerializer.Serialize(a),
                PersistenceJsonSerializer.Serialize(b), StringComparison.Ordinal);
        }

        private static void MoveNewFiles(string staging, string target, string directory,
            IEnumerable<string> ids, ICollection<string> moved)
        {
            string destination = Path.Combine(target, directory);
            if (Directory.Exists(destination) &&
                (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("個人資料子目錄不可為連結：" + destination);
            Directory.CreateDirectory(destination);
            foreach (string id in ids)
            {
                string path = Path.Combine(destination, id + ".json");
                File.Move(Path.Combine(staging, directory, id + ".json"), path);
                moved.Add(path);
            }
        }

        /// <summary>
        /// 只匯入到不存在或只有空白 V0.2 子目錄的個人資料區。
        /// 先寫入同層 staging 並重新驗證，再以目錄更名切換；絕不合併舊紀錄。
        /// </summary>
        public static PersistenceStorageResult<JobCheckPortableImportSummary> Import(
            string packagePath, string destinationRoot)
        {
            PersistenceStorageResult<JobCheckPortableImportPreview> preview = Preview(packagePath);
            if (!preview.IsSuccess)
                return new PersistenceStorageResult<JobCheckPortableImportSummary>(null, preview.Issues);

            var issues = new List<PersistenceStorageIssue>();
            string target;
            try
            {
                if (string.IsNullOrWhiteSpace(destinationRoot))
                    throw new ArgumentException("個人資料目錄不可為空白。");
                string fullTarget = Path.GetFullPath(destinationRoot);
                if (string.Equals(fullTarget, Path.GetPathRoot(fullTarget),
                    StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("不可將磁碟根目錄當作個人資料區。");
                target = fullTarget.TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string source = Path.GetFullPath(packagePath);
                if (source.StartsWith(target + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("搬運檔不可放在將被替換的目標資料目錄中。");
                if (!CanReplaceEmptyRoot(target))
                {
                    AddIssue(issues, target, null,
                        "個人資料區已有紀錄或其他檔案，為避免覆蓋已取消匯入。",
                        PersistenceStorageError.DestinationNotEmpty);
                    return new PersistenceStorageResult<JobCheckPortableImportSummary>(null, issues);
                }
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is NotSupportedException
                || exception is PathTooLongException || exception is IOException
                || exception is UnauthorizedAccessException)
            {
                AddIssue(issues, destinationRoot, null, exception.Message,
                    PersistenceStorageError.IoFailure);
                return new PersistenceStorageResult<JobCheckPortableImportSummary>(null, issues);
            }

            string parent = Path.GetDirectoryName(target);
            if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
            {
                AddIssue(issues, target, null, "目標資料夾的上層目錄不存在。",
                    PersistenceStorageError.IoFailure);
                return new PersistenceStorageResult<JobCheckPortableImportSummary>(null, issues);
            }

            string staging = target + ".import-staging-" + Guid.NewGuid().ToString("N");
            string emptyBackup = target + ".preimport-empty-" + Guid.NewGuid().ToString("N");
            bool movedExistingRoot = false;
            bool published = false;
            string cleanupWarning = null;
            try
            {
                PersistenceStorageResult<PersistenceWriteSummary> write =
                    JobCheckDataRepository.WriteSnapshot(staging, preview.Value.DataSet);
                if (!write.IsSuccess)
                    return new PersistenceStorageResult<JobCheckPortableImportSummary>(null, write.Issues);

                PersistenceStorageResult<JobCheckDataSet> check = JobCheckDataRepository.Load(staging);
                if (!check.IsSuccess
                    || check.Value.Companies.Count != preview.Value.CompanyCount
                    || check.Value.JobPostings.Count != preview.Value.JobCount
                    || check.Value.Applications.Count != preview.Value.ApplicationCount
                    || check.Value.ApplicationEvents.Count != preview.Value.EventCount)
                {
                    AddIssue(issues, staging, null, "暫存資料重新讀取驗證失敗。",
                        PersistenceStorageError.DataSetValidationFailed);
                    return new PersistenceStorageResult<JobCheckPortableImportSummary>(null, issues);
                }

                // 切換前再次確認，避免預覽期間目標資料被其他操作新增。
                if (!CanReplaceEmptyRoot(target))
                {
                    AddIssue(issues, target, null, "個人資料區已有新資料，已取消匯入。",
                        PersistenceStorageError.DestinationNotEmpty);
                    return new PersistenceStorageResult<JobCheckPortableImportSummary>(null, issues);
                }
                if (Directory.Exists(target))
                {
                    Directory.Move(target, emptyBackup);
                    movedExistingRoot = true;
                }
                Directory.Move(staging, target);
                published = true;
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException || exception is NotSupportedException)
            {
                AddIssue(issues, target, null, exception.Message, PersistenceStorageError.IoFailure);
            }
            finally
            {
                if (!published && movedExistingRoot && !Directory.Exists(target))
                {
                    try { Directory.Move(emptyBackup, target); }
                    catch (Exception exception) when (
                        exception is IOException || exception is UnauthorizedAccessException)
                    {
                        AddIssue(issues, emptyBackup, null,
                            "無法自動復原空白目錄：" + exception.Message,
                            PersistenceStorageError.IoFailure);
                    }
                }
                if (Directory.Exists(staging))
                {
                    try { Directory.Delete(staging, true); }
                    catch (Exception exception) when (
                        exception is IOException || exception is UnauthorizedAccessException)
                    {
                        if (published) cleanupWarning = "暫存目錄清理失敗：" + staging;
                        else AddIssue(issues, staging, null,
                            "暫存目錄清理失敗：" + exception.Message,
                            PersistenceStorageError.IoFailure);
                    }
                }
            }

            if (!published)
                return new PersistenceStorageResult<JobCheckPortableImportSummary>(null, issues);
            if (movedExistingRoot)
            {
                try { DeleteEmptyRoot(emptyBackup); }
                catch (Exception exception) when (
                    exception is IOException || exception is UnauthorizedAccessException)
                {
                    cleanupWarning = "舊空白目錄未能清理：" + emptyBackup + "（" + exception.Message + "）";
                }
            }
            return new PersistenceStorageResult<JobCheckPortableImportSummary>(
                new JobCheckPortableImportSummary(target, preview.Value, cleanupWarning),
                Array.Empty<PersistenceStorageIssue>());
        }

        private static bool CanReplaceEmptyRoot(string root)
        {
            if (!Directory.Exists(root)) return !File.Exists(root);
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) return false;
            string[] expected = {
                JobCheckDataRepository.CompaniesDirectoryName,
                JobCheckDataRepository.JobsDirectoryName,
                JobCheckDataRepository.ApplicationsDirectoryName
            };
            foreach (string entry in Directory.GetFileSystemEntries(root))
            {
                if (!Directory.Exists(entry)
                    || (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0
                    || !expected.Contains(Path.GetFileName(entry), StringComparer.Ordinal)
                    || Directory.EnumerateFileSystemEntries(entry).Any())
                    return false;
            }
            return true;
        }

        private static void DeleteEmptyRoot(string root)
        {
            if (!CanReplaceEmptyRoot(root))
                throw new IOException("備份目錄已不再是空白，未清理。");
            foreach (string child in Directory.GetDirectories(root)) Directory.Delete(child, false);
            Directory.Delete(root, false);
        }

        private static void AddMappingIssues(ICollection<PersistenceStorageIssue> issues,
            string path, IEnumerable<PersistenceConversionIssue> mappingIssues)
        {
            foreach (PersistenceConversionIssue issue in mappingIssues)
                AddIssue(issues, path, issue.FieldPath, issue.Error.ToString(),
                    PersistenceStorageError.ConversionFailed);
        }

        private static void AddIssue(ICollection<PersistenceStorageIssue> issues,
            string path, string field, string message,
            PersistenceStorageError error = PersistenceStorageError.DataSetValidationFailed)
        {
            issues.Add(new PersistenceStorageIssue(error, path, field, message));
        }
    }
}
