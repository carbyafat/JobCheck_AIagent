using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace JobCheck.Domain.Tests
{
    /// <summary>
    /// 驗證 ApplicationEvent 的 ID、必要欄位、時間、migration snapshot 與事件修正规則。
    /// </summary>
    public class ApplicationEventTests
    {
        /// <summary>
        /// 確認新事件 ID 符合 evt_ 加上 32 位小寫十六進位 UUID 的格式。
        /// </summary>
        [Test]
        public void ApplicationEventIdGenerator_Create_ReturnsExpectedFormat()
        {
            string eventId = ApplicationEventIdGenerator.Create();

            StringAssert.IsMatch("^evt_[0-9a-f]{32}$", eventId);
        }

        /// <summary>
        /// 確認連續建立事件時不會重用同一個 ID。
        /// </summary>
        [Test]
        public void ApplicationEventIdGenerator_CreateTwice_ReturnsDifferentIds()
        {
            Assert.AreNotEqual(
                ApplicationEventIdGenerator.Create(),
                ApplicationEventIdGenerator.Create());
        }

        /// <summary>
        /// 確認欄位齊全的一般事件可以通過驗證。
        /// </summary>
        [Test]
        public void ValidateNew_ValidEvent_ReturnsNoErrors()
        {
            IReadOnlyList<ApplicationEventValidationError> errors =
                ApplicationEventValidator.ValidateNew(CreateValidEvent(), ApplicationCreatedAt);

            Assert.IsEmpty(errors);
        }

        /// <summary>
        /// 確認事件物件不存在時回報 MissingApplicationEvent，而不是發生 null reference exception。
        /// </summary>
        [Test]
        public void ValidateNew_NullEvent_ReturnsMissingApplicationEvent()
        {
            IReadOnlyList<ApplicationEventValidationError> errors =
                ApplicationEventValidator.ValidateNew(null, ApplicationCreatedAt);

            CollectionAssert.Contains(errors, ApplicationEventValidationError.MissingApplicationEvent);
        }

        /// <summary>
        /// 確認缺少 ID、schema version 與 Application ID 時會分別留下錯誤。
        /// </summary>
        [Test]
        public void ValidateNew_MissingIdentityFields_ReturnsErrors()
        {
            ApplicationEvent applicationEvent = CreateValidEvent();
            applicationEvent.Id = " ";
            applicationEvent.SchemaVersion = null;
            applicationEvent.ApplicationId = null;

            IReadOnlyList<ApplicationEventValidationError> errors =
                ApplicationEventValidator.ValidateNew(applicationEvent, ApplicationCreatedAt);

            CollectionAssert.Contains(errors, ApplicationEventValidationError.MissingId);
            CollectionAssert.Contains(errors, ApplicationEventValidationError.UnsupportedSchemaVersion);
            CollectionAssert.Contains(errors, ApplicationEventValidationError.MissingApplicationId);
        }

        /// <summary>
        /// 確認錯誤前綴或非新版格式的事件 ID 不會被接受。
        /// </summary>
        [Test]
        public void ValidateNew_InvalidId_ReturnsError()
        {
            ApplicationEvent applicationEvent = CreateValidEvent();
            applicationEvent.Id = "event-001";

            IReadOnlyList<ApplicationEventValidationError> errors =
                ApplicationEventValidator.ValidateNew(applicationEvent, ApplicationCreatedAt);

            CollectionAssert.Contains(errors, ApplicationEventValidationError.InvalidNewId);
        }

        /// <summary>
        /// 確認 enum 定義以外的事件種類與行為角色都會被回報。
        /// </summary>
        [Test]
        public void ValidateNew_UndefinedEnums_ReturnErrors()
        {
            ApplicationEvent applicationEvent = CreateValidEvent();
            applicationEvent.EventType = (ApplicationEventType)999;
            applicationEvent.Actor = (EventActor)999;

            IReadOnlyList<ApplicationEventValidationError> errors =
                ApplicationEventValidator.ValidateNew(applicationEvent, ApplicationCreatedAt);

            CollectionAssert.Contains(errors, ApplicationEventValidationError.InvalidEventType);
            CollectionAssert.Contains(errors, ApplicationEventValidationError.InvalidActor);
        }

        /// <summary>
        /// 確認事件發生時間與輸入時間缺漏時都會留下明確錯誤。
        /// </summary>
        [Test]
        public void ValidateNew_MissingTimes_ReturnsErrors()
        {
            ApplicationEvent applicationEvent = CreateValidEvent();
            applicationEvent.OccurredAt = null;
            applicationEvent.RecordedAt = null;

            IReadOnlyList<ApplicationEventValidationError> errors =
                ApplicationEventValidator.ValidateNew(applicationEvent, ApplicationCreatedAt);

            CollectionAssert.Contains(errors, ApplicationEventValidationError.MissingOccurredAt);
            CollectionAssert.Contains(errors, ApplicationEventValidationError.MissingRecordedAt);
        }

        /// <summary>
        /// 確認事件輸入時間不能早於所屬 Application 的建立時間。
        /// </summary>
        [Test]
        public void ValidateNew_RecordedBeforeApplicationCreated_ReturnsError()
        {
            ApplicationEvent applicationEvent = CreateValidEvent();
            applicationEvent.RecordedAt = ApplicationCreatedAt.AddMinutes(-1);

            IReadOnlyList<ApplicationEventValidationError> errors =
                ApplicationEventValidator.ValidateNew(applicationEvent, ApplicationCreatedAt);

            CollectionAssert.Contains(
                errors,
                ApplicationEventValidationError.RecordedBeforeApplicationCreated);
        }

        /// <summary>
        /// 確認事後補登較早發生的事件是合法情況，不要求 OccurredAt 等於 RecordedAt。
        /// </summary>
        [Test]
        public void ValidateNew_BackdatedOccurrence_ReturnsNoErrors()
        {
            ApplicationEvent applicationEvent = CreateValidEvent();
            applicationEvent.OccurredAt = ApplicationCreatedAt.AddDays(-2);
            applicationEvent.RecordedAt = ApplicationCreatedAt.AddDays(1);

            IReadOnlyList<ApplicationEventValidationError> errors =
                ApplicationEventValidator.ValidateNew(applicationEvent, ApplicationCreatedAt);

            Assert.IsEmpty(errors);
        }

        /// <summary>
        /// 確認舊資料時間精度只接受固定值 date，不接受自行發明的文字。
        /// </summary>
        [Test]
        public void ValidateNew_InvalidTimePrecision_ReturnsError()
        {
            ApplicationEvent applicationEvent = CreateValidEvent();
            applicationEvent.TimePrecision = "minute";

            IReadOnlyList<ApplicationEventValidationError> errors =
                ApplicationEventValidator.ValidateNew(applicationEvent, ApplicationCreatedAt);

            CollectionAssert.Contains(errors, ApplicationEventValidationError.InvalidTimePrecision);
        }

        /// <summary>
        /// 確認一般操作入口不能建立 migration 專用的狀態快照。
        /// </summary>
        [Test]
        public void ValidateNew_MigrationSnapshot_ReturnsNotAllowed()
        {
            ApplicationEvent applicationEvent = CreateMigrationSnapshot();

            IReadOnlyList<ApplicationEventValidationError> errors =
                ApplicationEventValidator.ValidateNew(applicationEvent, ApplicationCreatedAt);

            CollectionAssert.Contains(errors, ApplicationEventValidationError.MigrationSnapshotNotAllowed);
        }

        /// <summary>
        /// 確認 migration snapshot 缺少轉換當下階段時會被拒絕。
        /// </summary>
        [Test]
        public void ValidateForMigration_MissingSnapshotStage_ReturnsError()
        {
            ApplicationEvent applicationEvent = CreateMigrationSnapshot();
            applicationEvent.SnapshotStage = null;

            IReadOnlyList<ApplicationEventValidationError> errors =
                ApplicationEventValidator.ValidateForMigration(applicationEvent, ApplicationCreatedAt);

            CollectionAssert.Contains(errors, ApplicationEventValidationError.MissingSnapshotStage);
        }

        /// <summary>
        /// 確認 migration 可以誠實保留無法判定的 Unknown 階段。
        /// </summary>
        [Test]
        public void ValidateForMigration_UnknownSnapshotStage_ReturnsNoErrors()
        {
            ApplicationEvent applicationEvent = CreateMigrationSnapshot();
            applicationEvent.SnapshotStage = ApplicationStage.Unknown;

            IReadOnlyList<ApplicationEventValidationError> errors =
                ApplicationEventValidator.ValidateForMigration(applicationEvent, ApplicationCreatedAt);

            Assert.IsEmpty(errors);
        }

        /// <summary>
        /// 確認普通流程事件不能填入只有 migration snapshot 才能使用的 SnapshotStage。
        /// </summary>
        [Test]
        public void ValidateNew_SnapshotStageOnNormalEvent_ReturnsError()
        {
            ApplicationEvent applicationEvent = CreateValidEvent();
            applicationEvent.SnapshotStage = ApplicationStage.Applied;

            IReadOnlyList<ApplicationEventValidationError> errors =
                ApplicationEventValidator.ValidateNew(applicationEvent, ApplicationCreatedAt);

            CollectionAssert.Contains(
                errors,
                ApplicationEventValidationError.SnapshotStageOnNonMigrationEvent);
        }

        /// <summary>
        /// 確認 migration snapshot 不能保存 enum 定義以外的階段值。
        /// </summary>
        [Test]
        public void ValidateForMigration_InvalidSnapshotStage_ReturnsError()
        {
            ApplicationEvent applicationEvent = CreateMigrationSnapshot();
            applicationEvent.SnapshotStage = (ApplicationStage)999;

            IReadOnlyList<ApplicationEventValidationError> errors =
                ApplicationEventValidator.ValidateForMigration(applicationEvent, ApplicationCreatedAt);

            CollectionAssert.Contains(errors, ApplicationEventValidationError.InvalidSnapshotStage);
        }

        /// <summary>
        /// 確認資料修正事件一定要指出所排除的原事件 ID。
        /// </summary>
        [Test]
        public void ValidateNew_DataCorrectedWithoutTarget_ReturnsError()
        {
            ApplicationEvent applicationEvent = CreateValidCorrection();
            applicationEvent.SupersedesEventId = null;

            IReadOnlyList<ApplicationEventValidationError> errors =
                ApplicationEventValidator.ValidateNew(applicationEvent, ApplicationCreatedAt);

            CollectionAssert.Contains(errors, ApplicationEventValidationError.MissingSupersedesEventId);
        }

        /// <summary>
        /// 確認普通事件不能誤用只有 DataCorrected 才能填寫的 SupersedesEventId。
        /// </summary>
        [Test]
        public void ValidateNew_SupersedesIdOnNormalEvent_ReturnsError()
        {
            ApplicationEvent applicationEvent = CreateValidEvent();
            applicationEvent.SupersedesEventId = ApplicationEventIdGenerator.Create();

            IReadOnlyList<ApplicationEventValidationError> errors =
                ApplicationEventValidator.ValidateNew(applicationEvent, ApplicationCreatedAt);

            CollectionAssert.Contains(
                errors,
                ApplicationEventValidationError.SupersedesEventIdOnNonCorrectionEvent);
        }

        /// <summary>
        /// 確認資料修正事件不能將自己指定為被排除的事件。
        /// </summary>
        [Test]
        public void ValidateNew_DataCorrectedSupersedesSelf_ReturnsError()
        {
            ApplicationEvent applicationEvent = CreateValidCorrection();
            applicationEvent.SupersedesEventId = applicationEvent.Id;

            IReadOnlyList<ApplicationEventValidationError> errors =
                ApplicationEventValidator.ValidateNew(applicationEvent, ApplicationCreatedAt);

            CollectionAssert.Contains(errors, ApplicationEventValidationError.SupersedesSelf);
        }

        /// <summary>
        /// 確認資料修正事件使用另一個事件 ID 並標記為 System 時可以通過單筆驗證。
        /// 目標事件是否真的存在，留給 Repository 的跨資料驗證。
        /// </summary>
        [Test]
        public void ValidateNew_ValidDataCorrection_ReturnsNoErrors()
        {
            IReadOnlyList<ApplicationEventValidationError> errors =
                ApplicationEventValidator.ValidateNew(CreateValidCorrection(), ApplicationCreatedAt);

            Assert.IsEmpty(errors);
        }

        /// <summary>
        /// 確認 migration 與資料修正等技術性事件必須明確標記由 System 建立。
        /// </summary>
        [Test]
        public void ValidateForMigration_NonSystemActor_ReturnsError()
        {
            ApplicationEvent applicationEvent = CreateMigrationSnapshot();
            applicationEvent.Actor = EventActor.Candidate;

            IReadOnlyList<ApplicationEventValidationError> errors =
                ApplicationEventValidator.ValidateForMigration(applicationEvent, ApplicationCreatedAt);

            CollectionAssert.Contains(
                errors,
                ApplicationEventValidationError.SystemEventRequiresSystemActor);
        }

        /// <summary>
        /// 所有測試共用的 Application 建立時間，固定保留台灣時區。
        /// </summary>
        private static readonly DateTimeOffset ApplicationCreatedAt = new DateTimeOffset(
            2026,
            9,
            6,
            12,
            0,
            0,
            TimeSpan.FromHours(8));

        /// <summary>
        /// 建立一筆必要欄位齊全的一般投遞事件。
        /// </summary>
        private static ApplicationEvent CreateValidEvent()
        {
            return new ApplicationEvent
            {
                Id = ApplicationEventIdGenerator.Create(),
                ApplicationId = ApplicationIdGenerator.Create(),
                EventType = ApplicationEventType.Applied,
                OccurredAt = ApplicationCreatedAt,
                RecordedAt = ApplicationCreatedAt,
                Actor = EventActor.Candidate
            };
        }

        /// <summary>
        /// 建立一筆 migration 專用的最小合法狀態快照。
        /// </summary>
        private static ApplicationEvent CreateMigrationSnapshot()
        {
            ApplicationEvent applicationEvent = CreateValidEvent();
            applicationEvent.EventType = ApplicationEventType.MigrationSnapshot;
            applicationEvent.Actor = EventActor.System;
            applicationEvent.SnapshotStage = ApplicationStage.Applied;
            applicationEvent.TimePrecision = ApplicationEvent.DateTimePrecision;
            return applicationEvent;
        }

        /// <summary>
        /// 建立一筆指向其他事件的最小合法資料修正事件。
        /// </summary>
        private static ApplicationEvent CreateValidCorrection()
        {
            ApplicationEvent applicationEvent = CreateValidEvent();
            applicationEvent.EventType = ApplicationEventType.DataCorrected;
            applicationEvent.Actor = EventActor.System;
            applicationEvent.SupersedesEventId = ApplicationEventIdGenerator.Create();
            return applicationEvent;
        }
    }
}
