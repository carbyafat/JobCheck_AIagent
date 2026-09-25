using System;
using System.Collections.Generic;
using System.Linq;
using JobCheck.Domain;

namespace JobCheck.Persistence
{
    /// <summary>
    /// V0.2 應徵資料的寫入入口。UI 只能提出「發生了什麼事」，
    /// 此服務負責建立／選取 Application、追加不可變事件、Reducer 重算階段與原子保存。
    /// </summary>
    public static class ApplicationCommandService
    {
        /// <summary>
        /// 記錄一筆流程事件。同一職缺只有在既有應徵已結案後再次記錄 Applied，才會建立新 Application，
        /// 並以 PreviousApplicationId 只連結最近一次應徵；進行中的應徵不可重複投遞。
        /// </summary>
        public static PersistenceStorageResult<ApplicationWriteSummary> RecordEvent(
            string dataRoot,
            string jobPostingId,
            ApplicationEventType eventType,
            EventActor actor,
            DateTimeOffset? occurredAt = null,
            DateTimeOffset? scheduledFor = null,
            CandidateCloseReason? closeReason = null,
            string closeReasonNote = null,
            string notes = null)
        {
            PersistenceStorageResult<JobCheckDataSet> load = JobCheckDataRepository.Load(dataRoot);
            if (!load.IsSuccess)
            {
                return Failure(load.Issues);
            }

            if (!load.Value.JobPostings.Any(item => string.Equals(
                item.Id,
                jobPostingId,
                StringComparison.Ordinal)))
            {
                return Failure(jobPostingId, "job_posting_id", "找不到指定的 JobPosting。");
            }

            if (eventType == ApplicationEventType.MigrationSnapshot
                || eventType == ApplicationEventType.DataCorrected)
            {
                return Failure(jobPostingId, "event_type", "一般操作不可建立 migration 或資料修正事件。");
            }

            DateTimeOffset recordedAt = DateTimeOffset.Now;
            DateTimeOffset eventOccurredAt = occurredAt ?? recordedAt;
            Application latest = FindLatestApplication(load.Value, jobPostingId);
            bool recordsAppliedForExisting = eventType == ApplicationEventType.Applied
                && latest != null;
            bool startsReapplication = recordsAppliedForExisting
                && IsTerminal(latest.CurrentStage);
            if (recordsAppliedForExisting
                && latest.CurrentStage != ApplicationStage.Saved
                && !startsReapplication)
            {
                return Failure(
                    latest.Id,
                    "current_stage",
                    "目前應徵尚未結案，不能再次投遞。請先記錄公司拒絕或本人放棄。");
            }

            DateTimeOffset? previousLastActivityAt = startsReapplication
                ? FindLastActivityAt(load.Value, latest)
                : null;
            if (previousLastActivityAt.HasValue
                && eventOccurredAt.Date < previousLastActivityAt.Value.Date)
            {
                return Failure(
                    latest.Id,
                    "occurred_at",
                    "再次投遞日期不可早於上一輪應徵的最後事件日期。");
            }

            Application application = latest;
            var events = new List<ApplicationEvent>();

            if (application == null || startsReapplication)
            {
                application = new Application
                {
                    Id = ApplicationIdGenerator.Create(),
                    JobPostingId = jobPostingId,
                    SourceType = SourceType.MyApplication,
                    CurrentStage = ApplicationStage.Saved,
                    // 事後補登時，Application 的生命週期必須從事件真正發生的時間開始，
                    // 而不是從今天輸入資料的時間開始。
                    CreatedAt = eventOccurredAt,
                    UpdatedAt = recordedAt,
                    PreviousApplicationId = latest?.Id
                };
            }
            else
            {
                events.AddRange(load.Value.ApplicationEvents.Where(item => string.Equals(
                    item.ApplicationId,
                    application.Id,
                    StringComparison.Ordinal)));
            }

            if (!application.CreatedAt.HasValue || eventOccurredAt < application.CreatedAt.Value)
            {
                application.CreatedAt = eventOccurredAt;
            }

            if (IsTerminal(application.CurrentStage) && !startsReapplication)
            {
                return Failure(
                    application.Id,
                    "current_stage",
                    "本次應徵已結案；若要再次投遞，請建立 Applied 事件以開始新 Application。");
            }

            var applicationEvent = new ApplicationEvent
            {
                Id = ApplicationEventIdGenerator.Create(),
                ApplicationId = application.Id,
                EventType = eventType,
                OccurredAt = eventOccurredAt,
                RecordedAt = recordedAt,
                ScheduledFor = scheduledFor,
                Actor = actor,
                Notes = NullIfWhiteSpace(notes)
            };
            IReadOnlyList<ApplicationEventValidationError> eventErrors =
                ApplicationEventValidator.ValidateNew(applicationEvent, application.CreatedAt.Value);
            if (eventErrors.Count > 0)
            {
                return Failure(
                    applicationEvent.Id,
                    "event",
                    "事件驗證失敗：" + string.Join(
                        "、",
                        eventErrors.Select(ValidationErrorLocalizer.ToChinese)));
            }

            events.Add(applicationEvent);
            ApplicationStateReductionResult reduction = ApplicationStateReducer.Reduce(
                new ApplicationStateReducerInput(application.Id, events));
            if (reduction.Issues.Count > 0)
            {
                return Failure(
                    application.Id,
                    "events",
                    "事件順序無法安全套用：" + string.Join(
                        "、",
                        reduction.Issues.Select(ValidationErrorLocalizer.ToChinese)));
            }

            application.CurrentStage = reduction.CurrentStage;
            application.UpdatedAt = recordedAt;
            application.NeedsReview = false;
            if (eventType == ApplicationEventType.ClosedByCandidate)
            {
                application.CandidateCloseReason = closeReason;
                application.CandidateCloseReasonNote = NullIfWhiteSpace(closeReasonNote);
            }
            else
            {
                application.CandidateCloseReason = null;
                application.CandidateCloseReasonNote = null;
            }

            IReadOnlyList<ApplicationValidationError> applicationErrors =
                ApplicationValidator.ValidateNew(application);
            if (applicationErrors.Count > 0)
            {
                return Failure(
                    application.Id,
                    "application",
                    "應徵資料驗證失敗：" + string.Join(
                        "、",
                        applicationErrors.Select(ValidationErrorLocalizer.ToChinese)));
            }

            return JobCheckDataRepository.SaveApplication(dataRoot, application, events);
        }

