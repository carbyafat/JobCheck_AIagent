using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace JobCheck.Domain.Tests
{
    public sealed class JobPreferenceMatchEngineTests
    {
        [Test]
        public void Compare_MatchesStructuredJobConditionsAndScoresPartialSalary()
        {
            var preferences = new JobSearchPreferences
            {
                TargetRoles = new List<string> { "後端工程師" },
                Industries = new List<string> { "資訊軟體業" },
                AcceptedRegions = new List<string> { "台北市" },
                EmploymentTypes = new List<string> { "正職" },
                WorkModes = new List<string> { "混合" },
                WorkSchedules = new List<string> { "一般日班" },
                MinimumSalary = 50000,
                DesiredSalary = 70000,
                TargetImportance = PreferenceImportance.Required,
                SalaryImportance = PreferenceImportance.Required,
                ArrangementImportance = PreferenceImportance.Preferred,
                ScheduleImportance = PreferenceImportance.Preferred
            };
            var company = new Company { Industry = "資訊軟體業" };
            var job = new JobPosting
            {
                Title = "資深後端工程師",
                Compensation = new JobCompensation
                {
                    Type = "range",
                    Period = "monthly",
                    Currency = "TWD",
                    Minimum = 55000,
                    Maximum = 75000
                },
                Location = new JobLocation { City = "台北市", WorkMode = "hybrid" },
                WorkConditions = new JobWorkConditions
                {
                    EmploymentType = "全職",
                    WorkingHours = "一般日班 09:00–18:00"
                }
            };

            JobPreferenceMatchResult result = JobPreferenceMatchEngine.Compare(
                preferences, company, job);
            JobPreferenceScore score = JobPreferenceScoreCalculator.Calculate(result);

            Assert.That(result.Items.Count, Is.EqualTo(7));
            Assert.That(result.Items.Single(item => item.Category == JobPreferenceCategory.Salary)
                .Status, Is.EqualTo(JobPreferenceMatchStatus.PartialMatch));
            Assert.That(result.Items.Where(item => item.Category != JobPreferenceCategory.Salary)
                .All(item => item.Status == JobPreferenceMatchStatus.Match), Is.True);
            Assert.That(score.Score, Is.EqualTo(90));
            Assert.That(score.RequiredMismatches, Is.Empty);
        }

        [Test]
        public void Compare_RequiredMismatch_IsReportedAsBlocker()
        {
            var preferences = new JobSearchPreferences
            {
                TargetRoles = new List<string> { "前端工程師" },
                TargetImportance = PreferenceImportance.Required
            };

            JobPreferenceMatchResult result = JobPreferenceMatchEngine.Compare(
                preferences,
                new Company(),
                new JobPosting { Title = "後端工程師" });
            JobPreferenceScore score = JobPreferenceScoreCalculator.Calculate(result);

            Assert.That(score.Score, Is.EqualTo(0));
            Assert.That(score.RequiredMismatches.Count, Is.EqualTo(1));
            Assert.That(score.RequiredMismatches[0].Category,
                Is.EqualTo(JobPreferenceCategory.TargetRole));
        }

        [Test]
        public void Compare_MissingJobData_RemainsUnknownAndDoesNotInventZeroScore()
        {
            var preferences = new JobSearchPreferences
            {
                AcceptedRegions = new List<string> { "台北市" },
                MinimumSalary = 50000,
                ArrangementImportance = PreferenceImportance.Required,
                SalaryImportance = PreferenceImportance.Required
            };
            var job = new JobPosting
            {
                Title = "工程師",
                Compensation = new JobCompensation
                {
                    Type = "negotiable",
                    Period = "monthly"
                }
            };

            JobPreferenceMatchResult result = JobPreferenceMatchEngine.Compare(
                preferences, new Company(), job);
            JobPreferenceScore score = JobPreferenceScoreCalculator.Calculate(result);

            Assert.That(result.Items.All(item =>
                item.Status == JobPreferenceMatchStatus.JobDataUnknown), Is.True);
            Assert.That(score.Score, Is.Null);
            Assert.That(score.UnknownCount, Is.EqualTo(2));
            Assert.That(score.RequiredMismatches, Is.Empty);
        }

        [Test]
        public void Compare_SalaryBelowHardFloor_IsConfirmedMismatch()
        {
            var preferences = new JobSearchPreferences
            {
                MinimumSalary = 60000,
                SalaryImportance = PreferenceImportance.Required
            };
            var job = new JobPosting
            {
                Title = "工程師",
                Compensation = new JobCompensation
                {
                    Type = "range",
                    Period = "monthly",
                    Currency = "TWD",
                    Minimum = 40000,
                    Maximum = 55000
                }
            };

            JobPreferenceScore score = JobPreferenceScoreCalculator.Calculate(
                JobPreferenceMatchEngine.Compare(preferences, new Company(), job));

            Assert.That(score.ConfirmedMismatchCount, Is.EqualTo(1));
            Assert.That(score.RequiredMismatches.Count, Is.EqualTo(1));
        }

        [Test]
        public void Compare_AiContextFields_AreVisibleButExcludedFromScore()
        {
            var preferences = new JobSearchPreferences
            {
                MaximumCommuteMinutes = 45,
                OvertimePreference = "不接受常態加班",
                TravelPreference = "一年最多兩次",
                RelocationPreference = "不接受搬遷"
            };

            JobPreferenceMatchResult result = JobPreferenceMatchEngine.Compare(
                preferences, new Company(), new JobPosting { Title = "工程師" });
            JobPreferenceScore score = JobPreferenceScoreCalculator.Calculate(result);

            Assert.That(result.Items.Count, Is.EqualTo(4));
            Assert.That(result.Items.All(item =>
                item.Status == JobPreferenceMatchStatus.AiContextOnly), Is.True);
            Assert.That(score.Score, Is.Null);
            Assert.That(score.AiContextCount, Is.EqualTo(4));
        }
    }
}
