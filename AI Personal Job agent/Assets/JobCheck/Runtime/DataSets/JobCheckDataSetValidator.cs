using System;
using System.Collections.Generic;
using System.Linq;

namespace JobCheck.Domain
{
    /// <summary>
    /// 驗證 Company、JobPosting、Application 與 ApplicationEvent 之間的跨集合關聯。
    /// 單筆必要欄位仍由各模型自己的 Validator 負責；本類別不修改任何傳入資料。
    /// </summary>
    public static class JobCheckDataSetValidator
    {
        /// <summary>
        /// 驗證重複 ID、核心外鍵、PreviousApplicationId 投遞鏈與事件推算階段。
        /// </summary>
        public static JobCheckDataSetValidationResult Validate(JobCheckDataSet dataSet)
        {
            var issues = new List<JobCheckDataSetValidationIssue>();
            if (dataSet == null)
            {
                return new JobCheckDataSetValidationResult(issues);
            }

            AddDuplicateIdIssues(dataSet.Companies, item => item?.Id,
                JobCheckDataSetValidationError.DuplicateCompanyId,
                JobCheckDataSetEntityType.Company, issues);
            AddDuplicateIdIssues(dataSet.JobPostings, item => item?.Id,
                JobCheckDataSetValidationError.DuplicateJobPostingId,
                JobCheckDataSetEntityType.JobPosting, issues);
            AddDuplicateIdIssues(dataSet.Applications, item => item?.Id,
                JobCheckDataSetValidationError.DuplicateApplicationId,
                JobCheckDataSetEntityType.Application, issues);
            AddDuplicateIdIssues(dataSet.ApplicationEvents, item => item?.Id,
                JobCheckDataSetValidationError.DuplicateApplicationEventId,
                JobCheckDataSetEntityType.ApplicationEvent, issues);

            HashSet<string> companyIds = CreateIdSet(dataSet.Companies, item => item?.Id);
            HashSet<string> jobPostingIds = CreateIdSet(dataSet.JobPostings, item => item?.Id);
            HashSet<string> applicationIds = CreateIdSet(dataSet.Applications, item => item?.Id);

            ValidateJobPostingCompanies(dataSet.JobPostings, companyIds, issues);
            ValidateApplicationJobPostings(dataSet.Applications, jobPostingIds, issues);
            ValidateApplicationEventOwners(dataSet.ApplicationEvents, applicationIds, issues);
            ValidatePreviousApplications(dataSet.Applications, issues);
            ValidateApplicationStates(
                dataSet.Applications,
                dataSet.ApplicationEvents,
                issues);

            return new JobCheckDataSetValidationResult(issues);
        }

        /// <summary>
        /// 每一種資料的 ID 必須在自己的集合內唯一；空白 ID 留給單筆 Validator 回報。
        /// </summary>
        private static void AddDuplicateIdIssues<T>(
            IEnumerable<T> items,
            Func<T, string> getId,
            JobCheckDataSetValidationError error,
            JobCheckDataSetEntityType entityType,
            ICollection<JobCheckDataSetValidationIssue> issues)
        {
            foreach (string duplicateId in items
                .Where(item => item != null)
                .Select(getId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .GroupBy(id => id, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key))
            {
                issues.Add(new JobCheckDataSetValidationIssue(
                    error, entityType, duplicateId, "Id", null));
            }
        }

        /// <summary>
        /// 建立只包含非空白 ID 的精確比對集合。
        /// </summary>
        private static HashSet<string> CreateIdSet<T>(
            IEnumerable<T> items,
            Func<T, string> getId)
        {
            return new HashSet<string>(
                items.Where(item => item != null)
                    .Select(getId)
                    .Where(id => !string.IsNullOrWhiteSpace(id)),
                StringComparer.Ordinal);
        }

        /// <summary>
        /// 驗證每個非空白 CompanyId 都能連到一筆公司資料。
        /// </summary>
        private static void ValidateJobPostingCompanies(
            IEnumerable<JobPosting> jobPostings,
            ISet<string> companyIds,
            ICollection<JobCheckDataSetValidationIssue> issues)
        {
            foreach (JobPosting jobPosting in jobPostings.Where(item => item != null))
            {
                if (!string.IsNullOrWhiteSpace(jobPosting.CompanyId)
                    && !companyIds.Contains(jobPosting.CompanyId))
                {
                    issues.Add(new JobCheckDataSetValidationIssue(
                        JobCheckDataSetValidationError.JobPostingCompanyNotFound,
                        JobCheckDataSetEntityType.JobPosting,
                        jobPosting.Id,
                        nameof(JobPosting.CompanyId),
                        jobPosting.CompanyId));
                }
            }
        }

        /// <summary>
        /// 驗證每個非空白 JobPostingId 都能連到一筆職缺資料。
        /// </summary>
        private static void ValidateApplicationJobPostings(
            IEnumerable<Application> applications,
            ISet<string> jobPostingIds,
            ICollection<JobCheckDataSetValidationIssue> issues)
        {
            foreach (Application application in applications.Where(item => item != null))
            {
                if (!string.IsNullOrWhiteSpace(application.JobPostingId)
                    && !jobPostingIds.Contains(application.JobPostingId))
                {
                    issues.Add(new JobCheckDataSetValidationIssue(
                        JobCheckDataSetValidationError.ApplicationJobPostingNotFound,
                        JobCheckDataSetEntityType.Application,
                        application.Id,
                        nameof(Application.JobPostingId),
                        application.JobPostingId));
                }
            }
        }

        /// <summary>
        /// 驗證每個非空白 ApplicationId 都能連到一筆應徵資料。
        /// </summary>
        private static void ValidateApplicationEventOwners(
            IEnumerable<ApplicationEvent> events,
            ISet<string> applicationIds,
            ICollection<JobCheckDataSetValidationIssue> issues)
        {
            foreach (ApplicationEvent applicationEvent in events.Where(item => item != null))
            {
                if (!string.IsNullOrWhiteSpace(applicationEvent.ApplicationId)
                    && !applicationIds.Contains(applicationEvent.ApplicationId))
                {
                    issues.Add(new JobCheckDataSetValidationIssue(
                        JobCheckDataSetValidationError.ApplicationEventApplicationNotFound,
                        JobCheckDataSetEntityType.ApplicationEvent,
                        applicationEvent.Id,
                        nameof(ApplicationEvent.ApplicationId),
                        applicationEvent.ApplicationId));
                }
            }
        }

        /// <summary>
        /// 驗證前次投遞存在、屬於相同職缺、未指向自己且整條鏈沒有循環。
        /// ID 重複時不建立模稜兩可的索引，該問題已由 DuplicateApplicationId 回報。
        /// </summary>
        private static void ValidatePreviousApplications(
            IReadOnlyList<Application> applications,
            ICollection<JobCheckDataSetValidationIssue> issues)
        {
            Dictionary<string, Application> uniqueApplications = applications
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Id))
                .GroupBy(item => item.Id, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

            foreach (Application application in applications.Where(item => item != null))
            {
                string previousId = application.PreviousApplicationId;
                if (string.IsNullOrWhiteSpace(previousId))
                {
                    continue;
                }

                if (string.Equals(application.Id, previousId, StringComparison.Ordinal))
                {
                    AddPreviousIssue(
                        JobCheckDataSetValidationError.PreviousApplicationReferencesSelf,
                        application, previousId, issues);
                    continue;
                }

                if (!uniqueApplications.TryGetValue(previousId, out Application previous))
                {
                    AddPreviousIssue(
                        JobCheckDataSetValidationError.PreviousApplicationNotFound,
                        application, previousId, issues);
                    continue;
                }

                if (!string.Equals(
                    application.JobPostingId,
                    previous.JobPostingId,
                    StringComparison.Ordinal))
                {
                    AddPreviousIssue(
                        JobCheckDataSetValidationError.PreviousApplicationBelongsToDifferentJobPosting,
                        application, previousId, issues);
                }

                if (HasPreviousApplicationCycle(application, uniqueApplications))
                {
                    AddPreviousIssue(
                        JobCheckDataSetValidationError.PreviousApplicationCycle,
                        application, previousId, issues);
                }
            }
        }

