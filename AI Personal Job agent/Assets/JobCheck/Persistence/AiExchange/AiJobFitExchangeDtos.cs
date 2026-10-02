using System;
using System.Collections.Generic;

namespace JobCheck.Persistence
{
    /// <summary>JobCheck 與外部 AI 交換資料時使用的固定識別值。</summary>
    public static class AiExchangeContract
    {
        public const string Format = "jobcheck_ai_exchange";
        public const string SchemaVersion = "1";
        public const string RequestMessageType = "request";
        public const string ResultMessageType = "result";
        public const string JobFitAnalysisTaskType = "job_fit_analysis";
        public const string RequestFileExtension = ".jobcheck-ai-request.json";
        public const string ResultFileExtension = ".jobcheck-ai-result.json";
    }

    /// <summary>
    /// 用於先讀取外層欄位的輕量 DTO。情境頁面確認 task_type 後，才解析完整結果。
    /// </summary>
    [Serializable]
    public sealed class AiExchangeEnvelopeProbeDto
    {
        public string format;
        public string schema_version;
        public string message_type;
        public string task_type;
        public string request_id;
        public string job_id;
    }

    /// <summary>JobCheck 匯出給 AI 的單一職缺適配分析要求。</summary>
    [Serializable]
    public sealed class AiJobFitRequestPackageDto
    {
        public string format;
        public string schema_version;
        public string message_type;
        public string task_type;
        public string request_id;
        public string job_id;
        public string created_at;
        public AiJobFitRequestPayloadDto payload;
    }

    [Serializable]
    public sealed class AiJobFitRequestPayloadDto
    {
        public AiCompanySnapshotDto company;
        public JobPostingDto job;
        public CareerProfileDto resume;
        public AiRuleComparisonSnapshotDto resume_to_job;
        public AiRuleComparisonSnapshotDto job_to_preferences;
        public List<string> data_scope = new List<string>();
        public List<string> excluded_data = new List<string>();
        public AiJobFitInstructionsDto instructions;
    }

    /// <summary>只提供適配分析所需的公司公開資料，不包含私人公司備註。</summary>
    [Serializable]
    public sealed class AiCompanySnapshotDto
    {
        public string name;
        public string industry;
        public List<string> risk_flags = new List<string>();
    }

    [Serializable]
    public sealed class AiRuleComparisonSnapshotDto
    {
        public bool has_score;
        public int score;
        public int assessable_count;
        public int match_count;
        public int partial_match_count;
        public int not_evidenced_count;
        public int confirmed_mismatch_count;
        public int unknown_count;
        public int ai_context_count;
        public List<AiRuleMatchItemDto> items = new List<AiRuleMatchItemDto>();
    }

    [Serializable]
    public sealed class AiRuleMatchItemDto
    {
        public string category;
        public string status;
        public string importance;
        public string label;
        public string expected;
        public string actual;
        public string explanation;
    }

    [Serializable]
    public sealed class AiJobFitInstructionsDto
    {
        public string response_language;
        public string objective;
        public List<string> rules = new List<string>();
        public AiJobFitResultPackageDto response_template;
    }

    /// <summary>AI 回傳且可由 JobCheck 驗證、預覽及另行保存的結果封包。</summary>
    [Serializable]
    public sealed class AiJobFitResultPackageDto
    {
        public string format;
        public string schema_version;
        public string message_type;
        public string task_type;
        public string request_id;
        public string job_id;
        public string status;
        public AiResultProducerDto producer;
        public AiJobFitAnalysisDto result;
        /// <summary>由 JobCheck 匯入時覆寫；不採信外部 AI 提供的值。</summary>
        public string imported_at;
    }

    [Serializable]
    public sealed class AiResultProducerDto
    {
        public string provider;
        public string model;
        public string generated_at;
    }

    [Serializable]
    public sealed class AiJobFitAnalysisDto
    {
        public string recommendation;
        public string confidence;
        public string summary;
        public List<AiInsightDto> strengths = new List<AiInsightDto>();
        public List<AiInsightDto> gaps = new List<AiInsightDto>();
        public List<AiInsightDto> risks = new List<AiInsightDto>();
        public List<string> missing_information = new List<string>();
        public List<string> interview_questions = new List<string>();
        public List<string> suggested_actions = new List<string>();
    }

    [Serializable]
    public sealed class AiInsightDto
    {
        public string title;
        public string detail;
        public List<string> evidence_refs = new List<string>();
    }

    public sealed class AiJobFitRequestCreationResult
    {
        public AiJobFitRequestCreationResult(
            AiJobFitRequestPackageDto package,
            IEnumerable<string> issues)
        {
            Package = package;
            Issues = issues == null
                ? new List<string>().AsReadOnly()
                : new List<string>(issues).AsReadOnly();
        }

        public AiJobFitRequestPackageDto Package { get; }
        public IReadOnlyList<string> Issues { get; }
        public bool IsSuccess => Package != null && Issues.Count == 0;
    }

    public sealed class AiJobFitResultValidationResult
    {
        public AiJobFitResultValidationResult(
            AiJobFitResultPackageDto value,
            IEnumerable<string> issues)
        {
            Value = value;
            Issues = issues == null
                ? new List<string>().AsReadOnly()
                : new List<string>(issues).AsReadOnly();
        }

        public AiJobFitResultPackageDto Value { get; }
        public IReadOnlyList<string> Issues { get; }
        public bool IsSuccess => Value != null && Issues.Count == 0;
    }
}
