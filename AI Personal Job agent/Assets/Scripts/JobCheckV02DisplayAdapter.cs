using System.Collections.Generic;
using System.IO;
using Domain = JobCheck.Domain;
using JobCheck.Persistence;

/// <summary>
/// V0.2 Domain 與現有畫面資料形狀之間的暫時轉接器。
/// 它只複製顯示欄位，不讀檔、不寫檔，也不改動 Domain 物件。
/// 等 UI 全面改用 V0.2 ViewModel 後即可移除這個相容層。
/// </summary>
public static class JobCheckV02DisplayAdapter
{
    public static JobSummaryData CreateSummary(
        JobPostingReadOnlyItem item,
        string dataRoot)
    {
        JobDetailData detail = CreateDetail(item);
        JobTrackingData tracking = CreateTracking(item);
        return new JobSummaryData
        {
            id = item.JobPosting.Id,
            file = Path.Combine(
                JobCheckDataRepository.JobsDirectoryName,
                item.JobPosting.Id + ".json"),
            detailFullPath = Path.Combine(
                dataRoot,
                JobCheckDataRepository.JobsDirectoryName,
                item.JobPosting.Id + ".json"),
            company = item.Company != null ? item.Company.Name : string.Empty,
            title = item.JobPosting.Title,
            salary = item.JobPosting.Compensation != null
                ? item.JobPosting.Compensation.RawText
                : string.Empty,
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
            recruitment_process = CopyStrings(job.RecruitmentProcess)
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
            min = value.Minimum ?? 0,
            max = value.Maximum ?? 0,
            currency = value.Currency,
            raw_text = value.RawText,
            notes = value.Notes
        };
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
            status = item.StatusCode,
            last_action_at = item.LastActivityAt?.ToString("o") ?? string.Empty,
            manual_expire_at = application?.ManualFollowUpAt?.ToString("o") ?? string.Empty,
            favorite = application != null && application.IsFavorite,
            fit_score = -1
        };
    }

    private static List<string> CopyStrings(IEnumerable<string> values)
    {
        return values == null ? new List<string>() : new List<string>(values);
    }
}
