using System;
using System.Collections.Generic;
using System.Linq;
using JobCheck.Domain;

namespace JobCheck.Persistence
{
    /// <summary>
    /// 由既有母資料與確定性比對結果建立 AI 分析快照。
    /// 不讀取 Application、應徵事件、追蹤日期或私人應徵備註。
    /// </summary>
    public static class AiJobFitRequestFactory
    {
        public static AiJobFitRequestCreationResult Create(
            Company company,
            JobPosting job,
            CareerProfile profile,
            RequirementMatchResult requirementMatch,
            RequirementScore requirementScore,
            JobPreferenceMatchResult preferenceMatch,
            JobPreferenceScore preferenceScore,
            DateTimeOffset createdAt,
            string requestId = null)
        {
            var issues = new List<string>();
            if (company == null) issues.Add("找不到職缺所屬公司資料。");
            if (job == null) issues.Add("找不到目前職缺資料。");
            if (profile == null) issues.Add("找不到個人履歷資料。");
            if (job != null && string.IsNullOrWhiteSpace(job.Id))
                issues.Add("目前職缺缺少 job_id。");

            if (issues.Count > 0)
                return new AiJobFitRequestCreationResult(null, issues);

            PersistenceConversionResult<JobPostingDto> jobMapping =
                JobPostingDtoMapper.ToDto(job);
            PersistenceConversionResult<CareerProfileDto> profileMapping =
                CareerProfileDtoMapper.ToDto(profile);
            AppendMappingIssues(issues, "職缺", jobMapping.Issues);
            AppendMappingIssues(issues, "履歷", profileMapping.Issues);
            if (jobMapping.Value == null || profileMapping.Value == null || issues.Count > 0)
                return new AiJobFitRequestCreationResult(null, issues);

            JobPostingDto jobSnapshot = jobMapping.Value;
            CareerProfileDto profileSnapshot = profileMapping.Value;
            RemoveInternalIdentifiers(jobSnapshot, profileSnapshot);

            string id = string.IsNullOrWhiteSpace(requestId)
                ? Guid.NewGuid().ToString("N")
                : requestId.Trim();
            if (!Guid.TryParse(id, out _))
            {
                issues.Add("request_id 必須是有效 UUID。");
                return new AiJobFitRequestCreationResult(null, issues);
            }

            var package = new AiJobFitRequestPackageDto
            {
                format = AiExchangeContract.Format,
                schema_version = AiExchangeContract.SchemaVersion,
                message_type = AiExchangeContract.RequestMessageType,
                task_type = AiExchangeContract.JobFitAnalysisTaskType,
                request_id = id,
                job_id = job.Id,
                created_at = createdAt.ToUniversalTime().ToString("o"),
                payload = new AiJobFitRequestPayloadDto
                {
                    company = new AiCompanySnapshotDto
                    {
                        name = company.Name,
                        industry = company.Industry,
                        risk_flags = Copy(company.RiskFlags)
                    },
                    job = jobSnapshot,
                    resume = profileSnapshot,
                    resume_to_job = MapRequirementComparison(
                        requirementMatch,
                        requirementScore),
                    job_to_preferences = MapPreferenceComparison(
                        preferenceMatch,
                        preferenceScore),
                    data_scope = new List<string>
                    {
                        "目前職缺與公司公開資料",
                        "個人履歷內容與公開作品連結",
                        "求職條件",
                        "V0.2.7 履歷對職缺規則比對",
                        "V0.2.8 職缺對求職條件規則比對"
                    },
                    excluded_data = new List<string>
                    {
                        "應徵事件歷程",
                        "下次追蹤日期",
                        "私人應徵備註",
                        "本機絕對路徑",
                        "履歷與職缺的內部子項目 ID"
                    },
                    instructions = CreateInstructions(id, job.Id)
                }
            };

            return new AiJobFitRequestCreationResult(package, issues);
        }

        private static AiJobFitInstructionsDto CreateInstructions(
            string requestId,
            string jobId)
        {
            return new AiJobFitInstructionsDto
            {
                response_language = "zh-TW",
                objective = "在不改寫原始資料的前提下，解釋履歷、職缺與求職條件之間的語意適配情況。",
                rules = new List<string>
                {
                    "只能根據本封包 payload 內的資料判斷，不得捏造未提供的經歷、能力或職缺條件。",
                    "資料未提供時列入 missing_information，不可直接當成不符合。",
                    "V0.2.7 與 V0.2.8 分數是確定性規則結果；不得建立另一個 0 到 100 的 AI 分數。",
                    "recommendation 只能是 apply、consider 或 skip。",
                    "confidence 只能是 low、medium 或 high。",
                    "每項 strengths、gaps、risks 應以 evidence_refs 指向 payload 的欄位路徑。",
                    "僅輸出一個符合 response_template 形狀的 JSON 物件，不得加 Markdown 程式碼圍欄或前後說明。",
                    "不得在回傳內容中要求 JobCheck 自動修改履歷、職缺或求職條件。"
                },
                response_template = new AiJobFitResultPackageDto
                {
                    format = AiExchangeContract.Format,
                    schema_version = AiExchangeContract.SchemaVersion,
                    message_type = AiExchangeContract.ResultMessageType,
                    task_type = AiExchangeContract.JobFitAnalysisTaskType,
                    request_id = requestId,
                    job_id = jobId,
                    status = "completed",
                    producer = new AiResultProducerDto
                    {
                        provider = "填入實際服務名稱",
                        model = "填入實際模型名稱",
                        generated_at = "ISO 8601 時間；未知可留空"
                    },
                    result = new AiJobFitAnalysisDto
                    {
                        recommendation = "apply | consider | skip",
                        confidence = "low | medium | high",
                        summary = "繁體中文摘要",
                        strengths = new List<AiInsightDto>(),
                        gaps = new List<AiInsightDto>(),
                        risks = new List<AiInsightDto>(),
                        missing_information = new List<string>(),
                        interview_questions = new List<string>(),
                        suggested_actions = new List<string>()
                    }
                }
            };
        }

