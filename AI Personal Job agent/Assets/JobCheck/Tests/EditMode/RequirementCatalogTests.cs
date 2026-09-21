using NUnit.Framework;

namespace JobCheck.Domain.Tests
{
    public sealed class RequirementCatalogTests
    {
        [TestCase("C#", "csharp")]
        [TestCase("C Sharp", "csharp")]
        [TestCase("ASP.NET Core", "aspnet_core")]
        [TestCase("Vue.js", "vue")]
        [TestCase("RESTful API", "rest_api")]
        public void NormalizeSkill_KnownAlias_ReturnsCanonicalId(
            string source,
            string expected)
        {
            Assert.That(RequirementCatalog.NormalizeSkill(source), Is.EqualTo(expected));
        }

        [Test]
        public void NormalizeSkill_DoesNotInferRelatedTechnology()
        {
            Assert.That(RequirementCatalog.AreSameSkill("Unity", "C#"), Is.False);
            Assert.That(RequirementCatalog.AreSameSkill("GitHub", "Git"), Is.False);
        }

        [TestCase("英文", "en")]
        [TestCase("English", "en")]
        [TestCase("日語", "ja")]
        public void NormalizeLanguage_KnownAlias_ReturnsCanonicalId(
            string source,
            string expected)
        {
            Assert.That(RequirementCatalog.NormalizeLanguage(source), Is.EqualTo(expected));
        }
    }
}
