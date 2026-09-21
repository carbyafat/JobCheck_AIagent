using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JobCheck.Domain
{
    /// <summary>
    /// V0.2.7 內建的可重現比對字典。只收錄明確同義詞，不推論技術之間的隱含關係。
    /// </summary>
    public static class RequirementCatalog
    {
        private static readonly Dictionary<string, string> SkillAliases =
            CreateSkillAliases();

        private static readonly Dictionary<string, string> LanguageAliases =
            CreateLanguageAliases();

        public static string NormalizeSkill(string value)
        {
            return Normalize(value, SkillAliases);
        }

        public static string NormalizeLanguage(string value)
        {
            return Normalize(value, LanguageAliases);
        }

        public static bool AreSameSkill(string left, string right)
        {
            string normalizedLeft = NormalizeSkill(left);
            return normalizedLeft.Length > 0
                && string.Equals(normalizedLeft, NormalizeSkill(right),
                    StringComparison.Ordinal);
        }

        private static string Normalize(
            string value,
            IDictionary<string, string> aliases)
        {
            string key = NormalizeKey(value);
            if (key.Length == 0)
            {
                return string.Empty;
            }

            return aliases.TryGetValue(key, out string canonical) ? canonical : key;
        }

        private static Dictionary<string, string> CreateSkillAliases()
        {
            var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
            Add(aliases, "csharp", "c#", "c sharp", "csharp");
            Add(aliases, "dotnet", ".net", "dotnet", "dot net");
            Add(aliases, "aspnet_core", "asp.net core", "aspnet core", "asp net core");
            Add(aliases, "unity", "unity", "unity3d");
            Add(aliases, "git", "git");
            Add(aliases, "github", "github", "git hub");
            Add(aliases, "sql", "sql");
            Add(aliases, "mssql", "mssql", "ms sql", "sql server", "microsoft sql server");
            Add(aliases, "mysql", "mysql", "my sql");
            Add(aliases, "javascript", "javascript", "java script", "js");
            Add(aliases, "typescript", "typescript", "type script", "ts");
            Add(aliases, "react", "react", "reactjs", "react.js");
            Add(aliases, "vue", "vue", "vuejs", "vue.js");
            Add(aliases, "html", "html", "html5");
            Add(aliases, "css", "css", "css3");
            Add(aliases, "rest_api", "rest api", "restful api", "restful");
            Add(aliases, "docker", "docker");
            Add(aliases, "python", "python");
            Add(aliases, "java", "java");
            return aliases;
        }

        private static Dictionary<string, string> CreateLanguageAliases()
        {
            var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
            Add(aliases, "zh", "中文", "華語", "國語", "chinese", "mandarin");
            Add(aliases, "en", "英文", "英語", "english");
            Add(aliases, "ja", "日文", "日語", "japanese");
            Add(aliases, "ko", "韓文", "韓語", "korean");
            return aliases;
        }

        private static void Add(
            IDictionary<string, string> aliases,
            string canonical,
            params string[] values)
        {
            foreach (string value in values)
            {
                aliases[NormalizeKey(value)] = canonical;
            }
        }

        private static string NormalizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Trim().Normalize(NormalizationForm.FormKC)
                .ToLower(CultureInfo.InvariantCulture);
            var builder = new StringBuilder(normalized.Length);
            bool previousWasSpace = false;
            foreach (char character in normalized)
            {
                bool isSeparator = char.IsWhiteSpace(character)
                    || character == '-' || character == '_' || character == '/';
                if (isSeparator)
                {
                    if (builder.Length > 0 && !previousWasSpace)
                    {
                        builder.Append(' ');
                    }

                    previousWasSpace = true;
                    continue;
                }

                builder.Append(character);
                previousWasSpace = false;
            }

            return builder.ToString().Trim();
        }
    }
}
