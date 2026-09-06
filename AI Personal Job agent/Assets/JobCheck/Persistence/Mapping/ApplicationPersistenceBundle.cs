using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JobCheck.Domain;

namespace JobCheck.Persistence
{
    /// <summary>
    /// ApplicationDto 轉成 Domain 後的 Application 與內嵌事件集合。
    /// 這只是 Mapper 回傳容器，不負責狀態推導或檔案讀寫。
    /// </summary>
    public sealed class ApplicationPersistenceBundle
    {
        /// <summary>
        /// 建立一筆應徵與事件集合的不可增刪視圖。
        /// </summary>
        public ApplicationPersistenceBundle(
            Application application,
            IEnumerable<ApplicationEvent> events)
        {
            Application = application;
            var eventSnapshot = events == null
                ? Array.Empty<ApplicationEvent>()
                : new List<ApplicationEvent>(events).ToArray();
            Events = new ReadOnlyCollection<ApplicationEvent>(eventSnapshot);
        }

        /// <summary>
        /// 由 ApplicationDto 主體轉換出的 Domain Application。
        /// </summary>
        public Application Application { get; }

        /// <summary>
        /// 由 ApplicationDto.events 轉換出的 Domain ApplicationEvent 集合。
        /// </summary>
        public IReadOnlyList<ApplicationEvent> Events { get; }
    }
}
