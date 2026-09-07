using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using JobCheck.Domain;

namespace JobCheck.Persistence
{
    /// <summary>
    /// 將儲存庫根目錄的 V0.1 demo golden data 安全轉換為 V0.2 data 目錄。
    /// 舊 jobs、job_tracking、job_index 永遠只讀；完成前只在獨立 work/staging 目錄工作。
    /// </summary>
    public static class JobCheckV01Migration
    {
        public const string MigrationVersion = "v0.1-to-v0.2-demo-1";

        /// <summary>
        /// 執行一次 demo migration。targetDataRoot 必須尚未存在或為空目錄。
        /// </summary>
        public static JobCheckMigrationResult Run(
            string sourceRoot,
            string targetDataRoot,
            DateTimeOffset executedAt)
        {
            var issues = new List<JobCheckMigrationIssue>();
            string fullSourceRoot = GetFullPath(sourceRoot, issues);
            string fullTargetRoot = GetFullPath(targetDataRoot, issues);
            if (HasErrors(issues))
            {
                return CreateFailure(null, null, issues);
            }

            if (!Directory.Exists(fullSourceRoot))
            {
                AddError(issues, JobCheckMigrationError.SourceDirectoryNotFound,
                    fullSourceRoot, null, "找不到 V0.1 來源根目錄。");
                return CreateFailure(null, null, issues);
            }

            if (Directory.Exists(fullTargetRoot)
                && Directory.EnumerateFileSystemEntries(fullTargetRoot).Any())
            {
                AddError(issues, JobCheckMigrationError.TargetAlreadyExists,
                    fullTargetRoot, null, "V0.2 data 已存在內容，migration 不會覆蓋。" );
                return CreateFailure(null, null, issues);
            }

            string jobsDirectory = Path.Combine(fullSourceRoot, "jobs");
            string trackingDirectory = Path.Combine(fullSourceRoot, "job_tracking");
            string[] jobFiles = Directory.Exists(jobsDirectory)
                ? Directory.GetFiles(jobsDirectory, "*.json", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
            string[] trackingFiles = Directory.Exists(trackingDirectory)
                ? Directory.GetFiles(trackingDirectory, "*.json", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();

            if (jobFiles.Length == 0)
            {
                AddError(issues, JobCheckMigrationError.NoLegacyJobsFound,
                    jobsDirectory, null, "找不到任何 V0.1 職缺 JSON。" );
                return CreateFailure(null, null, issues);
            }

            var legacyJobs = ReadLegacyJobs(jobFiles, issues);
            var trackingByJobId = ReadLegacyTracking(trackingFiles, issues);
            if (HasErrors(issues))
            {
                return CreateFailure(null, null, issues);
            }

            string runName = executedAt.ToUniversalTime().ToString("yyyyMMddTHHmmssZ");
            string workRoot = fullTargetRoot + ".migration-work";
            string runRoot = Path.Combine(workRoot, runName);
            string backupRoot = Path.Combine(runRoot, "backup");
            string stagingRoot = Path.Combine(runRoot, "staging-data");

            if (Directory.Exists(runRoot))
            {
                AddError(issues, JobCheckMigrationError.TargetAlreadyExists,
                    runRoot, null, "相同執行時間的 migration work 已存在，請勿覆蓋。" );
                return CreateFailure(null, runRoot, issues);
            }

            var manifest = CreateManifest(
                fullSourceRoot,
                fullTargetRoot,
                executedAt,
                jobFiles.Concat(trackingFiles).Concat(GetIndexFiles(fullSourceRoot)),
                issues);
            if (HasErrors(issues))
            {
                return CreateFailure(null, runRoot, issues);
            }

            try
            {
                Directory.CreateDirectory(runRoot);
                CopyLegacyBackup(fullSourceRoot, backupRoot);
            }
            catch (Exception exception) when (IsFileException(exception))
            {
                AddError(issues, JobCheckMigrationError.IoFailure,
                    backupRoot, null, "建立完整備份失敗：" + exception.Message);
                WriteFailureReport(runRoot, executedAt, jobFiles.Length,
                    trackingFiles.Length, issues);
                return CreateFailure(null, runRoot, issues);
            }

            JobCheckDataSet dataSet = BuildDataSet(
                legacyJobs,
                trackingByJobId,
                fullSourceRoot,
                executedAt,
                manifest,
                issues);
            if (HasErrors(issues))
            {
                WriteFailureReport(runRoot, executedAt, jobFiles.Length,
                    trackingFiles.Length, issues);
                return CreateFailure(null, runRoot, issues);
            }

            PersistenceStorageResult<PersistenceWriteSummary> write =
                JobCheckDataRepository.WriteSnapshot(stagingRoot, dataSet);
            if (!write.IsSuccess)
            {
                foreach (PersistenceStorageIssue storageIssue in write.Issues)
                {
                    AddError(issues, JobCheckMigrationError.SnapshotWriteFailed,
                        storageIssue.FilePath, storageIssue.FieldPath,
                        storageIssue.Error + ": " + storageIssue.Message);
                }

                WriteFailureReport(runRoot, executedAt, jobFiles.Length,
                    trackingFiles.Length, issues);
                return CreateFailure(null, runRoot, issues);
            }

            manifest.status = "succeeded";
            manifest.company_count = dataSet.Companies.Count;
            manifest.job_posting_count = dataSet.JobPostings.Count;
            manifest.application_count = dataSet.Applications.Count;
            manifest.application_event_count = dataSet.ApplicationEvents.Count;
            var report = CreateReport(
                "succeeded",
                executedAt,
                jobFiles.Length,
                trackingFiles.Length,
                dataSet,
                issues);

            try
            {
                string migrationDirectory = Path.Combine(stagingRoot, "migration");
                Directory.CreateDirectory(migrationDirectory);
                Directory.Move(
                    backupRoot,
                    Path.Combine(migrationDirectory, "backup_v0.1_" + runName));
                WriteTextFile(
                    Path.Combine(migrationDirectory, "manifest.json"),
                    PersistenceJsonSerializer.Serialize(manifest));
                WriteTextFile(
                    Path.Combine(migrationDirectory, "report.json"),
                    PersistenceJsonSerializer.Serialize(report));

                PersistenceStorageResult<JobCheckDataSet> verification =
                    JobCheckDataRepository.Load(stagingRoot);
                if (!verification.IsSuccess)
                {
                    foreach (PersistenceStorageIssue storageIssue in verification.Issues)
                    {
                        AddError(issues, JobCheckMigrationError.SnapshotValidationFailed,
                            storageIssue.FilePath, storageIssue.FieldPath,
                            storageIssue.Error + ": " + storageIssue.Message);
                    }

                    manifest.status = "failed_validation";
                    report.status = "failed_validation";
                    report.issues = ToReportIssues(issues);
                    ReplaceTextFile(
                        Path.Combine(migrationDirectory, "manifest.json"),
                        PersistenceJsonSerializer.Serialize(manifest));
                    ReplaceTextFile(
                        Path.Combine(migrationDirectory, "report.json"),
                        PersistenceJsonSerializer.Serialize(report));
                    return CreateFailure(null, runRoot, issues, dataSet);
                }

                if (Directory.Exists(fullTargetRoot))
                {
                    // 前面已確認它完全為空；只移除空殼以便一次 rename staging。
                    Directory.Delete(fullTargetRoot, false);
                }

                Directory.Move(stagingRoot, fullTargetRoot);
            }
            catch (Exception exception) when (IsFileException(exception))
            {
                AddError(issues, JobCheckMigrationError.IoFailure,
                    stagingRoot, null, "驗證或切換 staging 失敗：" + exception.Message);
                WriteFailureReport(runRoot, executedAt, jobFiles.Length,
                    trackingFiles.Length, issues, dataSet);
                return CreateFailure(null, runRoot, issues, dataSet);
            }

            return new JobCheckMigrationResult(
                fullTargetRoot,
                runRoot,
                dataSet.Companies.Count,
                dataSet.JobPostings.Count,
                dataSet.Applications.Count,
                issues);
        }

        private static List<KeyValuePair<string, LegacyV01JobDto>> ReadLegacyJobs(
            IEnumerable<string> paths,
            ICollection<JobCheckMigrationIssue> issues)
        {
            var result = new List<KeyValuePair<string, LegacyV01JobDto>>();
            foreach (string path in paths)
            {
                if (!TryRead(path, out LegacyV01JobDto dto, issues))
                {
                    continue;
                }

                if (!string.Equals(dto.schema_version, "0.1", StringComparison.Ordinal))
                {
                    AddError(issues, JobCheckMigrationError.UnsupportedLegacySchema,
                        path, "schema_version", "第一輪只接受 V0.1 demo。" );
                    continue;
                }

                if (dto.source == null
                    || !string.Equals(dto.source.platform, "demo", StringComparison.Ordinal))
                {
                    AddError(issues, JobCheckMigrationError.NonDemoSourceRejected,
                        path, "source.platform", "第一輪 migration 僅允許 demo 來源。" );
                    continue;
                }

                if (string.IsNullOrWhiteSpace(dto.id)
                    || dto.company == null
                    || string.IsNullOrWhiteSpace(dto.company.name)
                    || dto.job == null
                    || string.IsNullOrWhiteSpace(dto.job.title))
                {
                    AddError(issues, JobCheckMigrationError.MissingRequiredLegacyField,
                        path, null, "舊職缺缺少 id、company.name 或 job.title。" );
                    continue;
                }

                result.Add(new KeyValuePair<string, LegacyV01JobDto>(path, dto));
            }

            return result;
        }

        private static Dictionary<string, KeyValuePair<string, LegacyV01TrackingDto>>
            ReadLegacyTracking(
                IEnumerable<string> paths,
                ICollection<JobCheckMigrationIssue> issues)
        {
            var result = new Dictionary<string, KeyValuePair<string, LegacyV01TrackingDto>>(
                StringComparer.Ordinal);
            foreach (string path in paths)
            {
                if (!TryRead(path, out LegacyV01TrackingDto dto, issues))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(dto.job_id))
                {
                    AddError(issues, JobCheckMigrationError.MissingRequiredLegacyField,
                        path, "job_id", "tracking 缺少 job_id。" );
                    continue;
                }

                string expectedId = Path.GetFileName(path)
                    .Replace(".tracking.json", string.Empty);
                if (!string.Equals(expectedId, dto.job_id, StringComparison.Ordinal))
                {
                    AddError(issues, JobCheckMigrationError.TrackingJobMismatch,
                        path, "job_id", "tracking 檔名與 job_id 不一致。" );
                    continue;
                }

                if (result.ContainsKey(dto.job_id))
                {
                    AddError(issues, JobCheckMigrationError.TrackingJobMismatch,
                        path, "job_id", "同一職缺存在多個 tracking 檔。" );
                    continue;
                }

                result.Add(dto.job_id,
                    new KeyValuePair<string, LegacyV01TrackingDto>(path, dto));
            }

            return result;
        }

        private static JobCheckDataSet BuildDataSet(
            IEnumerable<KeyValuePair<string, LegacyV01JobDto>> legacyJobs,
            IReadOnlyDictionary<string, KeyValuePair<string, LegacyV01TrackingDto>> trackingByJobId,
            string sourceRoot,
            DateTimeOffset executedAt,
            MigrationManifestDto manifest,
            ICollection<JobCheckMigrationIssue> issues)
        {
            var companiesByNormalizedName = new Dictionary<string, Company>(StringComparer.Ordinal);
            var jobs = new List<JobPosting>();
            var applications = new List<Application>();
            var events = new List<ApplicationEvent>();

            foreach (KeyValuePair<string, LegacyV01JobDto> pair in legacyJobs)
            {
                string path = pair.Key;
                LegacyV01JobDto legacy = pair.Value;
                string normalizedCompanyName = CompanyNameNormalizer.Normalize(legacy.company.name);
                if (!companiesByNormalizedName.TryGetValue(normalizedCompanyName, out Company company))
                {
                    company = new Company
                    {
                        Id = CreateStableId("cmp_", "company:" + normalizedCompanyName),
                        Name = normalizedCompanyName,
                        Industry = legacy.company.industry
                    };
                    companiesByNormalizedName.Add(normalizedCompanyName, company);
                    AddIdMapping(manifest, "company", normalizedCompanyName, company.Id);
                }
                else if (!string.IsNullOrWhiteSpace(legacy.company.industry)
                    && !string.IsNullOrWhiteSpace(company.Industry)
                    && !string.Equals(company.Industry, legacy.company.industry, StringComparison.Ordinal))
                {
                    AddWarning(issues, JobCheckMigrationError.ConflictingLegacyValue,
                        path, "company.industry",
                        "同名公司的產業文字不同；保留第一筆並列入人工確認。" );
                }

                if (!PersistenceDateTimeConverter.TryParseRequired(
                    legacy.source.captured_at,
                    out DateTimeOffset capturedAt))
                {
                    AddError(issues, JobCheckMigrationError.InvalidLegacyDateTime,
                        path, "source.captured_at", "captured_at 必須包含明確時區。" );
                    continue;
                }

                JobPosting jobPosting = MapJob(legacy, company.Id, capturedAt);
                jobs.Add(jobPosting);
                AddIdMapping(manifest, "job_posting", legacy.id, jobPosting.Id);

                if (trackingByJobId.TryGetValue(
                    legacy.id,
                    out KeyValuePair<string, LegacyV01TrackingDto> trackingPair))
                {
                    MapTracking(
                        trackingPair.Key,
                        MakeRelativePath(sourceRoot, trackingPair.Key).Replace('\\', '/'),
                        trackingPair.Value,
                        legacy,
                        executedAt, applications, events, manifest, issues);
                }
                else if (legacy.tracking != null)
                {
                    MapEmbeddedTracking(
                        path,
                        MakeRelativePath(sourceRoot, path).Replace('\\', '/'),
                        legacy,
                        executedAt,
                        applications, events, manifest, issues);
                }
            }

            foreach (string orphanId in trackingByJobId.Keys.Where(
                id => jobs.All(job => !string.Equals(job.Id, id, StringComparison.Ordinal))))
            {
                AddError(issues, JobCheckMigrationError.TrackingJobMismatch,
                    trackingByJobId[orphanId].Key, "job_id",
                    "tracking 指向不存在的舊職缺。" );
            }

            return new JobCheckDataSet(
                companiesByNormalizedName.Values.OrderBy(item => item.Id, StringComparer.Ordinal),
                jobs.OrderBy(item => item.Id, StringComparer.Ordinal),
                applications.OrderBy(item => item.Id, StringComparer.Ordinal),
                events.OrderBy(item => item.Id, StringComparer.Ordinal));
        }

        private static JobPosting MapJob(
            LegacyV01JobDto source,
            string companyId,
            DateTimeOffset capturedAt)
        {
            return new JobPosting
            {
                Id = source.id,
                CompanyId = companyId,
                Title = source.job.title,
                Source = new JobSource
                {
                    Platform = source.source.platform,
                    Url = source.source.url
                },
                CapturedAt = capturedAt,
                Department = source.job.department,
                Category = source.job.category,
                Compensation = source.compensation == null ? null : new JobCompensation
                {
                    Type = source.compensation.type,
                    Period = source.compensation.period,
                    Minimum = source.compensation.min,
                    Maximum = source.compensation.max,
                    Currency = source.compensation.currency,
                    RawText = source.compensation.raw_text,
                    Notes = source.compensation.notes
                },
                Location = source.location == null ? null : new JobLocation
                {
                    WorkMode = source.location.work_mode,
                    City = source.location.city,
                    District = source.location.district,
                    Address = source.location.address,
                    RemoteAllowed = source.location.remote_allowed,
                    RawText = source.location.raw_text
                },
                WorkConditions = source.work_conditions == null ? null : new JobWorkConditions
                {
                    EmploymentType = source.work_conditions.employment_type,
                    WorkingHours = source.work_conditions.working_hours,
                    BusinessTrip = source.work_conditions.business_trip,
                    ManagementResponsibility = source.work_conditions.management_responsibility,
                    LeavePolicy = source.work_conditions.leave_policy,
                    StartDate = source.work_conditions.start_date
                },
                Responsibilities = Copy(source.responsibilities),
                Requirements = MapRequirements(source.requirements),
                Benefits = MapBenefits(source.benefits),
                RecruitmentProcess = Copy(source.recruitment_process),
                RawDescription = source.job.raw_text,
                LegacyId = source.id
            };
        }

        private static JobRequirements MapRequirements(LegacyV01RequirementsDto source)
        {
            if (source == null)
            {
                return null;
            }

            var languages = new List<JobLanguageRequirement>();
            if (source.languages != null)
            {
                foreach (LegacyV01LanguageDto language in source.languages)
                {
                    if (language != null)
                    {
                        languages.Add(new JobLanguageRequirement
                        {
                            Name = language.name,
                            Listening = language.listening,
                            Speaking = language.speaking,
                            Reading = language.reading,
                            Writing = language.writing,
                            RawText = language.raw_text
                        });
                    }
                }
            }

            return new JobRequirements
            {
                Experience = source.experience,
                Education = source.education,
                Major = source.major,
                Languages = languages,
                Tools = Copy(source.tools),
                Skills = Copy(source.skills),
                OtherConditions = Copy(source.other_conditions)
            };
        }

        private static JobBenefits MapBenefits(LegacyV01BenefitsDto source)
        {
            return source == null ? null : new JobBenefits
            {
                SalaryBonus = Copy(source.salary_bonus),
                InsuranceHealth = Copy(source.insurance_health),
                Flexibility = Copy(source.flexibility),
                Training = Copy(source.training),
                Life = Copy(source.life),
                Other = Copy(source.other)
            };
        }

        private static void MapTracking(
            string trackingPath,
            string sourceReference,
            LegacyV01TrackingDto tracking,
            LegacyV01JobDto job,
            DateTimeOffset executedAt,
            ICollection<Application> applications,
            ICollection<ApplicationEvent> events,
            MigrationManifestDto manifest,
            ICollection<JobCheckMigrationIssue> issues)
        {
            string status = string.IsNullOrWhiteSpace(tracking.status)
                ? "not_viewed"
                : tracking.status;
            bool hasContent = tracking.favorite
                || !string.IsNullOrWhiteSpace(tracking.last_action_at)
                || !string.IsNullOrWhiteSpace(tracking.manual_expire_at)
                || tracking.fit_score >= 0;
            if (!ShouldCreateApplication(status, hasContent, trackingPath, issues))
            {
                return;
            }

            DateTimeOffset occurredAt = ParseLegacyActionTime(
                tracking.last_action_at,
                executedAt,
                trackingPath,
                issues,
                out bool invalidActionTime);
            DateTimeOffset? followUpAt = ParseOptionalLegacyTime(
                tracking.manual_expire_at,
                trackingPath,
                "manual_expire_at",
                issues,
                out bool invalidFollowUpTime);

            CreateApplicationAndEvent(
                job.id,
                status,
                tracking.favorite,
                null,
                followUpAt,
                tracking.fit_score,
                trackingPath,
                sourceReference,
                occurredAt,
                executedAt,
                invalidActionTime || invalidFollowUpTime,
                applications,
                events,
                manifest,
                issues);
        }

        private static void MapEmbeddedTracking(
            string jobPath,
            string sourceReference,
            LegacyV01JobDto job,
            DateTimeOffset executedAt,
            ICollection<Application> applications,
            ICollection<ApplicationEvent> events,
            MigrationManifestDto manifest,
            ICollection<JobCheckMigrationIssue> issues)
        {
            LegacyV01EmbeddedTrackingDto tracking = job.tracking;
            string status = string.IsNullOrWhiteSpace(tracking.status)
                ? "not_viewed"
                : tracking.status;
            bool hasContent = tracking.favorite
                || tracking.priority.HasValue
                || !string.IsNullOrWhiteSpace(tracking.applied_at)
                || !string.IsNullOrWhiteSpace(tracking.last_updated_at)
                || !string.IsNullOrWhiteSpace(tracking.notes);
            if (!ShouldCreateApplication(status, hasContent, jobPath, issues))
            {
                return;
            }

            string timeText = !string.IsNullOrWhiteSpace(tracking.last_updated_at)
                ? tracking.last_updated_at
                : tracking.applied_at;
            DateTimeOffset occurredAt = ParseLegacyActionTime(
                timeText,
                executedAt,
                jobPath,
                issues,
                out bool invalidActionTime);
            CreateApplicationAndEvent(
                job.id,
                status,
                tracking.favorite,
                tracking.notes,
                null,
                -1,
                jobPath,
                sourceReference,
                occurredAt,
                executedAt,
                invalidActionTime,
                applications,
                events,
                manifest,
                issues);
        }

        private static void CreateApplicationAndEvent(
            string jobId,
            string status,
            bool favorite,
            string notes,
            DateTimeOffset? followUpAt,
            int fitScore,
            string sourcePath,
            string sourceReference,
            DateTimeOffset occurredAt,
            DateTimeOffset executedAt,
            bool additionalNeedsReview,
            ICollection<Application> applications,
            ICollection<ApplicationEvent> events,
            MigrationManifestDto manifest,
            ICollection<JobCheckMigrationIssue> issues)
        {
            ApplicationStage stage = MapStage(status);
            bool needsReview = status == "archived"
                || status == "archived_wait_other_job_result";
            string applicationId = CreateStableId("app_", "application:" + jobId);
            string eventId = CreateStableId("evt_", "migration_snapshot:" + jobId);
            var application = new Application
            {
                Id = applicationId,
                JobPostingId = jobId,
                SourceType = SourceType.MyApplication,
                CurrentStage = stage,
                CreatedAt = executedAt,
                UpdatedAt = executedAt,
                CandidateCloseReason = stage == ApplicationStage.ClosedByCandidate
                    ? CandidateCloseReason.Other
                    : (CandidateCloseReason?)null,
                CandidateCloseReasonNote = stage == ApplicationStage.ClosedByCandidate
                    ? "由 V0.1 狀態 " + status + " 轉換，原始資料沒有更精確的結案原因。"
                    : null,
                Notes = notes,
                IsFavorite = favorite,
                IsArchived = false,
                ManualFollowUpAt = followUpAt,
                LegacyStatus = status,
                NeedsReview = needsReview || additionalNeedsReview
            };
            var applicationEvent = new ApplicationEvent
            {
                Id = eventId,
                ApplicationId = applicationId,
                EventType = ApplicationEventType.MigrationSnapshot,
                OccurredAt = occurredAt,
                RecordedAt = executedAt,
                Actor = EventActor.System,
                Notes = "由 V0.1 目前狀態建立快照；不代表完整歷史事件。",
                SourceReference = sourceReference,
                LegacyStatus = status,
                SnapshotStage = stage
            };

            applications.Add(application);
            events.Add(applicationEvent);
            AddIdMapping(manifest, "application", jobId, applicationId);
            AddIdMapping(manifest, "application_event", jobId + ":migration_snapshot", eventId);

            if (fitScore >= 0)
            {
                AddWarning(issues, JobCheckMigrationError.LegacyFieldDeferred,
                    sourcePath, "fit_score",
                    "V0.2 六維 Fit 尚未實作；舊單一分數未寫入 Domain，已保留在備份。" );
                application.NeedsReview = true;
            }
        }

        private static bool ShouldCreateApplication(
            string status,
            bool hasContent,
            string path,
            ICollection<JobCheckMigrationIssue> issues)
        {
            switch (status)
            {
                case "not_viewed":
                case "not_applied":
                    return hasContent;
                case "interested":
                case "not_applying":
                case "applied":
                case "interview_scheduled":
                case "interviewing":
                case "waiting_reply":
                case "offer":
                case "rejected":
                case "closed":
                case "archived":
                case "archived_wait_other_job_result":
                    return true;
                default:
                    AddError(issues, JobCheckMigrationError.UnknownLegacyStatus,
                        path, "status", "未知的 V0.1 tracking status：" + status);
                    return false;
            }
        }

        private static ApplicationStage MapStage(string status)
        {
            switch (status)
            {
                case "not_viewed":
                case "not_applied":
                case "interested":
                    return ApplicationStage.Saved;
                case "not_applying":
                case "closed":
                    return ApplicationStage.ClosedByCandidate;
                case "applied":
                    return ApplicationStage.Applied;
                case "interview_scheduled":
                    return ApplicationStage.InterviewScheduled;
                case "interviewing":
                    return ApplicationStage.InterviewCompleted;
                case "waiting_reply":
                    return ApplicationStage.WaitingResponse;
                case "offer":
                    return ApplicationStage.OfferReceived;
                case "rejected":
                    return ApplicationStage.RejectedByCompany;
                case "archived":
                case "archived_wait_other_job_result":
                    return ApplicationStage.Unknown;
                default:
                    return ApplicationStage.Unknown;
            }
        }

        private static DateTimeOffset ParseLegacyActionTime(
            string text,
            DateTimeOffset fallback,
            string path,
            ICollection<JobCheckMigrationIssue> issues,
            out bool needsReview)
        {
            needsReview = false;
            if (string.IsNullOrWhiteSpace(text))
            {
                return fallback;
            }

            if (PersistenceDateTimeConverter.TryParseRequired(text, out DateTimeOffset parsed))
            {
                return parsed;
            }

            AddWarning(issues, JobCheckMigrationError.InvalidLegacyDateTime,
                path, "last_action_at",
                "舊操作時間無法解析；MigrationSnapshot 使用 migration 執行時間。" );
            needsReview = true;
            return fallback;
        }

        private static DateTimeOffset? ParseOptionalLegacyTime(
            string text,
            string path,
            string field,
            ICollection<JobCheckMigrationIssue> issues,
            out bool needsReview)
        {
            needsReview = false;
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (PersistenceDateTimeConverter.TryParseRequired(text, out DateTimeOffset parsed))
            {
                return parsed;
            }

            AddWarning(issues, JobCheckMigrationError.InvalidLegacyDateTime,
                path, field, "無法解析 optional 時間；保持空值並標記人工確認。" );
            needsReview = true;
            return null;
        }

        private static MigrationManifestDto CreateManifest(
            string sourceRoot,
            string targetRoot,
            DateTimeOffset executedAt,
            IEnumerable<string> sourceFiles,
            ICollection<JobCheckMigrationIssue> issues)
        {
            var manifest = new MigrationManifestDto
            {
                migration_version = MigrationVersion,
                status = "running",
                source_root = sourceRoot,
                target_root = targetRoot,
                executed_at = PersistenceDateTimeConverter.Format(executedAt)
            };

            foreach (string path in sourceFiles.OrderBy(item => item, StringComparer.Ordinal))
            {
                try
                {
                    var info = new FileInfo(path);
                    manifest.source_files.Add(new MigrationSourceFileDto
                    {
                        relative_path = MakeRelativePath(sourceRoot, path).Replace('\\', '/'),
                        size_bytes = info.Length,
                        sha256 = ComputeSha256(path)
                    });
                }
                catch (Exception exception) when (IsFileException(exception))
                {
                    AddError(issues, JobCheckMigrationError.IoFailure,
                        path, null, "建立來源雜湊失敗：" + exception.Message);
                }
            }

            return manifest;
        }

        private static void CopyLegacyBackup(string sourceRoot, string backupRoot)
        {
            foreach (string directoryName in new[] { "jobs", "job_tracking", "job_index" })
            {
                string sourceDirectory = Path.Combine(sourceRoot, directoryName);
                if (!Directory.Exists(sourceDirectory))
                {
                    continue;
                }

                string destinationDirectory = Path.Combine(backupRoot, directoryName);
                Directory.CreateDirectory(destinationDirectory);
                foreach (string sourcePath in Directory.GetFiles(
                    sourceDirectory, "*", SearchOption.TopDirectoryOnly))
                {
                    File.Copy(
                        sourcePath,
                        Path.Combine(destinationDirectory, Path.GetFileName(sourcePath)),
                        false);
                }
            }
        }

        private static IEnumerable<string> GetIndexFiles(string sourceRoot)
        {
            string path = Path.Combine(sourceRoot, "job_index");
            return Directory.Exists(path)
                ? Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();
        }

        private static bool TryRead<T>(
            string path,
            out T dto,
            ICollection<JobCheckMigrationIssue> issues)
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

                AddError(issues, JobCheckMigrationError.InvalidLegacyJson,
                    path, null, error);
            }
            catch (Exception exception) when (IsFileException(exception))
            {
                AddError(issues, JobCheckMigrationError.IoFailure,
                    path, null, exception.Message);
            }

