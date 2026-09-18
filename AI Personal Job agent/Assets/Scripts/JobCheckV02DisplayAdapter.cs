using System.Collections.Generic;
using System.Linq;
using Domain = JobCheck.Domain;
using JobCheck.Persistence;

/// <summary>
/// V0.2 Domain 與現有畫面資料形狀之間的相容轉接器，目前仍由列表與詳情頁使用。
/// 它只複製顯示欄位，不讀檔、不寫檔，也不改動 Domain 物件。
/// 待清理：等 UI 全面改用 V0.2 ViewModel、且 Prefab 綁定驗證通過後，
/// 才能移除此相容層；目前不是死碼。
/// </summary>
public static class JobCheckV02DisplayAdapter
{
    public static JobSummaryData CreateSummary(JobPostingReadOnlyItem item)
    {
        JobDetailData detail = CreateDetail(item);
        JobTrackingData tracking = CreateTracking(item);
        return new JobSummaryData
        {
            id = item.JobPosting.Id,
            company = item.Company != null ? item.Company.Name : string.Empty,
            title = item.JobPosting.Title,
            salary = FormatCompensation(item.JobPosting.Compensation),
            salary_min = item.JobPosting.Compensation?.Minimum ?? 0,
            parse_status = "ok",
            status = tracking.status,
            last_action_at = tracking.last_action_at,
            manual_expire_at = tracking.manual_expire_at,
            favorite = tracking.favorite,
            fit_score = -1,
            v02Detail = detail,
            v02Tracking = tracking
        };
    }

    /// <summary>
    /// 把 V0.2 Domain 資料轉成現有詳細頁可顯示的形狀。
    /// </summary>
    private static JobDetailData CreateDetail(JobPostingReadOnlyItem item)
    {
        Domain.JobPosting job = item.JobPosting;
        Domain.Company company = item.Company;

        return new JobDetailData
        {
            schema_version = job.SchemaVersion,
            id = job.Id,
            parse_status = "ok",
            source = job.Source == null
                ? null
                : new SourceJsonData
                {
                    platform = job.Source.Platform,
                    url = job.Source.Url,
                    captured_at = job.CapturedAt?.ToString("o"),
                    raw_images = new List<string>()
                },
            company = company == null
                ? null
                : new CompanyJsonData
                {
                    name = company.Name,
                    industry = company.Industry,
                    raw_text = company.Notes
                },
            job = new JobJsonData
            {
                title = job.Title,
                department = job.Department,
                category = job.Category,
                raw_text = job.RawDescription
            },
            compensation = CreateCompensation(job.Compensation),
            location = CreateLocation(job.Location),
            work_conditions = CreateWorkConditions(job.WorkConditions),
            responsibilities = CopyStrings(job.Responsibilities),
            requirements = CreateRequirements(job.Requirements),
            benefits = CreateBenefits(job.Benefits),
            recruitment_process = CopyStrings(job.RecruitmentProcess),
            tags = CopyStrings(job.Tags),
            risk_flags = CopyStrings(job.RiskFlags)
        };
    }

    private static CompensationJsonData CreateCompensation(Domain.JobCompensation value)
    {
        if (value == null)
        {
            return null;
        }

        return new CompensationJsonData
        {
            type = value.Type,
            period = value.Period,
            has_min = value.Minimum.HasValue,
            has_max = value.Maximum.HasValue,
            min = value.Minimum ?? 0,
            max = value.Maximum ?? 0,
            currency = value.Currency,
            raw_text = FormatCompensation(value),
            notes = value.Notes
        };
    }

    /// <summary>
    /// 優先保留來源的薪資原文；沒有原文時，使用結構化欄位產生可讀文字。
    /// </summary>
    public static string FormatCompensation(CompensationJsonData value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(value.raw_text))
        {
            return value.raw_text.Trim();
        }

