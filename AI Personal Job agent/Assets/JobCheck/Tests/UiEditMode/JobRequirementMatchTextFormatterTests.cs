using System.Collections.Generic;
using System;
using System.Linq;
using System.Reflection;
using JobCheck.Domain;
using NUnit.Framework;

namespace JobCheck.Ui.Editor.Tests
{
    public sealed class JobRequirementMatchTextFormatterTests
    {
        [Test]
        public void Format_SeparatesNotEvidencedFromConfirmedMismatch()
        {
            var match = new RequirementMatchResult
            {
                Items = new List<RequirementMatchItem>
                {
                    Item("C#", RequirementMatchStatus.Match),
                    Item("Docker", RequirementMatchStatus.NotEvidenced),
                    Item("年資", RequirementMatchStatus.ConfirmedMismatch)
                }
            };
            RequirementScore score = RequirementScoreCalculator.Calculate(match);

            string text = Format(match, score);

            StringAssert.Contains("未證明 1", text);
            StringAssert.Contains("不符合 1", text);
            StringAssert.Contains("[未證明] 技能－Docker", text);
            StringAssert.Contains("[不符合] 技能－年資", text);
        }

        [Test]
        public void Format_NoAssessableConditions_ExplainsWhyNoScoreExists()
        {
            var match = new RequirementMatchResult
            {
                Items = new List<RequirementMatchItem>
                {
                    Item("工作年資", RequirementMatchStatus.RequirementUnclear)
                }
            };

            string text = Format(
                match,
                RequirementScoreCalculator.Calculate(match));

            StringAssert.Contains("尚無可判斷條件", text);
            StringAssert.Contains("條件不明 1", text);
        }

        [Test]
        public void Format_BidirectionalResult_SeparatesPreferenceAndAiContext()
        {
            var requirementMatch = new RequirementMatchResult();
            var preferenceMatch = new JobPreferenceMatchResult
            {
                Items = new List<JobPreferenceMatchItem>
                {
                    new JobPreferenceMatchItem
                    {
                        Category = JobPreferenceCategory.Salary,
                        Status = JobPreferenceMatchStatus.ConfirmedMismatch,
                        Importance = PreferenceImportance.Required,
                        Label = "薪資",
                        PreferredValue = "最低 60,000／月",
                        JobValue = "TWD 40,000–55,000／月"
                    },
                    new JobPreferenceMatchItem
                    {
                        Category = JobPreferenceCategory.Commute,
                        Status = JobPreferenceMatchStatus.AiContextOnly,
                        Importance = PreferenceImportance.Indifferent,
                        Label = "通勤時間",
                        PreferredValue = "最多 45 分鐘"
                    }
                }
            };

            string text = FormatBidirectional(
                requirementMatch,
                RequirementScoreCalculator.Calculate(requirementMatch),
                preferenceMatch,
                JobPreferenceScoreCalculator.Calculate(preferenceMatch));

            StringAssert.Contains("【履歷是否符合職缺】", text);
            StringAssert.Contains("【職缺是否符合求職條件】", text);
            StringAssert.Contains("有必要條件不符合", text);
            StringAssert.Contains("[不符合] 薪資－薪資", text);
            StringAssert.Contains("【保留給後續 AI 判斷】", text);
            StringAssert.Contains("[待 AI] 通勤－通勤時間", text);
        }

