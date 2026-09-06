using System;
using System.Collections.Generic;

namespace JobCheck.Domain
{
    /// <summary>
    /// 驗證 ApplicationEvent 的必要欄位及單筆事件可以判斷的規則。
    /// Application 是否存在、SupersedesEventId 是否找到同一 Application 的事件，以及事件 ID 是否重複，
    /// 必須由持有完整資料集合的 Repository 驗證。
    /// </summary>
    public static class ApplicationEventValidator
    {
        /// <summary>
        /// 驗證一般操作建立的新事件。此入口不允許 MigrationSnapshot。
        /// </summary>
        /// <param name="applicationEvent">要驗證的新事件；可以傳入 null 以取得 MissingApplicationEvent。</param>
        /// <param name="applicationCreatedAt">所屬 Application 的建立時間，用來驗證 RecordedAt。</param>
        /// <returns>所有已發現的錯誤代碼；空集合表示此 Validator 沒有發現錯誤。</returns>
        public static IReadOnlyList<ApplicationEventValidationError> ValidateNew(
            ApplicationEvent applicationEvent,
            DateTimeOffset applicationCreatedAt)
        {
            return Validate(applicationEvent, applicationCreatedAt, allowMigrationSnapshot: false);
        }

        /// <summary>
        /// 驗證 migration 建立的事件。此入口允許 MigrationSnapshot 及 Unknown 的 SnapshotStage。
        /// </summary>
        /// <param name="applicationEvent">要驗證的 migration 事件；可以傳入 null。</param>
        /// <param name="applicationCreatedAt">migration 建立之 Application 的建立時間。</param>
        /// <returns>所有已發現的錯誤代碼；空集合表示此 Validator 沒有發現錯誤。</returns>
        public static IReadOnlyList<ApplicationEventValidationError> ValidateForMigration(
            ApplicationEvent applicationEvent,
            DateTimeOffset applicationCreatedAt)
        {
            return Validate(applicationEvent, applicationCreatedAt, allowMigrationSnapshot: true);
        }

        /// <summary>
        /// 收集一般事件與 migration 事件共用的驗證結果。
        /// </summary>
        /// <param name="applicationEvent">要驗證的事件。</param>
        /// <param name="applicationCreatedAt">所屬 Application 的建立時間。</param>
        /// <param name="allowMigrationSnapshot">是否允許 migration 專用事件與 Unknown 快照。</param>
        /// <returns>所有已發現的錯誤代碼。</returns>
        private static List<ApplicationEventValidationError> Validate(
            ApplicationEvent applicationEvent,
            DateTimeOffset applicationCreatedAt,
            bool allowMigrationSnapshot)
        {
            var errors = new List<ApplicationEventValidationError>();

            if (applicationEvent == null)
            {
                errors.Add(ApplicationEventValidationError.MissingApplicationEvent);
                return errors;
            }

            ValidateIdentity(applicationEvent, errors);
            ValidateEnums(applicationEvent, errors);
            ValidateTimes(applicationEvent, applicationCreatedAt, errors);
            ValidateSnapshot(applicationEvent, allowMigrationSnapshot, errors);
            ValidateCorrection(applicationEvent, errors);
            ValidateSystemActor(applicationEvent, errors);

            return errors;
        }

        /// <summary>
        /// 檢查事件 ID、schema version 與 Application 關聯 ID。
        /// </summary>
        private static void ValidateIdentity(
            ApplicationEvent applicationEvent,
            ICollection<ApplicationEventValidationError> errors)
        {
            if (string.IsNullOrWhiteSpace(applicationEvent.Id))
            {
                errors.Add(ApplicationEventValidationError.MissingId);
            }
            else if (!IsValidNewId(applicationEvent.Id))
            {
                errors.Add(ApplicationEventValidationError.InvalidNewId);
            }

            if (!string.Equals(
                applicationEvent.SchemaVersion,
                ApplicationEvent.CurrentSchemaVersion,
                StringComparison.Ordinal))
            {
                errors.Add(ApplicationEventValidationError.UnsupportedSchemaVersion);
            }

            if (string.IsNullOrWhiteSpace(applicationEvent.ApplicationId))
            {
                errors.Add(ApplicationEventValidationError.MissingApplicationId);
            }
        }

        /// <summary>
        /// 檢查事件種類與造成事件的角色是否為已定義 enum 值。
        /// </summary>
        private static void ValidateEnums(
            ApplicationEvent applicationEvent,
            ICollection<ApplicationEventValidationError> errors)
        {
            if (!Enum.IsDefined(typeof(ApplicationEventType), applicationEvent.EventType))
            {
                errors.Add(ApplicationEventValidationError.InvalidEventType);
            }

            if (!Enum.IsDefined(typeof(EventActor), applicationEvent.Actor))
            {
                errors.Add(ApplicationEventValidationError.InvalidActor);
            }
        }