        private static AiRuleComparisonSnapshotDto MapRequirementComparison(
            RequirementMatchResult match,
            RequirementScore score)
        {
            var result = new AiRuleComparisonSnapshotDto();
            if (score != null)
            {
                result.has_score = score.Score.HasValue;
                result.score = score.Score.GetValueOrDefault();
                result.assessable_count = score.AssessableCount;
                result.match_count = score.MatchCount;
                result.not_evidenced_count = score.NotEvidencedCount;
                result.confirmed_mismatch_count = score.ConfirmedMismatchCount;
                result.unknown_count = score.UnclearCount;
            }

            if (match?.Items == null) return result;
            foreach (RequirementMatchItem item in match.Items.Where(item => item != null))
            {
                result.items.Add(new AiRuleMatchItemDto
                {
                    category = item.Category.ToString(),
                    status = item.Status.ToString(),
                    importance = item.Importance.ToString(),
                    label = item.Label,
                    expected = item.RequiredValue,
                    actual = item.ActualValue,
                    explanation = item.Explanation
                });
            }

            return result;
        }

        private static AiRuleComparisonSnapshotDto MapPreferenceComparison(
            JobPreferenceMatchResult match,
            JobPreferenceScore score)
        {
            var result = new AiRuleComparisonSnapshotDto();
            if (score != null)
            {
                result.has_score = score.Score.HasValue;
                result.score = score.Score.GetValueOrDefault();
                result.assessable_count = score.AssessableCount;
                result.match_count = score.MatchCount;
                result.partial_match_count = score.PartialMatchCount;
                result.confirmed_mismatch_count = score.ConfirmedMismatchCount;
                result.unknown_count = score.UnknownCount;
                result.ai_context_count = score.AiContextCount;
            }

            if (match?.Items == null) return result;
            foreach (JobPreferenceMatchItem item in match.Items.Where(item => item != null))
            {
                result.items.Add(new AiRuleMatchItemDto
                {
                    category = item.Category.ToString(),
                    status = item.Status.ToString(),
                    importance = item.Importance.ToString(),
                    label = item.Label,
                    expected = item.PreferredValue,
                    actual = item.JobValue,
                    explanation = item.Explanation
                });
            }

            return result;
        }

        private static void RemoveInternalIdentifiers(
            JobPostingDto job,
            CareerProfileDto profile)
        {
            job.id = null;
            job.company_id = null;
            job.legacy_id = null;

            profile.id = null;
            profile.created_at = null;
            profile.updated_at = null;
            foreach (CareerProfileLinkDto item in profile.links ?? new List<CareerProfileLinkDto>())
                if (item != null) item.id = null;
            foreach (CareerSkillDto item in profile.skills ?? new List<CareerSkillDto>())
                if (item != null) item.id = null;
            foreach (CareerExperienceDto item in profile.experiences ?? new List<CareerExperienceDto>())
                if (item != null) item.id = null;
            foreach (CareerProjectDto item in profile.projects ?? new List<CareerProjectDto>())
                if (item != null) item.id = null;
            foreach (CareerEducationDto item in profile.educations ?? new List<CareerEducationDto>())
                if (item != null) item.id = null;
            foreach (CareerLanguageDto item in profile.languages ?? new List<CareerLanguageDto>())
                if (item != null) item.id = null;
            if (profile.job_preferences != null)
                profile.job_preferences.updated_at = null;
        }

        private static void AppendMappingIssues(
            ICollection<string> output,
            string label,
            IEnumerable<PersistenceConversionIssue> mappingIssues)
        {
            if (mappingIssues == null) return;
            foreach (PersistenceConversionIssue issue in mappingIssues)
            {
                output.Add(label + "欄位轉換失敗：" + issue.FieldPath + " " + issue.Error);
            }
        }

        private static List<string> Copy(IEnumerable<string> values)
        {
            return values == null
                ? new List<string>()
                : values.Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim()).ToList();
        }
    }
}
