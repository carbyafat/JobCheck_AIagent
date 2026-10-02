using System;
using System.Collections.Generic;
using System.Linq;

namespace JobCheck.Persistence
{
    /// <summary>將 AI 回傳視為不可信輸入，解析後再依情境與長度上限驗證。</summary>
    public static class AiJobFitResultValidator
    {
        public const int MaximumJsonCharacters = 1024 * 1024;
        private const int MaximumSummaryCharacters = 4000;
        private const int MaximumInsightCount = 20;
        private const int MaximumListCount = 30;

        public static AiJobFitResultValidationResult ParseAndValidate(
            string json,
            string expectedRequestId,
            string expectedJobId)
        {
            var issues = new List<string>();
            string normalized = RemoveMarkdownFence(json);
            if (string.IsNullOrWhiteSpace(normalized))
                return Failure("AI 回傳內容為空白。");
            if (normalized.Length > MaximumJsonCharacters)
                return Failure("AI 回傳內容超過 1 MB，為避免異常資料已拒絕匯入。");

            if (!PersistenceJsonSerializer.TryDeserialize(
                normalized,
                out AiExchangeEnvelopeProbeDto probe,
                out string probeError))
            {
                return Failure("無法讀取 AI 回傳 JSON：" + probeError);
            }

            ValidateEnvelope(probe, issues);
            if (issues.Count > 0)
                return new AiJobFitResultValidationResult(null, issues);

            if (!PersistenceJsonSerializer.TryDeserialize(
                normalized,
                out AiJobFitResultPackageDto value,
                out string parseError))
            {
                return Failure("AI 回傳格式不符合職缺適配分析結果：" + parseError);
            }

            ValidateIdentity(value, expectedRequestId, expectedJobId, issues);
            ValidateAnalysis(value.result, issues);
            if (issues.Count > 0)
                return new AiJobFitResultValidationResult(null, issues);

            Normalize(value);
            return new AiJobFitResultValidationResult(value, Array.Empty<string>());
        }

        public static string RemoveMarkdownFence(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            string trimmed = value.Trim().TrimStart('\uFEFF');
            if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;

            int firstLineEnd = trimmed.IndexOf('\n');
            int closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstLineEnd < 0 || closingFence <= firstLineEnd) return trimmed;
            return trimmed.Substring(firstLineEnd + 1, closingFence - firstLineEnd - 1).Trim();
        }

        private static void ValidateEnvelope(
            AiExchangeEnvelopeProbeDto probe,
            ICollection<string> issues)
        {
            if (!string.Equals(probe.format, AiExchangeContract.Format, StringComparison.Ordinal))
                issues.Add("這不是 JobCheck AI 交換檔。" );
            if (!string.Equals(probe.schema_version, AiExchangeContract.SchemaVersion,
                StringComparison.Ordinal))
                issues.Add("不支援的 AI 交換格式版本：" + (probe.schema_version ?? "未提供"));
            if (!string.Equals(probe.message_type, AiExchangeContract.ResultMessageType,
                StringComparison.Ordinal))
                issues.Add("選到的不是 AI 分析結果檔；message_type 必須是 result。" );
            if (!string.Equals(probe.task_type, AiExchangeContract.JobFitAnalysisTaskType,
                StringComparison.Ordinal))
                issues.Add("此頁只能匯入 job_fit_analysis 結果。" );
        }

        private static void ValidateIdentity(
            AiJobFitResultPackageDto value,
            string expectedRequestId,
            string expectedJobId,
            ICollection<string> issues)
        {
            if (value == null)
            {
                issues.Add("AI 分析結果不存在。" );
                return;
            }

            if (!Guid.TryParse(value.request_id, out _))
                issues.Add("request_id 不是有效 UUID。" );
            if (!string.IsNullOrWhiteSpace(expectedRequestId)
                && !string.Equals(value.request_id, expectedRequestId, StringComparison.Ordinal))
                issues.Add("request_id 與這次匯出的分析需求不相符。" );
            if (string.IsNullOrWhiteSpace(value.job_id))
                issues.Add("AI 結果缺少 job_id。" );
            else if (!string.IsNullOrWhiteSpace(expectedJobId)
                && !string.Equals(value.job_id, expectedJobId, StringComparison.Ordinal))
                issues.Add("AI 結果屬於其他職缺，不能匯入目前頁面。" );
            if (!string.Equals(value.status, "completed", StringComparison.Ordinal))
                issues.Add("AI 結果尚未完成；status 必須是 completed。" );
        }

