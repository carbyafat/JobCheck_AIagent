namespace JobCheck.Domain
{
    /// <summary>
    /// ApplicationEvent 驗證時可能回報的穩定錯誤代碼。
    /// UI、migration 與 repository 應依代碼判斷錯誤種類，顯示文字由外層決定。
    /// </summary>
    public enum ApplicationEventValidationError
    {
        /// <summary>
        /// 傳入的 ApplicationEvent 物件本身不存在。
        /// </summary>
        MissingApplicationEvent,

        /// <summary>
        /// 事件 ID 為 null、空字串或純空白。
        /// </summary>
        MissingId,

        /// <summary>
        /// 新事件 ID 不符合 evt_ 加上 32 位小寫十六進位 UUID。
        /// </summary>
        InvalidNewId,

        /// <summary>
        /// schema version 缺漏或不是目前支援的 0.2。
        /// </summary>
        UnsupportedSchemaVersion,

        /// <summary>
        /// 沒有指定事件所屬的 Application ID。
        /// </summary>
        MissingApplicationId,

        /// <summary>
        /// EventType 數值不是目前定義的事件種類。
        /// </summary>
        InvalidEventType,

        /// <summary>
        /// Actor 數值不是目前定義的事件角色。
        /// </summary>
        InvalidActor,

        /// <summary>
        /// 沒有提供事件實際發生時間。
        /// </summary>
        MissingOccurredAt,

        /// <summary>
        /// 沒有提供事件被輸入系統的時間。
        /// </summary>
        MissingRecordedAt,

        /// <summary>
        /// RecordedAt 早於所屬 Application 的建立時間。
        /// </summary>
        RecordedBeforeApplicationCreated,

        /// <summary>
        /// TimePrecision 有值，但不是目前允許的 date。
        /// </summary>
        InvalidTimePrecision,

        /// <summary>
        /// 一般新事件誤用了只允許 migration 建立的 MigrationSnapshot。
        /// </summary>
        MigrationSnapshotNotAllowed,

        /// <summary>
        /// MigrationSnapshot 沒有提供轉換當下的 SnapshotStage。
        /// </summary>
        MissingSnapshotStage,

        /// <summary>
        /// SnapshotStage 數值不是目前定義的 ApplicationStage。
        /// </summary>
        InvalidSnapshotStage,

        /// <summary>
        /// 非 MigrationSnapshot 事件卻填入了 SnapshotStage。
        /// </summary>
        SnapshotStageOnNonMigrationEvent,

        /// <summary>
        /// 新事件或一般事件的 SnapshotStage 使用了只允許 migration／修復的 Unknown。
        /// </summary>
        UnknownSnapshotStageNotAllowed,

        /// <summary>
        /// DataCorrected 沒有指定要排除的原事件 ID。
        /// </summary>
        MissingSupersedesEventId,

        /// <summary>
        /// 非 DataCorrected 事件卻填入 SupersedesEventId。
        /// </summary>
        SupersedesEventIdOnNonCorrectionEvent,

        /// <summary>
        /// DataCorrected 指向自己，無法形成有效修正。
        /// </summary>
        SupersedesSelf,

        /// <summary>
        /// MigrationSnapshot 或 DataCorrected 等系統事件沒有使用 System actor。
        /// </summary>
        SystemEventRequiresSystemActor
    }
}