        /// <summary>
        /// 更新收藏旗標；這是整理資訊，不建立假的流程事件。
        /// </summary>
        public static PersistenceStorageResult<ApplicationWriteSummary> SetFavorite(
            string dataRoot,
            string jobPostingId,
            bool isFavorite)
        {
            return UpdateApplication(
                dataRoot,
                jobPostingId,
                application => application.IsFavorite = isFavorite);
        }

        /// <summary>
        /// 更新自由備註；null 或空白代表清除。
        /// </summary>
        public static PersistenceStorageResult<ApplicationWriteSummary> SetNotes(
            string dataRoot,
            string jobPostingId,
            string notes)
        {
            return UpdateApplication(
                dataRoot,
                jobPostingId,
                application => application.Notes = NullIfWhiteSpace(notes));
        }

        /// <summary>
        /// 設定下次人工追蹤時間；null 代表清除，不會自行改變 CurrentStage。
        /// </summary>
        public static PersistenceStorageResult<ApplicationWriteSummary> SetManualFollowUp(
            string dataRoot,
            string jobPostingId,
            DateTimeOffset? followUpAt)
        {
            return UpdateApplication(
                dataRoot,
                jobPostingId,
                application => application.ManualFollowUpAt = followUpAt);
        }

        /// <summary>
        /// 封存只允許用於已結案應徵（或 Duplicate）；不把封存當作求職進度。
        /// </summary>
        public static PersistenceStorageResult<ApplicationWriteSummary> SetArchived(
            string dataRoot,
            string jobPostingId,
            bool isArchived,
            ArchiveReason? reason)
        {
            return UpdateApplication(
                dataRoot,
                jobPostingId,
                application =>
                {
                    application.IsArchived = isArchived;
                    application.ArchiveReason = isArchived ? reason : null;
                });
        }

        private static PersistenceStorageResult<ApplicationWriteSummary> UpdateApplication(
            string dataRoot,
            string jobPostingId,
            Action<Application> update)
        {
            PersistenceStorageResult<JobCheckDataSet> load = JobCheckDataRepository.Load(dataRoot);
            if (!load.IsSuccess)
            {
                return Failure(load.Issues);
            }

            Application application = FindLatestApplication(load.Value, jobPostingId);
            if (application == null)
            {
                return Failure(
                    jobPostingId,
                    "application",
                    "尚未建立 Application；請先記錄有興趣或已投遞。");
            }

            update(application);
            application.UpdatedAt = DateTimeOffset.Now;
            IReadOnlyList<ApplicationValidationError> errors =
                ApplicationValidator.ValidateNew(application);
            if (errors.Count > 0)
            {
                return Failure(
                    application.Id,
                    "application",
                    "應徵資料驗證失敗：" + string.Join(
                        "、",
                        errors.Select(ValidationErrorLocalizer.ToChinese)));
            }

            IEnumerable<ApplicationEvent> events = load.Value.ApplicationEvents.Where(
                item => string.Equals(item.ApplicationId, application.Id, StringComparison.Ordinal));
            return JobCheckDataRepository.SaveApplication(dataRoot, application, events);
        }

        private static Application FindLatestApplication(
            JobCheckDataSet dataSet,
            string jobPostingId)
        {
            return dataSet.Applications
                .Where(item => item.SourceType == SourceType.MyApplication
                    && string.Equals(item.JobPostingId, jobPostingId, StringComparison.Ordinal))
                .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt ?? DateTimeOffset.MinValue)
                .ThenByDescending(item => item.Id, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private static DateTimeOffset? FindLastActivityAt(
            JobCheckDataSet dataSet,
            Application application)
        {
            if (application == null)
            {
                return null;
            }

            ApplicationEvent latestEvent = dataSet.ApplicationEvents
                .Where(item => string.Equals(
                    item.ApplicationId,
                    application.Id,
                    StringComparison.Ordinal))
                .OrderByDescending(item => item.OccurredAt ?? item.RecordedAt)
                .ThenByDescending(item => item.Id, StringComparer.Ordinal)
                .FirstOrDefault();

            return latestEvent?.OccurredAt
                ?? latestEvent?.RecordedAt
                ?? application.UpdatedAt
                ?? application.CreatedAt;
        }

        private static bool IsTerminal(ApplicationStage stage)
        {
            return stage == ApplicationStage.RejectedByCompany
                || stage == ApplicationStage.ClosedByCandidate;
        }

        private static string NullIfWhiteSpace(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static PersistenceStorageResult<ApplicationWriteSummary> Failure(
            IEnumerable<PersistenceStorageIssue> issues)
        {
            return new PersistenceStorageResult<ApplicationWriteSummary>(null, issues);
        }

        private static PersistenceStorageResult<ApplicationWriteSummary> Failure(
            string entityId,
            string fieldPath,
            string message)
        {
            return Failure(new[]
            {
                new PersistenceStorageIssue(
                    PersistenceStorageError.EntityValidationFailed,
                    entityId,
                    fieldPath,
                    message)
            });
        }
    }
}
