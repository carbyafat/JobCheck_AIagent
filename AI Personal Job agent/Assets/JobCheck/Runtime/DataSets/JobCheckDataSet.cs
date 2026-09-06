using System.Collections.Generic;

namespace JobCheck.Domain
{
    /// <summary>
    /// 代表一次驗證或讀取時可見的完整 JobCheck 資料集合。
    /// 此物件只提供跨模型關聯視圖，不負責讀寫檔案，也不是資料庫。
    /// </summary>
    public sealed class JobCheckDataSet
    {
        /// <summary>
        /// 建立資料集合的唯讀快照。傳入 null 會視為空集合，且後續修改原始 List 不會改變此快照。
        /// 集合內的 Domain 物件本身仍由呼叫端持有，Validator 不得修改它們。
        /// </summary>
        public JobCheckDataSet(
            IEnumerable<Company> companies,
            IEnumerable<JobPosting> jobPostings,
            IEnumerable<Application> applications,
            IEnumerable<ApplicationEvent> applicationEvents)
        {
            Companies = Copy(companies);
            JobPostings = Copy(jobPostings);
            Applications = Copy(applications);
            ApplicationEvents = Copy(applicationEvents);
        }

        /// <summary>
        /// 本次可見的公司集合，用來驗證 JobPosting.CompanyId。
        /// </summary>
        public IReadOnlyList<Company> Companies { get; }

        /// <summary>
        /// 本次可見的職缺集合，用來驗證 Application.JobPostingId。
        /// </summary>
        public IReadOnlyList<JobPosting> JobPostings { get; }

        /// <summary>
        /// 本次可見的應徵集合，用來驗證事件與前次投遞連結。
        /// </summary>
        public IReadOnlyList<Application> Applications { get; }

        /// <summary>
        /// 本次可見的應徵事件集合；事件仍由 ApplicationId 歸屬到特定應徵。
        /// </summary>
        public IReadOnlyList<ApplicationEvent> ApplicationEvents { get; }

        /// <summary>
        /// 複製來源集合，避免驗證期間受到呼叫端新增、刪除或重新排序影響。
        /// </summary>
        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> source)
        {
            return (source == null ? new List<T>() : new List<T>(source)).AsReadOnly();
        }
    }
}
