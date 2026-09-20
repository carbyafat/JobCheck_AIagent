using System;
using System.Collections.Generic;
using JobCheck.Domain;
using NUnit.Framework;

namespace JobCheck.Persistence.Tests
{
    public sealed class HomeDashboardQueryTests
    {
        [Test]
        public void Build_ReusesAnalyticsSummaryWithoutChangingCounts()
        {
            var summary = new AnalyticsSummary(12, 7, 3, 2, 1);
            var report = new ApplicationAnalyticsReport(
                summary, null, null, null, null, null);
            DateTimeOffset refreshedAt = new DateTimeOffset(
                2026, 9, 21, 10, 30, 0, TimeSpan.FromHours(8));

            HomeDashboardSnapshot snapshot = HomeDashboardQuery.Build(
                report, new CareerProfile(), false, refreshedAt);

            Assert.That(snapshot.Analytics, Is.SameAs(summary));
            Assert.That(snapshot.Analytics.JobPostingCount, Is.EqualTo(12));
            Assert.That(snapshot.Analytics.ActiveApplicationCount, Is.EqualTo(3));
            Assert.That(snapshot.AttentionItemCount, Is.EqualTo(3));
            Assert.That(snapshot.RefreshedAt, Is.EqualTo(refreshedAt));
        }

        [Test]
        public void Build_CountsCompletedProfileSections()
        {
            DateTimeOffset updatedAt = DateTimeOffset.Now.AddDays(-2);
            var profile = new CareerProfile
            {
                Summary = "Unity 工具開發",
                UpdatedAt = updatedAt,
                Skills = new List<CareerSkill>
                {
                    new CareerSkill { Name = "Unity" }
                },
                Projects = new List<CareerProject>
                {
                    new CareerProject { Name = "JobCheck" }
                },
                Languages = new List<CareerLanguage>
                {
                    new CareerLanguage { Name = "中文" }
                }
            };

            HomeDashboardSnapshot snapshot = HomeDashboardQuery.Build(
                null, profile, true, DateTimeOffset.Now);

            Assert.That(snapshot.HasSavedProfile, Is.True);
            Assert.That(snapshot.HasProfileContent, Is.True);
            Assert.That(snapshot.CompletedProfileSectionCount, Is.EqualTo(4));
            Assert.That(snapshot.ProfileUpdatedAt, Is.EqualTo(updatedAt));
        }

        [Test]
        public void Build_UnsavedDraftDoesNotPretendProfileExists()
        {
            var profile = new CareerProfile
            {
                Summary = "尚未落盤的草稿",
                UpdatedAt = DateTimeOffset.Now
            };

            HomeDashboardSnapshot snapshot = HomeDashboardQuery.Build(
                null, profile, false, DateTimeOffset.Now);

            Assert.That(snapshot.HasSavedProfile, Is.False);
            Assert.That(snapshot.HasProfileContent, Is.False);
            Assert.That(snapshot.ProfileUpdatedAt, Is.Null);
        }
    }
}
