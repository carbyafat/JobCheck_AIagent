namespace JobCheck.Domain
{
    /// <summary>
    /// 一筆 Application 被收進歷史或整理區的原因。
    /// 封存不是暫停追蹤或備取狀態；仍需採取行動的本人應徵不應因此從主要列表消失。
    /// </summary>
    public enum ArchiveReason
    {
        /// <summary>
        /// 使用者自行將這筆應徵收進封存區。
        /// </summary>
        UserArchived,

        /// <summary>
        /// 這筆資料是重複紀錄，因此不在主要列表顯示。
        /// </summary>
        Duplicate,

        /// <summary>
        /// 這筆資料是匯入後保留的參考紀錄，不當作目前進行中的應徵。
        /// </summary>
        ImportedReference,

        /// <summary>
        /// 其他未列出的封存原因。
        /// </summary>
        Other
    }
}