        return FormatCompensationParts(
            value.type,
            value.period,
            value.has_min ? (int?)value.min : null,
            value.has_max ? (int?)value.max : null,
            value.currency);
    }

    private static string FormatCompensation(Domain.JobCompensation value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(value.RawText))
        {
            return value.RawText.Trim();
        }

        return FormatCompensationParts(
            value.Type,
            value.Period,
            value.Minimum,
            value.Maximum,
            value.Currency);
    }

    private static string FormatCompensationParts(
        string type,
        string period,
        int? minimum,
        int? maximum,
        string currency)
    {
        if (!minimum.HasValue && !maximum.HasValue)
        {
            return string.Equals(type, "negotiable", System.StringComparison.OrdinalIgnoreCase)
                ? "面議"
                : string.Empty;
        }

        string amount;
        if (minimum.HasValue && maximum.HasValue)
        {
            amount = minimum.Value.ToString("N0") + "–" + maximum.Value.ToString("N0");
        }
        else if (minimum.HasValue)
        {
            amount = minimum.Value.ToString("N0") + " 以上";
        }
        else
        {
            amount = maximum.Value.ToString("N0") + " 以下";
        }

        return (string.IsNullOrWhiteSpace(currency) ? string.Empty : currency.Trim() + " ")
            + amount
            + FormatCompensationPeriod(period);
    }

    private static string FormatCompensationPeriod(string period)
    {
        switch ((period ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "monthly": return "／月";
            case "yearly": return "／年";
            case "hourly": return "／時";
            case "daily": return "／日";
            default: return string.IsNullOrWhiteSpace(period) ? string.Empty : "／" + period.Trim();
        }
    }

    private static LocationJsonData CreateLocation(Domain.JobLocation value)
    {
        if (value == null)
        {
            return null;
        }

        return new LocationJsonData
        {
            work_mode = value.WorkMode,
            city = value.City,
            district = value.District,
            address = value.Address,
            remote_allowed = value.RemoteAllowed ?? false,
            raw_text = value.RawText
        };
    }

    private static WorkConditionsJsonData CreateWorkConditions(Domain.JobWorkConditions value)
    {
        if (value == null)
        {
            return null;
        }

        return new WorkConditionsJsonData
        {
            employment_type = value.EmploymentType,
            working_hours = value.WorkingHours,
            business_trip = value.BusinessTrip,
            management_responsibility = value.ManagementResponsibility,
            leave_policy = value.LeavePolicy,
            start_date = value.StartDate
        };
    }

    private static RequirementsJsonData CreateRequirements(Domain.JobRequirements value)
    {
        if (value == null)
        {
            return null;
        }

        var languages = new List<LanguageJsonData>();
        if (value.Languages != null)
        {
            foreach (Domain.JobLanguageRequirement language in value.Languages)
            {
                if (language == null)
                {
                    continue;
                }

                languages.Add(new LanguageJsonData
                {
                    name = language.Name,
                    listening = language.Listening,
                    speaking = language.Speaking,
                    reading = language.Reading,
                    writing = language.Writing,
                    raw_text = language.RawText
                });
            }
        }

        return new RequirementsJsonData
        {
            experience = value.Experience,
            education = value.Education,
            major = value.Major,
            languages = languages,
            tools = CopyStrings(value.Tools),
            skills = CopyStrings(value.Skills),
            other_conditions = CopyStrings(value.OtherConditions)
        };
    }

    private static BenefitsJsonData CreateBenefits(Domain.JobBenefits value)
    {
        if (value == null)
        {
            return null;
        }

        return new BenefitsJsonData
        {
            salary_bonus = CopyStrings(value.SalaryBonus),
            insurance_health = CopyStrings(value.InsuranceHealth),
            flexibility = CopyStrings(value.Flexibility),
            training = CopyStrings(value.Training),
            life = CopyStrings(value.Life),
            other = CopyStrings(value.Other)
        };
    }

    /// <summary>
    /// 將 V0.2 Application 投影成既有狀態顯示資料；此物件不允許寫回 V0.2。
    /// </summary>
    private static JobTrackingData CreateTracking(JobPostingReadOnlyItem item)
    {
        Domain.Application application = item.CurrentApplication;
        return new JobTrackingData
        {
            job_id = item.JobPosting.Id,
            application_id = application?.Id,
            status = item.StatusCode,
            last_action_at = item.LastActivityAt?.ToString("o") ?? string.Empty,
            manual_expire_at = application?.ManualFollowUpAt?.ToString("o") ?? string.Empty,
            favorite = application != null && application.IsFavorite,
            fit_score = -1,
            notes = application?.Notes,
            is_archived = application != null && application.IsArchived,
            event_history = BuildEventHistory(item.ApplicationEvents, application)
        };
    }

    private static List<string> BuildEventHistory(
        IEnumerable<Domain.ApplicationEvent> applicationEvents,
        Domain.Application application)
    {
        var result = new List<string>();
        if (applicationEvents == null)
        {
            return result;
        }

        foreach (Domain.ApplicationEvent item in applicationEvents
            .OrderByDescending(value => value.OccurredAt ?? value.RecordedAt)
            .ThenByDescending(value => value.Id, System.StringComparer.Ordinal))
        {
            string time = item.OccurredAt?.ToString("yyyy/MM/dd HH:mm") ?? "時間未知";
            string scheduled = item.ScheduledFor.HasValue
                ? "\n    面試時間：" + item.ScheduledFor.Value.ToString("yyyy/MM/dd HH:mm")
                : string.Empty;
            string notes = string.IsNullOrWhiteSpace(item.Notes)
                ? string.Empty
                : "\n    備註：" + item.Notes.Trim();
            string closeReason = item.EventType == Domain.ApplicationEventType.ClosedByCandidate
                ? FormatCloseReason(application)
                : string.Empty;
            result.Add(
                time + "｜" + FormatEventType(item.EventType)
                + "｜" + FormatActor(item.Actor)
                + scheduled
                + closeReason
                + notes);
        }

        return result;
    }

    private static string FormatActor(Domain.EventActor actor)
    {
        switch (actor)
        {
            case Domain.EventActor.Candidate: return "本人";
            case Domain.EventActor.Company: return "公司";
            case Domain.EventActor.Platform: return "平台";
            case Domain.EventActor.System: return "系統";
            default: return actor.ToString();
        }
    }

    private static string FormatCloseReason(Domain.Application application)
    {
        if (application == null || !application.CandidateCloseReason.HasValue)
        {
            return string.Empty;
        }

        string reason;
        switch (application.CandidateCloseReason.Value)
        {
            case Domain.CandidateCloseReason.SalaryTooLow: reason = "薪資太低"; break;
            case Domain.CandidateCloseReason.GamblingIndustry: reason = "博弈產業"; break;
            case Domain.CandidateCloseReason.Commute: reason = "通勤"; break;
            case Domain.CandidateCloseReason.WorkSchedule: reason = "工時"; break;
            case Domain.CandidateCloseReason.WeekendDuty: reason = "週末值班"; break;
            case Domain.CandidateCloseReason.RoleMismatch: reason = "職務不符"; break;
            case Domain.CandidateCloseReason.TechMismatch: reason = "技術不符"; break;
            case Domain.CandidateCloseReason.CompanyConcern: reason = "公司疑慮"; break;
            case Domain.CandidateCloseReason.BetterOpportunity: reason = "選擇其他機會"; break;
            case Domain.CandidateCloseReason.NoResponse: reason = "長期無回覆"; break;
            default: reason = "其他"; break;
        }

        string note = string.IsNullOrWhiteSpace(application.CandidateCloseReasonNote)
            ? string.Empty
            : "（" + application.CandidateCloseReasonNote.Trim() + "）";
        return "\n    結案原因：" + reason + note;
    }

    private static string FormatEventType(Domain.ApplicationEventType eventType)
    {
        switch (eventType)
        {
            case Domain.ApplicationEventType.Saved: return "有興趣";
            case Domain.ApplicationEventType.Applied: return "已投遞";
            case Domain.ApplicationEventType.Viewed: return "公司已讀";
            case Domain.ApplicationEventType.Contacted: return "公司已聯絡";
            case Domain.ApplicationEventType.InterviewScheduled: return "已安排面試";
            case Domain.ApplicationEventType.InterviewCompleted: return "已完成面試";
            case Domain.ApplicationEventType.WaitingResponseStarted: return "開始等待回覆";
            case Domain.ApplicationEventType.RejectedByCompany: return "公司拒絕";
            case Domain.ApplicationEventType.ClosedByCandidate: return "本人放棄";
            case Domain.ApplicationEventType.OfferReceived: return "收到 Offer";
            case Domain.ApplicationEventType.NoResponseMarked: return "標記無回覆";
            case Domain.ApplicationEventType.MigrationSnapshot: return "V0.1 搬移快照";
            case Domain.ApplicationEventType.DataCorrected: return "資料修正";
            default: return eventType.ToString();
        }
    }

    private static List<string> CopyStrings(IEnumerable<string> values)
    {
        return values == null ? new List<string>() : new List<string>(values);
    }
}