        /// <summary>
        /// 從指定應徵沿 PreviousApplicationId 往前走；再次遇到相同 ID 即代表形成循環。
        /// </summary>
        private static bool HasPreviousApplicationCycle(
            Application start,
            IReadOnlyDictionary<string, Application> applicationsById)
        {
            var visitedIds = new HashSet<string>(StringComparer.Ordinal);
            Application current = start;

            while (current != null && !string.IsNullOrWhiteSpace(current.Id))
            {
                if (!visitedIds.Add(current.Id))
                {
                    return true;
                }

                if (string.IsNullOrWhiteSpace(current.PreviousApplicationId)
                    || !applicationsById.TryGetValue(
                        current.PreviousApplicationId,
                        out current))
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// 以一致欄位資訊加入一筆 PreviousApplicationId 問題。
        /// </summary>
        private static void AddPreviousIssue(
            JobCheckDataSetValidationError error,
            Application application,
            string relatedId,
            ICollection<JobCheckDataSetValidationIssue> issues)
        {
            issues.Add(new JobCheckDataSetValidationIssue(
                error,
                JobCheckDataSetEntityType.Application,
                application.Id,
                nameof(Application.PreviousApplicationId),
                relatedId));
        }

        /// <summary>
        /// 以每筆 Application 自己的事件執行 Reducer，保留推算異常並核對 CurrentStage 快取。
        /// Application ID 重複時不進行推算，避免任選其中一筆造成誤導性結果。
        /// </summary>
        private static void ValidateApplicationStates(
            IReadOnlyList<Application> applications,
            IReadOnlyList<ApplicationEvent> applicationEvents,
            ICollection<JobCheckDataSetValidationIssue> issues)
        {
            IEnumerable<Application> uniqueApplications = applications
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Id))
                .GroupBy(item => item.Id, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .Select(group => group.Single());

            foreach (Application application in uniqueApplications)
            {
                List<ApplicationEvent> ownedEvents = applicationEvents
                    .Where(applicationEvent =>
                        applicationEvent != null
                        && string.Equals(
                            applicationEvent.ApplicationId,
                            application.Id,
                            StringComparison.Ordinal))
                    .ToList();

                ApplicationStateReductionResult reduction = ApplicationStateReducer.Reduce(
                    new ApplicationStateReducerInput(application.Id, ownedEvents));

                foreach (ApplicationStateReductionIssue reductionIssue in reduction.Issues)
                {
                    issues.Add(new JobCheckDataSetValidationIssue(
                        JobCheckDataSetValidationError.ApplicationEventHistoryNeedsReview,
                        JobCheckDataSetEntityType.Application,
                        application.Id,
                        "ApplicationEvents",
                        null,
                        reductionIssue));
                }

                if (reduction.CurrentStage != ApplicationStage.Unknown
                    && reduction.CurrentStage != application.CurrentStage)
                {
                    issues.Add(new JobCheckDataSetValidationIssue(
                        JobCheckDataSetValidationError.ApplicationCurrentStageMismatch,
                        JobCheckDataSetEntityType.Application,
                        application.Id,
                        nameof(Application.CurrentStage),
                        null,
                        null,
                        reduction.CurrentStage));
                }
            }
        }
    }
}
