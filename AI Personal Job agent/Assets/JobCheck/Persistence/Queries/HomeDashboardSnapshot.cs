using System;
using System.Collections.Generic;
using System.IO;
using JobCheck.Domain;

namespace JobCheck.Persistence
{
    /// <summary>首頁需要的唯讀摘要；所有求職數字都來自既有分析查詢。</summary>
    public sealed class HomeDashboardSnapshot
    {
        public HomeDashboardSnapshot(
            AnalyticsSummary analytics,
            bool hasSavedProfile,
            bool hasProfileContent,
            int completedProfileSectionCount,
            DateTimeOffset? profileUpdatedAt,
            DateTimeOffset refreshedAt)
        {
            Analytics = analytics ?? new AnalyticsSummary(0, 0, 0, 0, 0);
            HasSavedProfile = hasSavedProfile;
            HasProfileContent = hasProfileContent;
            CompletedProfileSectionCount = completedProfileSectionCount;
            ProfileUpdatedAt = profileUpdatedAt;
            RefreshedAt = refreshedAt;
        }

        public AnalyticsSummary Analytics { get; }
        public bool HasSavedProfile { get; }
        public bool HasProfileContent { get; }
        public int CompletedProfileSectionCount { get; }
        public DateTimeOffset? ProfileUpdatedAt { get; }
        public DateTimeOffset RefreshedAt { get; }
        public int AttentionItemCount =>
            Analytics.NeedsReviewCount + Analytics.NoResponseMarkedCount;
        public bool HasJobData => Analytics.JobPostingCount > 0;
    }

    public static class HomeDashboardQuery
    {
        public const int TotalProfileSectionCount = 7;

        public static PersistenceStorageResult<HomeDashboardSnapshot> Load(
            string dataRoot,
            string personalDataRoot,
            DateTimeOffset refreshedAt)
        {
            PersistenceStorageResult<ApplicationAnalyticsReport> analytics =
                ApplicationAnalyticsQuery.Load(dataRoot);
            if (!analytics.IsSuccess)
            {
                return new PersistenceStorageResult<HomeDashboardSnapshot>(
                    null,
                    analytics.Issues);
            }

            bool hasSavedProfile = !string.IsNullOrWhiteSpace(personalDataRoot)
                && File.Exists(CareerProfileRepository.GetProfilePath(personalDataRoot));
            PersistenceStorageResult<CareerProfile> profile =
                CareerProfileRepository.Load(personalDataRoot);
            if (!profile.IsSuccess)
            {
                return new PersistenceStorageResult<HomeDashboardSnapshot>(
                    null,
                    profile.Issues);
            }

            return new PersistenceStorageResult<HomeDashboardSnapshot>(
                Build(analytics.Value, profile.Value, hasSavedProfile, refreshedAt),
                Array.Empty<PersistenceStorageIssue>());
        }

        public static HomeDashboardSnapshot Build(
            ApplicationAnalyticsReport report,
            CareerProfile profile,
            bool hasSavedProfile,
            DateTimeOffset refreshedAt)
        {
            AnalyticsSummary analytics = report?.Summary
                ?? new AnalyticsSummary(0, 0, 0, 0, 0);
            int completedSections = CountCompletedSections(profile);
            return new HomeDashboardSnapshot(
                analytics,
                hasSavedProfile,
                hasSavedProfile && completedSections > 0,
                completedSections,
                hasSavedProfile ? profile?.UpdatedAt : null,
                refreshedAt);
        }

        private static int CountCompletedSections(CareerProfile profile)
        {
            if (profile == null)
            {
                return 0;
            }

            int count = string.IsNullOrWhiteSpace(profile.Summary) ? 0 : 1;
            count += HasItems(profile.Links) ? 1 : 0;
            count += HasItems(profile.Skills) ? 1 : 0;
            count += HasItems(profile.Experiences) ? 1 : 0;
            count += HasItems(profile.Projects) ? 1 : 0;
            count += HasItems(profile.Educations) ? 1 : 0;
            count += HasItems(profile.Languages) ? 1 : 0;
            return count;
        }

        private static bool HasItems<T>(ICollection<T> items) where T : class
        {
            return items != null && items.Count > 0;
        }
    }
}
