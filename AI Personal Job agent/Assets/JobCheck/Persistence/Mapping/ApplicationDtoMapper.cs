using System.Collections.Generic;
using JobCheck.Domain;

namespace JobCheck.Persistence
{
    /// <summary>
    /// 在 ApplicationDto 與 Domain Application＋內嵌事件集合之間轉換資料。
    /// </summary>
    public static class ApplicationDtoMapper
    {
        /// <summary>
        /// 將 ApplicationDto 主體與 events 轉成一份 ApplicationPersistenceBundle。
        /// 任何事件格式錯誤都會使整份結果失敗，避免只載入半套歷史。
        /// </summary>
        public static PersistenceConversionResult<ApplicationPersistenceBundle> ToDomain(
            ApplicationDto dto)
        {
            var context = new PersistenceMappingContext();
            if (dto == null)
            {
                context.Add(PersistenceConversionError.MissingSourceObject, "application");
                return context.CreateResult<ApplicationPersistenceBundle>(null);
            }

            var application = new Application
            {
                Id = dto.id,
                SchemaVersion = dto.schema_version,
                JobPostingId = dto.job_posting_id,
                SourceType = context.ParseRequiredEnum<SourceType>(
                    dto.source_type,
                    "source_type"),
                CurrentStage = context.ParseRequiredEnum<ApplicationStage>(
                    dto.current_stage,
                    "current_stage"),
                CreatedAt = context.ParseRequiredDateTime(dto.created_at, "created_at"),
                UpdatedAt = context.ParseRequiredDateTime(dto.updated_at, "updated_at"),
                CandidateCloseReason = context.ParseOptionalEnum<CandidateCloseReason>(
                    dto.candidate_close_reason,
                    "candidate_close_reason"),
                CandidateCloseReasonNote = dto.candidate_close_reason_note,
                Notes = dto.notes,
                IsFavorite = dto.is_favorite,
                IsArchived = dto.is_archived,
                ArchiveReason = context.ParseOptionalEnum<ArchiveReason>(
                    dto.archive_reason,
                    "archive_reason"),
                ManualFollowUpAt = context.ParseOptionalDateTime(
                    dto.manual_follow_up_at,
                    "manual_follow_up_at"),
                PreviousApplicationId = dto.previous_application_id,
                LegacyStatus = dto.legacy_status,
                NeedsReview = dto.needs_review
            };

            var events = new List<ApplicationEvent>();
            if (dto.events != null)
            {
                for (int index = 0; index < dto.events.Count; index++)
                {
                    ApplicationEvent applicationEvent = ApplicationEventDtoMapper.MapToDomain(
                        dto.events[index],
                        context,
                        "events[" + index + "]");
                    if (applicationEvent != null)
                    {
                        events.Add(applicationEvent);
                    }
                }
            }

            return context.CreateResult(
                new ApplicationPersistenceBundle(application, events));
        }

        /// <summary>
        /// 將 Domain Application 與事件集合轉成單一 ApplicationDto。
        /// 集合為 null 時視為沒有事件；集合內的 null 事件則會使結果失敗。
        /// </summary>
        public static PersistenceConversionResult<ApplicationDto> ToDto(
            Application application,
            IEnumerable<ApplicationEvent> applicationEvents)
        {
            var context = new PersistenceMappingContext();
            if (application == null)
            {
                context.Add(PersistenceConversionError.MissingSourceObject, "application");
                return context.CreateResult<ApplicationDto>(null);
            }

            var dto = new ApplicationDto
            {
                id = application.Id,
                schema_version = application.SchemaVersion,
                job_posting_id = application.JobPostingId,
                source_type = context.FormatEnum(application.SourceType, "source_type"),
                current_stage = context.FormatEnum(application.CurrentStage, "current_stage"),
                created_at = PersistenceDateTimeConverter.Format(application.CreatedAt),
                updated_at = PersistenceDateTimeConverter.Format(application.UpdatedAt),
                candidate_close_reason = context.FormatOptionalEnum(
                    application.CandidateCloseReason,
                    "candidate_close_reason"),
                candidate_close_reason_note = application.CandidateCloseReasonNote,
                notes = application.Notes,
                is_favorite = application.IsFavorite,
                is_archived = application.IsArchived,
                archive_reason = context.FormatOptionalEnum(
                    application.ArchiveReason,
                    "archive_reason"),
                manual_follow_up_at = PersistenceDateTimeConverter.Format(
                    application.ManualFollowUpAt),
                previous_application_id = application.PreviousApplicationId,
                legacy_status = application.LegacyStatus,
                needs_review = application.NeedsReview
            };

            if (applicationEvents != null)
            {
                int index = 0;
                foreach (ApplicationEvent applicationEvent in applicationEvents)
                {
                    dto.events.Add(ApplicationEventDtoMapper.MapToDto(
                        applicationEvent,
                        context,
                        "events[" + index + "]"));
                    index++;
                }
            }

            return context.CreateResult(dto);
        }
    }
}