        [Test]
        public void FormatCalculation_ShowsWeightsAndActualArithmetic()
        {
            var requirementMatch = new RequirementMatchResult
            {
                Items = new List<RequirementMatchItem>
                {
                    Item("C#", RequirementMatchStatus.Match),
                    Item("Docker", RequirementMatchStatus.ConfirmedMismatch),
                    new RequirementMatchItem
                    {
                        Category = RequirementCategory.Experience,
                        Status = RequirementMatchStatus.Match,
                        Importance = RequirementImportance.Required,
                        Label = "年資"
                    }
                }
            };
            var preferenceMatch = new JobPreferenceMatchResult
            {
                Items = new List<JobPreferenceMatchItem>
                {
                    new JobPreferenceMatchItem
                    {
                        Category = JobPreferenceCategory.Salary,
                        Status = JobPreferenceMatchStatus.PartialMatch,
                        Importance = PreferenceImportance.Required,
                        Label = "薪資"
                    },
                    new JobPreferenceMatchItem
                    {
                        Category = JobPreferenceCategory.WorkMode,
                        Status = JobPreferenceMatchStatus.Match,
                        Importance = PreferenceImportance.Preferred,
                        Label = "工作模式"
                    },
                    new JobPreferenceMatchItem
                    {
                        Category = JobPreferenceCategory.Region,
                        Status = JobPreferenceMatchStatus.JobDataUnknown,
                        Importance = PreferenceImportance.Required,
                        Label = "地區"
                    }
                }
            };

            string text = FormatCalculation(
                requirementMatch,
                RequirementScoreCalculator.Calculate(requirementMatch),
                preferenceMatch,
                JobPreferenceScoreCalculator.Calculate(preferenceMatch));

            StringAssert.Contains("技能：40 × 1 ÷ 2 = 20", text);
            StringAssert.Contains("年資：30 × 1 ÷ 1 = 30", text);
            StringAssert.Contains("總分：50 ÷ 70 × 100 = 71 分", text);
            StringAssert.Contains("薪資－薪資（必要）：2 × 0.5 = 1", text);
            StringAssert.Contains("工作模式－工作模式（偏好）：1 × 1 = 1", text);
            StringAssert.Contains("總分：2 ÷ 3 × 100 = 67 分", text);
            StringAssert.Contains("職缺未提供 1 項", text);
        }

        private static RequirementMatchItem Item(
            string label,
            RequirementMatchStatus status)
        {
            return new RequirementMatchItem
            {
                Category = RequirementCategory.Skill,
                Status = status,
                Importance = RequirementImportance.Required,
                Label = label,
                RequiredValue = "測試需求",
                Explanation = "測試原因"
            };
        }

        private static string Format(RequirementMatchResult match, RequirementScore score)
        {
            Type formatter = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "JobRequirementMatchTextFormatter", false))
                .FirstOrDefault(type => type != null);
            Assert.That(formatter, Is.Not.Null);
            MethodInfo method = formatter.GetMethod(
                "Format",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(RequirementMatchResult), typeof(RequirementScore) },
                null);
            Assert.That(method, Is.Not.Null);
            return (string)method.Invoke(null, new object[] { match, score });
        }

        private static string FormatBidirectional(
            RequirementMatchResult requirementMatch,
            RequirementScore requirementScore,
            JobPreferenceMatchResult preferenceMatch,
            JobPreferenceScore preferenceScore)
        {
            Type formatter = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "JobRequirementMatchTextFormatter", false))
                .FirstOrDefault(type => type != null);
            Assert.That(formatter, Is.Not.Null);
            MethodInfo method = formatter.GetMethod(
                "Format",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(RequirementMatchResult),
                    typeof(RequirementScore),
                    typeof(JobPreferenceMatchResult),
                    typeof(JobPreferenceScore)
                },
                null);
            Assert.That(method, Is.Not.Null);
            return (string)method.Invoke(null, new object[]
            {
                requirementMatch, requirementScore, preferenceMatch, preferenceScore
            });
        }

        private static string FormatCalculation(
            RequirementMatchResult requirementMatch,
            RequirementScore requirementScore,
            JobPreferenceMatchResult preferenceMatch,
            JobPreferenceScore preferenceScore)
        {
            Type formatter = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "JobRequirementMatchTextFormatter", false))
                .FirstOrDefault(type => type != null);
            Assert.That(formatter, Is.Not.Null);
            MethodInfo method = formatter.GetMethod(
                "FormatCalculation",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return (string)method.Invoke(null, new object[]
            {
                requirementMatch, requirementScore, preferenceMatch, preferenceScore
            });
        }
    }
}
