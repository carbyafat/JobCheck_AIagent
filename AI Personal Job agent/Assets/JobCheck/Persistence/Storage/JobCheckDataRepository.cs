using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JobCheck.Domain;

namespace JobCheck.Persistence
{
    /// <summary>
    /// V0.2 本機資料的唯一檔案邊界。
    /// 讀取不會建立或修改檔案；整批寫入只允許輸出到空目錄，供 migration staging 使用。
    /// </summary>
    public static class JobCheckDataRepository
    {
        public const string CompaniesDirectoryName = "companies";
        public const string JobsDirectoryName = "jobs";
        public const string ApplicationsDirectoryName = "applications";

        /// <summary>
        /// 從 V0.2 data 根目錄載入完整資料集。任一檔案失敗時不回傳半套資料。
        /// </summary>
        public static PersistenceStorageResult<JobCheckDataSet> Load(string dataRoot)
        {
            var issues = new List<PersistenceStorageIssue>();
            var companies = new List<Company>();
            var jobPostings = new List<JobPosting>();
            var applications = new List<Application>();
            var applicationEvents = new List<ApplicationEvent>();

            if (string.IsNullOrWhiteSpace(dataRoot) || !Directory.Exists(dataRoot))
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.DataRootNotFound,
                    dataRoot,
                    null,
                    "找不到 V0.2 data 根目錄；讀取操作不會自動建立目錄。"));
                return new PersistenceStorageResult<JobCheckDataSet>(null, issues);
            }

            ReadCompanies(Path.Combine(dataRoot, CompaniesDirectoryName), companies, issues);
            ReadJobPostings(Path.Combine(dataRoot, JobsDirectoryName), jobPostings, issues);
            ReadApplications(
                Path.Combine(dataRoot, ApplicationsDirectoryName),
                applications,
                applicationEvents,
                issues);

            var dataSet = new JobCheckDataSet(
                companies,
                jobPostings,
                applications,
                applicationEvents);
            AddContentValidationIssues(dataSet, issues);

            return new PersistenceStorageResult<JobCheckDataSet>(dataSet, issues);
        }

        /// <summary>
        /// 原子寫入一筆 Application 及其完整事件歷史。
        /// 寫入前會把候選內容放回完整資料集驗證；通過後才以同目錄暫存檔替換正式 JSON。
        /// </summary>
        public static PersistenceStorageResult<ApplicationWriteSummary> SaveApplication(
            string dataRoot,
            Application application,
            IEnumerable<ApplicationEvent> events)
        {
            var issues = new List<PersistenceStorageIssue>();
            PersistenceStorageResult<JobCheckDataSet> load = Load(dataRoot);
            if (!load.IsSuccess)
            {
                return new PersistenceStorageResult<ApplicationWriteSummary>(null, load.Issues);
            }

            var eventSnapshot = events == null
                ? new List<ApplicationEvent>()
                : events.ToList();
            var applications = load.Value.Applications
                .Where(item => !string.Equals(item.Id, application?.Id, StringComparison.Ordinal))
                .ToList();
            applications.Add(application);

            var applicationEvents = load.Value.ApplicationEvents
                .Where(item => !string.Equals(
                    item.ApplicationId,
                    application?.Id,
                    StringComparison.Ordinal))
                .Concat(eventSnapshot)
                .ToList();
            var candidateDataSet = new JobCheckDataSet(
                load.Value.Companies,
                load.Value.JobPostings,
                applications,
                applicationEvents);
            AddContentValidationIssues(candidateDataSet, issues);

            PersistenceConversionResult<ApplicationDto> conversion =
                ApplicationDtoMapper.ToDto(application, eventSnapshot);
            string applicationPath = BuildEntityPath(
                dataRoot,
                ApplicationsDirectoryName,
                application?.Id,
                issues);
            AddConversionIssues(applicationPath, conversion.Issues, issues);
            if (issues.Count > 0 || conversion.Value == null || applicationPath == null)
            {
                return new PersistenceStorageResult<ApplicationWriteSummary>(null, issues);
            }

            bool created = !File.Exists(applicationPath);
            try
            {
                string directory = Path.GetDirectoryName(applicationPath);
                Directory.CreateDirectory(directory);
                string json = PersistenceJsonSerializer.Serialize(conversion.Value);
                WriteOrReplaceFileAtomically(applicationPath, json);
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is ArgumentException
                || exception is NotSupportedException
                || exception is InvalidOperationException
                || exception is System.Runtime.Serialization.SerializationException)
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.IoFailure,
                    applicationPath,
                    null,
                    exception.Message));
                return new PersistenceStorageResult<ApplicationWriteSummary>(null, issues);
            }

            return new PersistenceStorageResult<ApplicationWriteSummary>(
                new ApplicationWriteSummary(application.Id, eventSnapshot.Count, created),
                issues);
        }

        /// <summary>
        /// 寫入一筆全新的 JobPosting，並可同時建立它所屬的新 Company。
        /// 所有驗證與序列化會在碰觸正式檔案前完成；第二個檔案失敗時會回復本次已建立的檔案。
        /// </summary>
        public static PersistenceStorageResult<JobPostingWriteSummary> SaveNewJobPosting(
            string dataRoot,
            Company newCompany,
            JobPosting jobPosting)
        {
            var issues = new List<PersistenceStorageIssue>();
            PersistenceStorageResult<JobCheckDataSet> load = Load(dataRoot);
            if (!load.IsSuccess)
            {
                return new PersistenceStorageResult<JobPostingWriteSummary>(null, load.Issues);
            }

            if (jobPosting == null)
            {
                issues.Add(CreateEntityIssue(null, "JobPosting 不可為 null。"));
                return new PersistenceStorageResult<JobPostingWriteSummary>(null, issues);
            }

            if (load.Value.JobPostings.Any(item => string.Equals(
                item.Id,
                jobPosting.Id,
                StringComparison.Ordinal)))
            {
                issues.Add(CreateEntityIssue(jobPosting.Id, "JobPosting ID 已存在。"));
            }

            var companies = load.Value.Companies.ToList();
            bool companyCreated = newCompany != null;
            if (companyCreated)
            {
                if (companies.Any(item => string.Equals(
                    item.Id,
                    newCompany.Id,
                    StringComparison.Ordinal)))
                {
                    issues.Add(CreateEntityIssue(newCompany.Id, "Company ID 已存在。"));
                }
                else
                {
                    companies.Add(newCompany);
                }
            }
            else if (!companies.Any(item => string.Equals(
                item.Id,
                jobPosting.CompanyId,
                StringComparison.Ordinal)))
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.DataSetValidationFailed,
                    jobPosting.Id,
                    "company_id",
                    "JobPosting 參照的 Company 不存在。"));
            }

            var jobs = load.Value.JobPostings.ToList();
            jobs.Add(jobPosting);
            var candidateDataSet = new JobCheckDataSet(
                companies,
                jobs,
                load.Value.Applications,
                load.Value.ApplicationEvents);
            AddContentValidationIssues(candidateDataSet, issues);

            PersistenceConversionResult<JobPostingDto> jobConversion =
                JobPostingDtoMapper.ToDto(jobPosting);
            string jobPath = BuildEntityPath(
                dataRoot,
                JobsDirectoryName,
                jobPosting.Id,
                issues);
            AddConversionIssues(jobPath, jobConversion.Issues, issues);

            PersistenceConversionResult<CompanyDto> companyConversion = null;
            string companyPath = null;
            if (companyCreated)
            {
                companyConversion = CompanyDtoMapper.ToDto(newCompany);
                companyPath = BuildEntityPath(
                    dataRoot,
                    CompaniesDirectoryName,
                    newCompany.Id,
                    issues);
                AddConversionIssues(companyPath, companyConversion.Issues, issues);
            }

            if (issues.Count > 0
                || jobConversion.Value == null
                || jobPath == null
                || (companyCreated && (companyConversion.Value == null || companyPath == null)))
            {
                return new PersistenceStorageResult<JobPostingWriteSummary>(null, issues);
            }

            var createdPaths = new List<string>();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(jobPath));
                if (companyCreated)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(companyPath));
                    WriteNewFileAtomically(
                        companyPath,
                        PersistenceJsonSerializer.Serialize(companyConversion.Value));
                    createdPaths.Add(companyPath);
                }

                WriteNewFileAtomically(
                    jobPath,
                    PersistenceJsonSerializer.Serialize(jobConversion.Value));
                createdPaths.Add(jobPath);
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is ArgumentException
                || exception is NotSupportedException
                || exception is InvalidOperationException
                || exception is System.Runtime.Serialization.SerializationException)
            {
                RollBackCreatedFiles(createdPaths, issues);
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.IoFailure,
                    jobPath,
                    null,
                    exception.Message));
                return new PersistenceStorageResult<JobPostingWriteSummary>(null, issues);
            }

            return new PersistenceStorageResult<JobPostingWriteSummary>(
                new JobPostingWriteSummary(
                    jobPosting.CompanyId,
                    jobPosting.Id,
                    companyCreated),
                issues);
        }

        /// <summary>
        /// 以相同 ID 更新既有 JobPosting，並可同時建立它新改連的 Company。
        /// 寫入前會用候選資料集完成單體、關聯與序列化驗證；新增 Company 後若職缺替換失敗，
        /// 會移除本次建立的 Company，原職缺檔案則維持不變。
        /// </summary>
        public static PersistenceStorageResult<JobPostingWriteSummary> UpdateJobPosting(
            string dataRoot,
            Company newCompany,
            JobPosting jobPosting)
        {
            var issues = new List<PersistenceStorageIssue>();
            PersistenceStorageResult<JobCheckDataSet> load = Load(dataRoot);
            if (!load.IsSuccess)
            {
                return new PersistenceStorageResult<JobPostingWriteSummary>(null, load.Issues);
            }

            if (jobPosting == null)
            {
                issues.Add(CreateEntityIssue(null, "JobPosting 不可為 null。"));
                return new PersistenceStorageResult<JobPostingWriteSummary>(null, issues);
            }

            int existingIndex = load.Value.JobPostings
                .ToList()
                .FindIndex(item => item != null && string.Equals(
                    item.Id,
                    jobPosting.Id,
                    StringComparison.Ordinal));
            if (existingIndex < 0)
            {
                issues.Add(CreateEntityIssue(jobPosting.Id, "找不到要更新的 JobPosting。"));
                return new PersistenceStorageResult<JobPostingWriteSummary>(null, issues);
            }

            var companies = load.Value.Companies.ToList();
            bool companyCreated = newCompany != null;
            if (companyCreated)
            {
                if (companies.Any(item => string.Equals(
                    item.Id,
                    newCompany.Id,
                    StringComparison.Ordinal)))
                {
                    issues.Add(CreateEntityIssue(newCompany.Id, "Company ID 已存在。"));
                }
                else
                {
                    companies.Add(newCompany);
                }
            }
            else if (!companies.Any(item => string.Equals(
                item.Id,
                jobPosting.CompanyId,
                StringComparison.Ordinal)))
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.DataSetValidationFailed,
                    jobPosting.Id,
                    "company_id",
                    "JobPosting 參照的 Company 不存在。"));
            }

            var jobs = load.Value.JobPostings.ToList();
            jobs[existingIndex] = jobPosting;
            var candidateDataSet = new JobCheckDataSet(
                companies,
                jobs,
                load.Value.Applications,
                load.Value.ApplicationEvents);
            AddContentValidationIssues(candidateDataSet, issues);

            PersistenceConversionResult<JobPostingDto> jobConversion =
                JobPostingDtoMapper.ToDto(jobPosting);
            string jobPath = BuildEntityPath(
                dataRoot,
                JobsDirectoryName,
                jobPosting.Id,
                issues);
            AddConversionIssues(jobPath, jobConversion.Issues, issues);

            PersistenceConversionResult<CompanyDto> companyConversion = null;
            string companyPath = null;
            if (companyCreated)
            {
                companyConversion = CompanyDtoMapper.ToDto(newCompany);
                companyPath = BuildEntityPath(
                    dataRoot,
                    CompaniesDirectoryName,
                    newCompany.Id,
                    issues);
                AddConversionIssues(companyPath, companyConversion.Issues, issues);
            }

            if (issues.Count > 0
                || jobConversion.Value == null
                || jobPath == null
                || (companyCreated && (companyConversion.Value == null || companyPath == null)))
            {
                return new PersistenceStorageResult<JobPostingWriteSummary>(null, issues);
            }

            var createdPaths = new List<string>();
            try
            {
                if (companyCreated)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(companyPath));
                    WriteNewFileAtomically(
                        companyPath,
                        PersistenceJsonSerializer.Serialize(companyConversion.Value));
                    createdPaths.Add(companyPath);
                }

                WriteOrReplaceFileAtomically(
                    jobPath,
                    PersistenceJsonSerializer.Serialize(jobConversion.Value));
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is ArgumentException
                || exception is NotSupportedException
                || exception is InvalidOperationException
                || exception is System.Runtime.Serialization.SerializationException)
            {
                RollBackCreatedFiles(createdPaths, issues);
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.IoFailure,
                    jobPath,
                    null,
                    exception.Message));
                return new PersistenceStorageResult<JobPostingWriteSummary>(null, issues);
            }

            return new PersistenceStorageResult<JobPostingWriteSummary>(
                new JobPostingWriteSummary(
                    jobPosting.CompanyId,
                    jobPosting.Id,
                    companyCreated),
                issues);
        }

        /// <summary>
        /// 將完整資料集寫入不存在或完全空白的目錄。
        /// 此方法不覆蓋既有正式資料，適合作為 migration 的 staging 輸出。
        /// </summary>
        public static PersistenceStorageResult<PersistenceWriteSummary> WriteSnapshot(
            string destinationRoot,
            JobCheckDataSet dataSet)
        {
            var issues = new List<PersistenceStorageIssue>();
            if (string.IsNullOrWhiteSpace(destinationRoot))
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.IoFailure,
                    destinationRoot,
                    null,
                    "輸出目錄不可為空白。"));
                return new PersistenceStorageResult<PersistenceWriteSummary>(null, issues);
            }

            if (dataSet == null)
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.EntityValidationFailed,
                    destinationRoot,
                    null,
                    "不可寫入 null 資料集。"));
                return new PersistenceStorageResult<PersistenceWriteSummary>(null, issues);
            }

            AddContentValidationIssues(dataSet, issues);
            var serializedFiles = BuildSerializedFiles(destinationRoot, dataSet, issues);
            if (issues.Count > 0)
            {
                return new PersistenceStorageResult<PersistenceWriteSummary>(null, issues);
            }

            if (Directory.Exists(destinationRoot)
                && Directory.EnumerateFileSystemEntries(destinationRoot).Any())
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.DestinationNotEmpty,
                    destinationRoot,
                    null,
                    "為避免覆蓋正式資料，整批快照只能寫入空目錄。"));
                return new PersistenceStorageResult<PersistenceWriteSummary>(null, issues);
            }

            try
            {
                Directory.CreateDirectory(destinationRoot);
                Directory.CreateDirectory(Path.Combine(destinationRoot, CompaniesDirectoryName));
                Directory.CreateDirectory(Path.Combine(destinationRoot, JobsDirectoryName));
                Directory.CreateDirectory(Path.Combine(destinationRoot, ApplicationsDirectoryName));

                foreach (KeyValuePair<string, string> file in serializedFiles)
                {
                    WriteNewFileAtomically(file.Key, file.Value);
                }
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is ArgumentException
                || exception is NotSupportedException)
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.IoFailure,
                    destinationRoot,
                    null,
                    exception.Message));
                return new PersistenceStorageResult<PersistenceWriteSummary>(null, issues);
            }

            return new PersistenceStorageResult<PersistenceWriteSummary>(
                new PersistenceWriteSummary(
                    dataSet.Companies.Count,
                    dataSet.JobPostings.Count,
                    dataSet.Applications.Count),
                issues);
        }

        /// <summary>唯讀驗證一份資料是否可安全地寫成 V0.2 快照。</summary>
        public static IReadOnlyList<PersistenceStorageIssue> ValidateSnapshot(JobCheckDataSet dataSet)
        {
            var issues = new List<PersistenceStorageIssue>();
            if (dataSet == null)
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.EntityValidationFailed, null, null,
                    "不可驗證 null 資料集。"));
                return issues.AsReadOnly();
            }

            AddContentValidationIssues(dataSet, issues);
            if (issues.Count == 0)
                BuildSerializedFiles("validation-only", dataSet, issues);
            return issues.AsReadOnly();
        }

        /// <summary>
        /// 先完成所有轉換與序列化，確定沒有錯誤後才建立任何輸出檔案。
        /// </summary>
        private static Dictionary<string, string> BuildSerializedFiles(
            string destinationRoot,
            JobCheckDataSet dataSet,
            ICollection<PersistenceStorageIssue> issues)
        {
            var files = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (Company company in dataSet.Companies)
            {
                string path = BuildEntityPath(
                    destinationRoot, CompaniesDirectoryName, company?.Id, issues);
                PersistenceConversionResult<CompanyDto> conversion = CompanyDtoMapper.ToDto(company);
                AddConversionIssues(path, conversion.Issues, issues);
                AddSerializedFile(path, conversion.Value, files, issues);
            }

            foreach (JobPosting jobPosting in dataSet.JobPostings)
            {
                string path = BuildEntityPath(
                    destinationRoot, JobsDirectoryName, jobPosting?.Id, issues);
                PersistenceConversionResult<JobPostingDto> conversion =
                    JobPostingDtoMapper.ToDto(jobPosting);
                AddConversionIssues(path, conversion.Issues, issues);
                AddSerializedFile(path, conversion.Value, files, issues);
            }

            foreach (Application application in dataSet.Applications)
            {
                string path = BuildEntityPath(
                    destinationRoot, ApplicationsDirectoryName, application?.Id, issues);
                IEnumerable<ApplicationEvent> ownedEvents = dataSet.ApplicationEvents.Where(
                    item => item != null
                        && string.Equals(
                            item.ApplicationId,
                            application?.Id,
                            StringComparison.Ordinal));
                PersistenceConversionResult<ApplicationDto> conversion =
                    ApplicationDtoMapper.ToDto(application, ownedEvents);
                AddConversionIssues(path, conversion.Issues, issues);
                AddSerializedFile(path, conversion.Value, files, issues);
            }

            return files;
        }

        private static void AddSerializedFile<T>(
            string path,
            T dto,
            IDictionary<string, string> files,
            ICollection<PersistenceStorageIssue> issues)
            where T : class
        {
            if (path == null || dto == null)
            {
                return;
            }

            try
            {
                files.Add(path, PersistenceJsonSerializer.Serialize(dto));
            }
            catch (Exception exception) when (
                exception is InvalidOperationException
                || exception is System.Runtime.Serialization.SerializationException
                || exception is ArgumentException)
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.ConversionFailed,
                    path,
                    null,
                    exception.Message));
            }
        }

        private static void ReadCompanies(
            string directory,
            ICollection<Company> companies,
            ICollection<PersistenceStorageIssue> issues)
        {
            foreach (string path in EnumerateJsonFiles(directory, issues))
            {
                if (!TryReadDto(path, out CompanyDto dto, issues)
                    || !HasSupportedSchema(dto.schema_version, path, issues))
                {
                    continue;
                }

                PersistenceConversionResult<Company> result = CompanyDtoMapper.ToDomain(dto);
                AddConversionIssues(path, result.Issues, issues);
                if (result.IsSuccess && HasMatchingFileName(path, result.Value.Id, issues))
                {
                    companies.Add(result.Value);
                }
            }
        }

        private static void ReadJobPostings(
            string directory,
            ICollection<JobPosting> jobPostings,
            ICollection<PersistenceStorageIssue> issues)
        {
            foreach (string path in EnumerateJsonFiles(directory, issues))
            {
                if (!TryReadDto(path, out JobPostingDto dto, issues)
                    || !HasSupportedSchema(dto.schema_version, path, issues))
                {
                    continue;
                }

                PersistenceConversionResult<JobPosting> result = JobPostingDtoMapper.ToDomain(dto);
                AddConversionIssues(path, result.Issues, issues);
                if (result.IsSuccess && HasMatchingFileName(path, result.Value.Id, issues))
                {
                    jobPostings.Add(result.Value);
                }
            }
        }

        private static void ReadApplications(
            string directory,
            ICollection<Application> applications,
            ICollection<ApplicationEvent> applicationEvents,
            ICollection<PersistenceStorageIssue> issues)
        {
            foreach (string path in EnumerateJsonFiles(directory, issues))
            {
                if (!TryReadDto(path, out ApplicationDto dto, issues)
                    || !HasSupportedSchema(dto.schema_version, path, issues))
                {
                    continue;
                }

                PersistenceConversionResult<ApplicationPersistenceBundle> result =
                    ApplicationDtoMapper.ToDomain(dto);
                AddConversionIssues(path, result.Issues, issues);
                if (result.IsSuccess
                    && HasMatchingFileName(path, result.Value.Application.Id, issues))
                {
                    applications.Add(result.Value.Application);
                    foreach (ApplicationEvent item in result.Value.Events)
                    {
                        applicationEvents.Add(item);
                    }
                }
            }
        }

        private static IEnumerable<string> EnumerateJsonFiles(
            string directory,
            ICollection<PersistenceStorageIssue> issues)
        {
            if (!Directory.Exists(directory))
            {
                return Array.Empty<string>();
            }

            try
            {
                return Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException)
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.IoFailure,
                    directory,
                    null,
                    exception.Message));
                return Array.Empty<string>();
            }
        }

        private static bool TryReadDto<T>(
            string path,
            out T dto,
            ICollection<PersistenceStorageIssue> issues)
            where T : class
        {
            dto = null;
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (PersistenceJsonSerializer.TryDeserialize(json, out dto, out string error))
                {
                    return true;
                }

                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.InvalidJson,
                    path,
                    null,
                    error));
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException)
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.IoFailure,
                    path,
                    null,
                    exception.Message));
            }

            return false;
        }

        private static bool HasSupportedSchema(
            string schemaVersion,
            string path,
            ICollection<PersistenceStorageIssue> issues)
        {
            if (string.Equals(schemaVersion, Company.CurrentSchemaVersion, StringComparison.Ordinal))
            {
                return true;
            }

            issues.Add(new PersistenceStorageIssue(
                PersistenceStorageError.UnsupportedSchemaVersion,
                path,
                "schema_version",
                "只接受 schema_version 0.2，實際值為：" + (schemaVersion ?? "<null>")));
            return false;
        }

        private static bool HasMatchingFileName(
            string path,
            string entityId,
            ICollection<PersistenceStorageIssue> issues)
        {
            string fileId = Path.GetFileNameWithoutExtension(path);
            if (string.Equals(fileId, entityId, StringComparison.Ordinal))
            {
                return true;
            }

            issues.Add(new PersistenceStorageIssue(
                PersistenceStorageError.FileNameIdMismatch,
                path,
                "id",
                "檔名必須與實體 ID 完全相同。"));
            return false;
        }

        private static string BuildEntityPath(
            string root,
            string directoryName,
            string id,
            ICollection<PersistenceStorageIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(id)
                || id == "."
                || id == ".."
                || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || id.Contains("/")
                || id.Contains("\\"))
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.UnsafeEntityId,
                    root,
                    "id",
                    "實體 ID 不可用作安全檔名：" + (id ?? "<null>")));
                return null;
            }

            return Path.Combine(root, directoryName, id + ".json");
        }

        private static void AddConversionIssues(
            string path,
            IEnumerable<PersistenceConversionIssue> conversionIssues,
            ICollection<PersistenceStorageIssue> issues)
        {
            foreach (PersistenceConversionIssue issue in conversionIssues)
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.ConversionFailed,
                    path,
                    issue.FieldPath,
                    issue.Error + (issue.RawValue == null ? string.Empty : ": " + issue.RawValue)));
            }
        }

        /// <summary>
        /// 執行單體與跨集合驗證。Repository 不自行修補任何錯誤內容。
        /// </summary>
        private static void AddContentValidationIssues(
            JobCheckDataSet dataSet,
            ICollection<PersistenceStorageIssue> issues)
        {
            foreach (Company company in dataSet.Companies)
            {
                if (company == null
                    || string.IsNullOrWhiteSpace(company.Id)
                    || !string.Equals(company.SchemaVersion, Company.CurrentSchemaVersion, StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(company.Name))
                {
                    issues.Add(new PersistenceStorageIssue(
                        PersistenceStorageError.EntityValidationFailed,
                        company?.Id,
                        null,
                        "Company 必須包含 id、schema_version 0.2 與 name。"));
                }
            }

            foreach (JobPosting jobPosting in dataSet.JobPostings)
            {
                foreach (JobPostingValidationError error in
                    JobPostingValidator.ValidateForMigration(jobPosting))
                {
                    issues.Add(CreateEntityIssue(
                        jobPosting?.Id,
                        ValidationErrorLocalizer.ToChinese(error)));
                }
            }

            var applicationsById = dataSet.Applications
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Id))
                .GroupBy(item => item.Id, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

            foreach (Application application in dataSet.Applications)
            {
                foreach (ApplicationValidationError error in
                    ApplicationValidator.ValidateForMigration(application))
                {
                    issues.Add(CreateEntityIssue(
                        application?.Id,
                        ValidationErrorLocalizer.ToChinese(error)));
                }
            }

            foreach (ApplicationEvent applicationEvent in dataSet.ApplicationEvents)
            {
                DateTimeOffset createdAt = DateTimeOffset.MinValue;
                if (applicationEvent != null
                    && applicationsById.TryGetValue(applicationEvent.ApplicationId, out Application owner)
                    && owner.CreatedAt.HasValue)
                {
                    createdAt = owner.CreatedAt.Value;
                }

                foreach (ApplicationEventValidationError error in
                    ApplicationEventValidator.ValidateForMigration(applicationEvent, createdAt))
                {
                    issues.Add(CreateEntityIssue(
                        applicationEvent?.Id,
                        ValidationErrorLocalizer.ToChinese(error)));
                }
            }

            JobCheckDataSetValidationResult dataSetResult =
                JobCheckDataSetValidator.Validate(dataSet);
            foreach (JobCheckDataSetValidationIssue issue in dataSetResult.Issues)
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.DataSetValidationFailed,
                    issue.EntityId,
                    issue.FieldName,
                    issue.Error.ToString()));
            }
        }

        private static PersistenceStorageIssue CreateEntityIssue(
            string entityId,
            string message)
        {
            return new PersistenceStorageIssue(
                PersistenceStorageError.EntityValidationFailed,
                entityId,
                null,
                message);
        }

        private static void WriteNewFileAtomically(string path, string json)
        {
            string temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
                File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static void RollBackCreatedFiles(
            IEnumerable<string> paths,
            ICollection<PersistenceStorageIssue> issues)
        {
            foreach (string path in paths.Reverse())
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception exception) when (
                    exception is IOException || exception is UnauthorizedAccessException)
                {
                    issues.Add(new PersistenceStorageIssue(
                        PersistenceStorageError.IoFailure,
                        path,
                        null,
                        "回復未完成寫入時無法移除檔案：" + exception.Message));
                }
            }
        }

        /// <summary>
        /// 在目標檔案旁寫完暫存檔，再以單一步驟新增或替換正式檔。
        /// </summary>
        private static void WriteOrReplaceFileAtomically(string path, string json)
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