        /// <summary>
        /// 檢查事件時間與舊資料時間精確度。
        /// OccurredAt 允許早於 RecordedAt，因為使用者可以事後補登事件。
        /// </summary>
        private static void ValidateTimes(
            ApplicationEvent applicationEvent,
            DateTimeOffset applicationCreatedAt,
            ICollection<ApplicationEventValidationError> errors)
        {
            if (!applicationEvent.OccurredAt.HasValue)
            {
                errors.Add(ApplicationEventValidationError.MissingOccurredAt);
            }

            if (!applicationEvent.RecordedAt.HasValue)
            {
                errors.Add(ApplicationEventValidationError.MissingRecordedAt);
            }
            else if (applicationEvent.RecordedAt.Value < applicationCreatedAt)
            {
                errors.Add(ApplicationEventValidationError.RecordedBeforeApplicationCreated);
            }

            if (!string.IsNullOrWhiteSpace(applicationEvent.TimePrecision)
                && !string.Equals(
                    applicationEvent.TimePrecision,
                    ApplicationEvent.DateTimePrecision,
                    StringComparison.Ordinal))
            {
                errors.Add(ApplicationEventValidationError.InvalidTimePrecision);
            }
        }

        /// <summary>
        /// 檢查 MigrationSnapshot 是否只出現在 migration，且正確保存當下階段。
        /// </summary>
        private static void ValidateSnapshot(
            ApplicationEvent applicationEvent,
            bool allowMigrationSnapshot,
            ICollection<ApplicationEventValidationError> errors)
        {
            bool isMigrationSnapshot =
                applicationEvent.EventType == ApplicationEventType.MigrationSnapshot;

            if (isMigrationSnapshot && !allowMigrationSnapshot)
            {
                errors.Add(ApplicationEventValidationError.MigrationSnapshotNotAllowed);
            }

            if (isMigrationSnapshot && !applicationEvent.SnapshotStage.HasValue)
            {
                errors.Add(ApplicationEventValidationError.MissingSnapshotStage);
                return;
            }

            if (!isMigrationSnapshot && applicationEvent.SnapshotStage.HasValue)
            {
                errors.Add(ApplicationEventValidationError.SnapshotStageOnNonMigrationEvent);
                return;
            }

            if (!applicationEvent.SnapshotStage.HasValue)
            {
                return;
            }

            ApplicationStage snapshotStage = applicationEvent.SnapshotStage.Value;
            bool hasDefinedStage = Enum.IsDefined(typeof(ApplicationStage), snapshotStage);
            if (!hasDefinedStage)
            {
                errors.Add(ApplicationEventValidationError.InvalidSnapshotStage);
            }
            else if (!allowMigrationSnapshot && snapshotStage == ApplicationStage.Unknown)
            {
                errors.Add(ApplicationEventValidationError.UnknownSnapshotStageNotAllowed);
            }
        }

        /// <summary>
        /// 檢查 DataCorrected 是否指定原事件，且其他事件沒有誤用修正欄位。
        /// </summary>
        private static void ValidateCorrection(
            ApplicationEvent applicationEvent,
            ICollection<ApplicationEventValidationError> errors)
        {
            bool isCorrection = applicationEvent.EventType == ApplicationEventType.DataCorrected;

            if (isCorrection && string.IsNullOrWhiteSpace(applicationEvent.SupersedesEventId))
            {
                errors.Add(ApplicationEventValidationError.MissingSupersedesEventId);
                return;
            }

            if (!isCorrection && !string.IsNullOrWhiteSpace(applicationEvent.SupersedesEventId))
            {
                errors.Add(ApplicationEventValidationError.SupersedesEventIdOnNonCorrectionEvent);
                return;
            }

            if (isCorrection
                && string.Equals(
                    applicationEvent.SupersedesEventId,
                    applicationEvent.Id,
                    StringComparison.Ordinal))
            {
                errors.Add(ApplicationEventValidationError.SupersedesSelf);
            }
        }

        /// <summary>
        /// 檢查技術性系統事件是否明確標記由 JobCheck 系統建立。
        /// </summary>
        private static void ValidateSystemActor(
            ApplicationEvent applicationEvent,
            ICollection<ApplicationEventValidationError> errors)
        {
            bool isSystemEvent = applicationEvent.EventType == ApplicationEventType.MigrationSnapshot
                || applicationEvent.EventType == ApplicationEventType.DataCorrected;

            if (isSystemEvent && applicationEvent.Actor != EventActor.System)
            {
                errors.Add(ApplicationEventValidationError.SystemEventRequiresSystemActor);
            }
        }

        /// <summary>
        /// 判斷 ID 是否符合新 ApplicationEvent 的固定格式。
        /// </summary>
        /// <param name="id">要檢查的事件 ID。</param>
        /// <returns>符合 evt_ 加上 32 位小寫十六進位 UUID 時為 true。</returns>
        private static bool IsValidNewId(string id)
        {
            if (!id.StartsWith(ApplicationEventIdGenerator.Prefix, StringComparison.Ordinal)
                || id.Length != 36)
            {
                return false;
            }

            string uuidPart = id.Substring(ApplicationEventIdGenerator.Prefix.Length);
            return string.Equals(uuidPart, uuidPart.ToLowerInvariant(), StringComparison.Ordinal)
                && Guid.TryParseExact(uuidPart, "N", out _);
        }
    }
}
