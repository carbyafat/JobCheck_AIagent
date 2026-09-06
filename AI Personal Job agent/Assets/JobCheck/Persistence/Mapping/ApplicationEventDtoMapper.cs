using JobCheck.Domain;

namespace JobCheck.Persistence
{
    /// <summary>
    /// 在 ApplicationEvent 與事件 DTO 之間轉換時間、enum 與一般欄位。
    /// </summary>
    public static class ApplicationEventDtoMapper
    {
        /// <summary>
        /// 將單一事件 DTO 轉成 Domain 事件。
        /// </summary>
        public static PersistenceConversionResult<ApplicationEvent> ToDomain(
            ApplicationEventDto dto)
        {
            var context = new PersistenceMappingContext();
            ApplicationEvent value = MapToDomain(dto, context, "application_event");
            return context.CreateResult(value);
        }

        /// <summary>
        /// 將單一 Domain 事件轉成 DTO。
        /// </summary>
        public static PersistenceConversionResult<ApplicationEventDto> ToDto(
            ApplicationEvent applicationEvent)
        {
            var context = new PersistenceMappingContext();
            ApplicationEventDto value = MapToDto(
                applicationEvent,
                context,
                "application_event");
            return context.CreateResult(value);
        }

        /// <summary>
        /// 使用共用 Context 轉換內嵌事件，讓 Application Mapper 能回報 events[index] 路徑。
        /// </summary>
        internal static ApplicationEvent MapToDomain(
            ApplicationEventDto dto,
            PersistenceMappingContext context,
            string fieldPath)
        {
            if (dto == null)
            {
                context.Add(PersistenceConversionError.MissingSourceObject, fieldPath);
                return null;
            }

            return new ApplicationEvent
            {
                Id = dto.id,
                SchemaVersion = dto.schema_version,
                ApplicationId = dto.application_id,
                EventType = context.ParseRequiredEnum<ApplicationEventType>(
                    dto.event_type,
                    fieldPath + ".event_type"),
                OccurredAt = context.ParseRequiredDateTime(
                    dto.occurred_at,
                    fieldPath + ".occurred_at"),
                RecordedAt = context.ParseRequiredDateTime(
                    dto.recorded_at,
                    fieldPath + ".recorded_at"),
                Actor = context.ParseRequiredEnum<EventActor>(
                    dto.actor,
                    fieldPath + ".actor"),
                Notes = dto.notes,
                SourceReference = dto.source_reference,
                LegacyStatus = dto.legacy_status,
                TimePrecision = dto.time_precision,
                SupersedesEventId = dto.supersedes_event_id,
                SnapshotStage = context.ParseOptionalEnum<ApplicationStage>(
                    dto.snapshot_stage,
                    fieldPath + ".snapshot_stage")
            };
        }

        /// <summary>
        /// 使用共用 Context 格式化內嵌事件，undefined enum 會成為可定位 Issue。
        /// </summary>
        internal static ApplicationEventDto MapToDto(
            ApplicationEvent applicationEvent,
            PersistenceMappingContext context,
            string fieldPath)
        {
            if (applicationEvent == null)
            {
                context.Add(PersistenceConversionError.MissingSourceObject, fieldPath);
                return null;
            }

            return new ApplicationEventDto
            {
                id = applicationEvent.Id,
                schema_version = applicationEvent.SchemaVersion,
                application_id = applicationEvent.ApplicationId,
                event_type = context.FormatEnum(
                    applicationEvent.EventType,
                    fieldPath + ".event_type"),
                occurred_at = PersistenceDateTimeConverter.Format(applicationEvent.OccurredAt),
                recorded_at = PersistenceDateTimeConverter.Format(applicationEvent.RecordedAt),
                actor = context.FormatEnum(
                    applicationEvent.Actor,
                    fieldPath + ".actor"),
                notes = applicationEvent.Notes,
                source_reference = applicationEvent.SourceReference,
                legacy_status = applicationEvent.LegacyStatus,
                time_precision = applicationEvent.TimePrecision,
                supersedes_event_id = applicationEvent.SupersedesEventId,
                snapshot_stage = context.FormatOptionalEnum(
                    applicationEvent.SnapshotStage,
                    fieldPath + ".snapshot_stage")
            };
        }
    }
}