            return false;
        }

        private static string CreateStableId(string prefix, string seed)
        {
            byte[] hash;
            using (SHA256 algorithm = SHA256.Create())
            {
                hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(
                    MigrationVersion + ":" + seed));
            }

            var guidBytes = new byte[16];
            Array.Copy(hash, guidBytes, guidBytes.Length);
            guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x50);
            guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
            return prefix + new Guid(guidBytes).ToString("N").ToLowerInvariant();
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return string.Concat(algorithm.ComputeHash(stream)
                    .Select(value => value.ToString("x2")));
            }
        }

        private static void AddIdMapping(
            MigrationManifestDto manifest,
            string entityType,
            string legacyKey,
            string newId)
        {
            manifest.id_mappings.Add(new MigrationIdMappingDto
            {
                entity_type = entityType,
                legacy_key = legacyKey,
                new_id = newId
            });
        }

        private static List<string> Copy(IEnumerable<string> values)
        {
            return values == null ? new List<string>() : new List<string>(values);
        }

        private static string MakeRelativePath(string root, string path)
        {
            Uri rootUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(root)));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(
                new Uri(Path.GetFullPath(path))).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static string GetFullPath(
            string path,
            ICollection<JobCheckMigrationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                AddError(issues, JobCheckMigrationError.IoFailure,
                    path, null, "來源與目標路徑不可為空白。" );
                return null;
            }

            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is NotSupportedException)
            {
                AddError(issues, JobCheckMigrationError.IoFailure,
                    path, null, exception.Message);
                return null;
            }
        }

        private static MigrationReportDto CreateReport(
            string status,
            DateTimeOffset executedAt,
            int sourceJobs,
            int sourceTracking,
            JobCheckDataSet dataSet,
            IEnumerable<JobCheckMigrationIssue> issues)
        {
            return new MigrationReportDto
            {
                migration_version = MigrationVersion,
                status = status,
                executed_at = PersistenceDateTimeConverter.Format(executedAt),
                source_job_count = sourceJobs,
                source_tracking_count = sourceTracking,
                output_company_count = dataSet?.Companies.Count ?? 0,
                output_job_count = dataSet?.JobPostings.Count ?? 0,
                output_application_count = dataSet?.Applications.Count ?? 0,
                output_event_count = dataSet?.ApplicationEvents.Count ?? 0,
                issues = ToReportIssues(issues)
            };
        }

        private static List<MigrationReportIssueDto> ToReportIssues(
            IEnumerable<JobCheckMigrationIssue> issues)
        {
            return issues.Select(issue => new MigrationReportIssueDto
            {
                severity = issue.Severity.ToString().ToLowerInvariant(),
                error = issue.Error.ToString(),
                file_path = issue.FilePath,
                field_path = issue.FieldPath,
                message = issue.Message
            }).ToList();
        }

        private static void WriteFailureReport(
            string runRoot,
            DateTimeOffset executedAt,
            int sourceJobs,
            int sourceTracking,
            IEnumerable<JobCheckMigrationIssue> issues,
            JobCheckDataSet dataSet = null)
        {
            try
            {
                Directory.CreateDirectory(runRoot);
                WriteTextFile(
                    Path.Combine(runRoot, "report.failed.json"),
                    PersistenceJsonSerializer.Serialize(CreateReport(
                        "failed", executedAt, sourceJobs, sourceTracking, dataSet, issues)));
            }
            catch
            {
                // 原始 migration error 仍保留；報告寫入失敗不可掩蓋真正原因。
            }
        }

        private static void WriteTextFile(string path, string content)
        {
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        private static void ReplaceTextFile(string path, string content)
        {
            string temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporaryPath, content, new UTF8Encoding(false));
                File.Replace(temporaryPath, path, null);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static bool HasErrors(IEnumerable<JobCheckMigrationIssue> issues)
        {
            return issues.Any(issue => issue.Severity == JobCheckMigrationSeverity.Error);
        }

        private static bool IsFileException(Exception exception)
        {
            return exception is IOException
                || exception is UnauthorizedAccessException
                || exception is ArgumentException
                || exception is NotSupportedException;
        }

        private static void AddError(
            ICollection<JobCheckMigrationIssue> issues,
            JobCheckMigrationError error,
            string path,
            string field,
            string message)
        {
            issues.Add(new JobCheckMigrationIssue(
                JobCheckMigrationSeverity.Error, error, path, field, message));
        }

        private static void AddWarning(
            ICollection<JobCheckMigrationIssue> issues,
            JobCheckMigrationError error,
            string path,
            string field,
            string message)
        {
            issues.Add(new JobCheckMigrationIssue(
                JobCheckMigrationSeverity.Warning, error, path, field, message));
        }

        private static JobCheckMigrationResult CreateFailure(
            string dataRoot,
            string workRoot,
            IEnumerable<JobCheckMigrationIssue> issues,
            JobCheckDataSet dataSet = null)
        {
            return new JobCheckMigrationResult(
                dataRoot,
                workRoot,
                dataSet?.Companies.Count ?? 0,
                dataSet?.JobPostings.Count ?? 0,
                dataSet?.Applications.Count ?? 0,
                issues);
        }
    }
}
