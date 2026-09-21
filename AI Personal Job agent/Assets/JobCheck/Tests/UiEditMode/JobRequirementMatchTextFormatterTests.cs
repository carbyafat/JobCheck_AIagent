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
                "Format", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return (string)method.Invoke(null, new object[] { match, score });
        }
    }
}
