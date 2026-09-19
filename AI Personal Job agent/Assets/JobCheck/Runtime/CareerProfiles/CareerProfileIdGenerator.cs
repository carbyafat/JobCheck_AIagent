using System;

namespace JobCheck.Domain
{
    /// <summary>建立履歷及各區塊的穩定 ID，供日後跨裝置同步辨識同一筆資料。</summary>
    public static class CareerProfileIdGenerator
    {
        public static string CreateProfileId() => Create("prf_");
        public static string CreateLinkId() => Create("lnk_");
        public static string CreateSkillId() => Create("skl_");
        public static string CreateExperienceId() => Create("exp_");
        public static string CreateProjectId() => Create("prj_");
        public static string CreateEducationId() => Create("edu_");
        public static string CreateLanguageId() => Create("lng_");

        private static string Create(string prefix)
        {
            return prefix + Guid.NewGuid().ToString("N").ToLowerInvariant();
        }
    }
}
