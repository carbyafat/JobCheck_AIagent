using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JobCheck.Domain;

namespace JobCheck.Persistence
{
    /// <summary>
    /// 列表與詳情頁需要的一筆唯讀資料。
    /// Company 與 JobPosting 在這裡完成關聯，UI 不必自行用 CompanyId 查公司名稱。
    /// CurrentApplication 只會選本人應徵資料；外部匯入不會被當成個人狀態。
    /// </summary>
    public sealed class JobPostingReadOnlyItem
    {
        public JobPostingReadOnlyItem(
            Company company,
            JobPosting jobPosting,
            Application currentApplication,
            DateTimeOffset? lastActivityAt)
        {
            Company = company;
            JobPosting = jobPosting;
            CurrentApplication = currentApplication;
            LastActivityAt = lastActivityAt;
        }

        public Company Company { get; }
        public JobPosting JobPosting { get; }
        public Application CurrentApplication { get; }
        public DateTimeOffset? LastActivityAt { get; }

        /// <summary>
        /// 暫時提供既有 UI 使用的狀態代碼。V0.2 Domain 仍以 ApplicationStage 為準。
        /// </summary>
        public string StatusCode => ToLegacyDisplayStatus(CurrentApplication);

        private static string ToLegacyDisplayStatus(Application application)
        {
            if (application == null)
            {
                return "not_viewed";
            }

            switch (application.CurrentStage)
            {
                case ApplicationStage.Saved:
                    return "interested";
                case ApplicationStage.Applied:
                    return "applied";
                case ApplicationStage.Viewed:
                    return "viewed";
                case ApplicationStage.Contacted:
                    return "contacted";
                case ApplicationStage.InterviewScheduled:
                    return "interview_scheduled";
                case ApplicationStage.InterviewCompleted:
                    return "interviewing";
                case ApplicationStage.WaitingResponse:
                    return "waiting_reply";
                case ApplicationStage.OfferReceived:
                    return "offer";
                case ApplicationStage.RejectedByCompany:
                    return "rejected";
                case ApplicationStage.ClosedByCandidate:
                    return "not_applying";
                default:
                    return "unknown";
            }
        }
    }

    /// <summary>
    /// 一次 V0.2 唯讀查詢的穩定快照。
    /// </summary>
    public sealed class JobPostingReadOnlyList
    {
        public JobPostingReadOnlyList(IEnumerable<JobPostingReadOnlyItem> items)
        {
            Items = new ReadOnlyCollection<JobPostingReadOnlyItem>(
                items == null
                    ? Array.Empty<JobPostingReadOnlyItem>()
                    : items.ToArray());
        }

        public IReadOnlyList<JobPostingReadOnlyItem> Items { get; }
    }

    /// <summary>
    /// 將 Repository 的四種 Domain 集合整理成列表 UI 可直接查詢的唯讀結果。
    /// 此服務不建立目錄、不補資料，也不寫回任何 JSON。
    /// </summary>
    public static class JobPostingReadOnlyQuery
    {
        public static PersistenceStorageResult<JobPostingReadOnlyList> Load(string dataRoot)
        {
            PersistenceStorageResult<JobCheckDataSet> load =
                JobCheckDataRepository.Load(dataRoot);
            if (!load.IsSuccess)
            {
                return new PersistenceStorageResult<JobPostingReadOnlyList>(null, load.Issues);
            }

            Dictionary<string, Company> companies = load.Value.Companies.ToDictionary(
                item => item.Id,
                item => item,
                StringComparer.Ordinal);

            Dictionary<string, Application> currentApplications = load.Value.Applications
                .Where(item => item.SourceType == SourceType.MyApplication)
                .GroupBy(item => item.JobPostingId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    SelectLatestApplication,
                    StringComparer.Ordinal);

            var items = new List<JobPostingReadOnlyItem>();
            foreach (JobPosting jobPosting in load.Value.JobPostings.OrderBy(
                item => item.Id,
                StringComparer.Ordinal))
            {
                companies.TryGetValue(jobPosting.CompanyId, out Company company);
                currentApplications.TryGetValue(jobPosting.Id, out Application application);
                items.Add(new JobPostingReadOnlyItem(
                    company,
                    jobPosting,
                    application,
                    FindLastActivityAt(load.Value, application)));
            }

            return new PersistenceStorageResult<JobPostingReadOnlyList>(
                new JobPostingReadOnlyList(items),
                Array.Empty<PersistenceStorageIssue>());
        }

        /// <summary>
        /// 同一職缺多次投遞時，以最後更新時間選目前要顯示的一筆；時間相同時用 ID 穩定排序。
        /// </summary>
        private static Application SelectLatestApplication(IEnumerable<Application> applications)
        {
            return applications
                .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt ?? DateTimeOffset.MinValue)
                .ThenByDescending(item => item.Id, StringComparer.Ordinal)
                .First();
        }

        /// <summary>
        /// Migration 的 Application 更新時間是搬移時間；畫面上的最後操作應優先採用事件發生時間。
        /// </summary>
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
    }
}
