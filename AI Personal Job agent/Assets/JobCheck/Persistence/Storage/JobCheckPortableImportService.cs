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

    /// <summary>只讀取、驗證搬運檔；預覽失敗時不提供部分資料。</summary>
    public static class JobCheckPortableImportService
    {
        private const long MaximumPackageBytes = 64L * 1024 * 1024;

        public static PersistenceStorageResult<JobCheckPortableImportPreview> Preview(string packagePath)
        {
            var issues = new List<PersistenceStorageIssue>();
            string fullPath;
            try
            {
                if (string.IsNullOrWhiteSpace(packagePath)
                    || !packagePath.EndsWith(JobCheckPortablePackageDto.FileExtension,
                        StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("請選擇 .jobcheck.json 搬運檔。");
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
