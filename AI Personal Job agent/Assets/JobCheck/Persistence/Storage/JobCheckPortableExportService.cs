using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JobCheck.Domain;

namespace JobCheck.Persistence
{
    /// <summary>只讀取現有資料，產生可攜的單一檔案；不覆蓋既有匯出檔。</summary>
    public static class JobCheckPortableExportService
    {
        public static PersistenceStorageResult<JobCheckPortableExportSummary> Export(
            string dataRoot,
            string destinationPath)
        {
            var issues = new List<PersistenceStorageIssue>();
            string fullSourceRoot;
            string fullDestinationPath;
            try
            {
                if (string.IsNullOrWhiteSpace(dataRoot)
                    || string.IsNullOrWhiteSpace(destinationPath)
                    || !destinationPath.EndsWith(
                        JobCheckPortablePackageDto.FileExtension,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("匯出路徑須為 .jobcheck.json 檔案。");
                }

                fullSourceRoot = Path.GetFullPath(dataRoot);
                fullDestinationPath = Path.GetFullPath(destinationPath);
                string rootWithSeparator = fullSourceRoot.TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (fullDestinationPath.StartsWith(
                    rootWithSeparator,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("匯出檔不可放在原始資料目錄內。");
                }
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is NotSupportedException
                || exception is PathTooLongException)
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.IoFailure, destinationPath, null, exception.Message));
                return new PersistenceStorageResult<JobCheckPortableExportSummary>(null, issues);
            }

            if (File.Exists(fullDestinationPath) || Directory.Exists(fullDestinationPath))
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.DestinationNotEmpty,
                    fullDestinationPath, null, "匯出檔已存在，不會覆蓋。"));
                return new PersistenceStorageResult<JobCheckPortableExportSummary>(null, issues);
            }

            string destinationDirectory = Path.GetDirectoryName(fullDestinationPath);
            if (string.IsNullOrEmpty(destinationDirectory) || !Directory.Exists(destinationDirectory))
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.IoFailure,
                    fullDestinationPath, null, "匯出資料夾不存在。"));
                return new PersistenceStorageResult<JobCheckPortableExportSummary>(null, issues);
            }

            PersistenceStorageResult<JobCheckDataSet> load = JobCheckDataRepository.Load(fullSourceRoot);
            if (!load.IsSuccess)
            {
                return new PersistenceStorageResult<JobCheckPortableExportSummary>(null, load.Issues);
            }

            JobCheckPortablePackageDto package = BuildPackage(load.Value, issues);
            if (issues.Count > 0)
            {
                return new PersistenceStorageResult<JobCheckPortableExportSummary>(null, issues);
            }

            string temporaryPath = fullDestinationPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                package.content_sha256 = package.CalculateContentSha256();
                string json = PersistenceJsonSerializer.Serialize(package);
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
                File.Move(temporaryPath, fullDestinationPath);
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException || exception is NotSupportedException
                || exception is InvalidOperationException
                || exception is System.Runtime.Serialization.SerializationException)
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.IoFailure, fullDestinationPath, null, exception.Message));
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
                        issues.Add(new PersistenceStorageIssue(
                            PersistenceStorageError.IoFailure,
                            temporaryPath,
                            null,
                            "無法移除未完成的匯出暫存檔：" + exception.Message));
                    }
                }
            }

            return new PersistenceStorageResult<JobCheckPortableExportSummary>(
                issues.Count == 0
                    ? new JobCheckPortableExportSummary(
                        fullDestinationPath,
                        package.company_count,
                        package.job_count,
                        package.application_count)
                    : null,
                issues);
        }

        private static JobCheckPortablePackageDto BuildPackage(
            JobCheckDataSet dataSet,
            ICollection<PersistenceStorageIssue> issues)
        {
            var package = new JobCheckPortablePackageDto
            {
                format_version = JobCheckPortablePackageDto.CurrentFormatVersion,
                exported_at = DateTimeOffset.UtcNow.ToString("o"),
                company_count = dataSet.Companies.Count,
                job_count = dataSet.JobPostings.Count,
                application_count = dataSet.Applications.Count
            };

            foreach (Company company in dataSet.Companies.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                PersistenceConversionResult<CompanyDto> mapped = CompanyDtoMapper.ToDto(company);
                AddIssues(mapped.Issues, company.Id, issues);
                if (mapped.Value != null) package.companies.Add(mapped.Value);
            }

            foreach (JobPosting job in dataSet.JobPostings.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                PersistenceConversionResult<JobPostingDto> mapped = JobPostingDtoMapper.ToDto(job);
                AddIssues(mapped.Issues, job.Id, issues);
                if (mapped.Value != null) package.jobs.Add(mapped.Value);
            }

            var eventsByApplicationId = dataSet.ApplicationEvents
                .ToLookup(item => item.ApplicationId, StringComparer.Ordinal);
            foreach (Application application in dataSet.Applications.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                PersistenceConversionResult<ApplicationDto> mapped = ApplicationDtoMapper.ToDto(
                    application,
                    eventsByApplicationId[application.Id]
                        .OrderBy(item => item.Id, StringComparer.Ordinal));
                AddIssues(mapped.Issues, application.Id, issues);
                if (mapped.Value != null) package.applications.Add(mapped.Value);
            }

            return package;
        }

        private static void AddIssues(
            IEnumerable<PersistenceConversionIssue> conversionIssues,
            string entityId,
            ICollection<PersistenceStorageIssue> issues)
        {
            foreach (PersistenceConversionIssue issue in conversionIssues)
            {
                issues.Add(new PersistenceStorageIssue(
                    PersistenceStorageError.ConversionFailed,
                    entityId,
                    issue.FieldPath,
                    issue.Error.ToString()));
            }
        }
    }
}