        private static void ValidateAnalysis(
            AiJobFitAnalysisDto analysis,
            ICollection<string> issues)
        {
            if (analysis == null)
            {
                issues.Add("AI 結果缺少 result。" );
                return;
            }

            if (!Allowed(analysis.recommendation, "apply", "consider", "skip"))
                issues.Add("recommendation 只能是 apply、consider 或 skip。" );
            if (!Allowed(analysis.confidence, "low", "medium", "high"))
                issues.Add("confidence 只能是 low、medium 或 high。" );
            ValidateText(analysis.summary, "summary", true, MaximumSummaryCharacters, issues);
            ValidateInsights(analysis.strengths, "strengths", issues);
            ValidateInsights(analysis.gaps, "gaps", issues);
            ValidateInsights(analysis.risks, "risks", issues);
            ValidateStrings(analysis.missing_information, "missing_information", issues);
            ValidateStrings(analysis.interview_questions, "interview_questions", issues);
            ValidateStrings(analysis.suggested_actions, "suggested_actions", issues);
        }

        private static void ValidateInsights(
            IList<AiInsightDto> values,
            string field,
            ICollection<string> issues)
        {
            if (values == null) return;
            if (values.Count > MaximumInsightCount)
            {
                issues.Add(field + " 最多允許 " + MaximumInsightCount + " 筆。" );
                return;
            }

            for (int index = 0; index < values.Count; index++)
            {
                AiInsightDto item = values[index];
                if (item == null)
                {
                    issues.Add(field + "[" + index + "] 不可為 null。" );
                    continue;
                }

                ValidateText(item.title, field + "[" + index + "].title", true, 200, issues);
                ValidateText(item.detail, field + "[" + index + "].detail", true, 2000, issues);
                if (item.evidence_refs != null && item.evidence_refs.Count > 20)
                    issues.Add(field + "[" + index + "].evidence_refs 最多允許 20 筆。" );
                foreach (string evidence in item.evidence_refs ?? new List<string>())
                    ValidateText(evidence, field + " evidence_ref", true, 300, issues);
            }
        }

        private static void ValidateStrings(
            IList<string> values,
            string field,
            ICollection<string> issues)
        {
            if (values == null) return;
            if (values.Count > MaximumListCount)
            {
                issues.Add(field + " 最多允許 " + MaximumListCount + " 筆。" );
                return;
            }

            foreach (string value in values)
                ValidateText(value, field, true, 1200, issues);
        }

        private static void ValidateText(
            string value,
            string field,
            bool required,
            int maximum,
            ICollection<string> issues)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (required) issues.Add(field + " 不可空白。" );
                return;
            }

            if (value.Length > maximum)
                issues.Add(field + " 超過 " + maximum + " 個字元。" );
        }

        private static bool Allowed(string value, params string[] allowed)
        {
            return allowed.Any(item => string.Equals(value, item, StringComparison.Ordinal));
        }

        private static void Normalize(AiJobFitResultPackageDto value)
        {
            value.result.strengths = value.result.strengths ?? new List<AiInsightDto>();
            value.result.gaps = value.result.gaps ?? new List<AiInsightDto>();
            value.result.risks = value.result.risks ?? new List<AiInsightDto>();
            value.result.missing_information = value.result.missing_information
                ?? new List<string>();
            value.result.interview_questions = value.result.interview_questions
                ?? new List<string>();
            value.result.suggested_actions = value.result.suggested_actions
                ?? new List<string>();
            foreach (AiInsightDto insight in value.result.strengths
                .Concat(value.result.gaps).Concat(value.result.risks))
            {
                insight.evidence_refs = insight.evidence_refs ?? new List<string>();
            }
        }

        private static AiJobFitResultValidationResult Failure(string message)
        {
            return new AiJobFitResultValidationResult(null, new[] { message });
        }
    }
}
