using System;
using System.Collections.Generic;
using System.Linq;

namespace JobCheck.Domain
{
    /// <summary>
    /// 依 ApplicationEvent 歷史重新推算 Application 的目前階段。
    /// Reducer 是純計算工具：不修改 Application、不改寫事件，也不依賴呼叫端提供的事件順序。
    /// </summary>
    public static class ApplicationStateReducer
    {
        /// <summary>
        /// 根據一筆 Application 的完整事件集合推算目前階段與需要人工確認的異常。
        /// </summary>
        /// <param name="input">目標 Application ID 與其事件集合。</param>
        /// <returns>推算出的階段及不重複的異常代碼。</returns>
        public static ApplicationStateReductionResult Reduce(ApplicationStateReducerInput input)
        {
            var issues = new List<ApplicationStateReductionIssue>();
            var issueSet = new HashSet<ApplicationStateReductionIssue>();

            if (input == null)
            {
                AddIssue(ApplicationStateReductionIssue.MissingInput, issues, issueSet);
                return CreateResult(ApplicationStage.Unknown, issues);
            }

            bool isApplicationIdMissing = string.IsNullOrWhiteSpace(input.ApplicationId);
            if (isApplicationIdMissing)
            {
                AddIssue(ApplicationStateReductionIssue.MissingApplicationId, issues, issueSet);
            }

            if (input.Events == null || input.Events.Count == 0)
            {
                AddIssue(ApplicationStateReductionIssue.EmptyEventHistory, issues, issueSet);
                return CreateResult(ApplicationStage.Unknown, issues);
            }

            if (isApplicationIdMissing)
            {
                return CreateResult(ApplicationStage.Unknown, issues);
            }

            List<ApplicationEvent> usableEvents = CollectUsableEvents(input, issues, issueSet);
            RemoveDuplicateIds(usableEvents, issues, issueSet);

            HashSet<string> correctedEventIds = FindCorrectedEventIds(
                usableEvents,
                issues,
                issueSet);

            List<ApplicationEvent> orderedStageEvents = usableEvents
                .Where(applicationEvent =>
                    applicationEvent.EventType != ApplicationEventType.DataCorrected
                    && applicationEvent.EventType != ApplicationEventType.NoResponseMarked
                    && !correctedEventIds.Contains(applicationEvent.Id))
                .OrderBy(applicationEvent => applicationEvent.OccurredAt.Value)
                .ThenBy(applicationEvent => applicationEvent.RecordedAt.Value)
                .ThenBy(applicationEvent => applicationEvent.Id, StringComparer.Ordinal)
                .ToList();

            if (orderedStageEvents.Count == 0)
            {
                AddIssue(ApplicationStateReductionIssue.NoEffectiveStageEvent, issues, issueSet);
                return CreateResult(ApplicationStage.Unknown, issues);
            }

            DetectAmbiguousOrder(orderedStageEvents, issues, issueSet);

            ApplicationStage currentStage = ApplicationStage.Unknown;
            foreach (ApplicationEvent applicationEvent in orderedStageEvents)
            {
                ApplicationStage nextStage = GetStage(applicationEvent);

                if (applicationEvent.EventType == ApplicationEventType.MigrationSnapshot)
                {
                    if (nextStage == ApplicationStage.Unknown)
                    {
                        AddIssue(
                            ApplicationStateReductionIssue.UnknownSnapshotStage,
                            issues,
                            issueSet);
                    }

                    if (currentStage != ApplicationStage.Unknown)
                    {
                        if (currentStage != nextStage)
                        {
                            AddIssue(
                                ApplicationStateReductionIssue.StageRegression,
                                issues,
                                issueSet);
                        }

                        continue;
                    }
                }

                if (IsTerminal(currentStage))
                {
                    if (nextStage != currentStage)
                    {
                        AddIssue(
                            ApplicationStateReductionIssue.EventAfterTerminalState,
                            issues,
                            issueSet);

                        if (IsTerminal(nextStage))
                        {
                            AddIssue(
                                ApplicationStateReductionIssue.ConflictingTerminalEvents,
                                issues,
                                issueSet);
                        }
                    }

                    continue;
                }

                if (CanAdvance(currentStage, nextStage))
                {
                    currentStage = nextStage;
                }
                else
                {
                    AddIssue(ApplicationStateReductionIssue.StageRegression, issues, issueSet);
                }
            }

            return CreateResult(currentStage, issues);
        }

        /// <summary>
        /// 排除 null、跨 Application 與不符合單筆事件規則的資料。
        /// </summary>
        private static List<ApplicationEvent> CollectUsableEvents(
            ApplicationStateReducerInput input,
            ICollection<ApplicationStateReductionIssue> issues,
            ISet<ApplicationStateReductionIssue> issueSet)
        {
            var usableEvents = new List<ApplicationEvent>();

            foreach (ApplicationEvent applicationEvent in input.Events)
            {
                if (applicationEvent == null)
                {
                    AddIssue(ApplicationStateReductionIssue.NullEvent, issues, issueSet);
                    continue;
                }

                if (!string.Equals(
                    applicationEvent.ApplicationId,
                    input.ApplicationId,
                    StringComparison.Ordinal))
                {
                    AddIssue(
                        ApplicationStateReductionIssue.EventBelongsToDifferentApplication,
                        issues,
                        issueSet);
                    continue;
                }

                if (!IsValidForReduction(applicationEvent))
                {
                    AddIssue(ApplicationStateReductionIssue.InvalidEventData, issues, issueSet);
                    continue;
                }

                usableEvents.Add(applicationEvent);
            }

            return usableEvents;
        }

        /// <summary>
        /// 使用既有 Validator 檢查 Reducer 所需的單筆資料完整性。
        /// Application 建立時間不是 Reducer 輸入，因此此處只使用最早時間作為結構檢查基準。
        /// </summary>
        private static bool IsValidForReduction(ApplicationEvent applicationEvent)
        {
            IReadOnlyList<ApplicationEventValidationError> errors =
                applicationEvent.EventType == ApplicationEventType.MigrationSnapshot
                    ? ApplicationEventValidator.ValidateForMigration(
                        applicationEvent,
                        DateTimeOffset.MinValue)
                    : ApplicationEventValidator.ValidateNew(
                        applicationEvent,
                        DateTimeOffset.MinValue);

            return errors.Count == 0;
        }

        /// <summary>
        /// 相同 ID 的事件全部排除，避免依輸入順序任選其中一筆造成不穩定結果。
        /// </summary>
        private static void RemoveDuplicateIds(
            List<ApplicationEvent> usableEvents,
            ICollection<ApplicationStateReductionIssue> issues,
            ISet<ApplicationStateReductionIssue> issueSet)
        {
            HashSet<string> duplicateIds = usableEvents
                .GroupBy(applicationEvent => applicationEvent.Id, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.Ordinal);

            if (duplicateIds.Count == 0)
            {
                return;
            }

            AddIssue(ApplicationStateReductionIssue.DuplicateEventId, issues, issueSet);
            usableEvents.RemoveAll(applicationEvent => duplicateIds.Contains(applicationEvent.Id));
        }

        /// <summary>
        /// 找出被有效 DataCorrected 排除的事件；修正鏈留待後續版本處理。
        /// </summary>
        private static HashSet<string> FindCorrectedEventIds(
            IReadOnlyList<ApplicationEvent> usableEvents,
            ICollection<ApplicationStateReductionIssue> issues,
            ISet<ApplicationStateReductionIssue> issueSet)
        {
            var correctedEventIds = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, ApplicationEvent> eventsById = usableEvents.ToDictionary(
                applicationEvent => applicationEvent.Id,
                StringComparer.Ordinal);

            foreach (ApplicationEvent correction in usableEvents.Where(
                applicationEvent => applicationEvent.EventType == ApplicationEventType.DataCorrected))
            {
                if (!eventsById.TryGetValue(correction.SupersedesEventId, out ApplicationEvent target))
                {
                    AddIssue(
                        ApplicationStateReductionIssue.CorrectionTargetNotFound,
                        issues,
                        issueSet);
                    continue;
                }

                if (target.EventType == ApplicationEventType.DataCorrected)
                {
                    AddIssue(
                        ApplicationStateReductionIssue.CorrectionTargetsCorrection,
                        issues,
                        issueSet);
                    continue;
                }

                correctedEventIds.Add(target.Id);
            }

            return correctedEventIds;
        }

        /// <summary>
        /// 同一精確時間出現不同階段事件時仍以 ID 穩定排序，但同時要求人工確認真實先後。
        /// date 精度事件只要落在同一天就視為無法判定順序。
        /// </summary>
        private static void DetectAmbiguousOrder(
            IReadOnlyList<ApplicationEvent> orderedEvents,
            ICollection<ApplicationStateReductionIssue> issues,
            ISet<ApplicationStateReductionIssue> issueSet)
        {
            for (int firstIndex = 0; firstIndex < orderedEvents.Count; firstIndex++)
            {
                for (int secondIndex = firstIndex + 1;
                    secondIndex < orderedEvents.Count;
                    secondIndex++)
                {
                    ApplicationEvent first = orderedEvents[firstIndex];
                    ApplicationEvent second = orderedEvents[secondIndex];

                    if (!HasAmbiguousOccurrenceTime(first, second))
                    {
                        continue;
                    }

                    if (GetStage(first) != GetStage(second))
                    {
                        AddIssue(
                            ApplicationStateReductionIssue.AmbiguousEventOrder,
                            issues,
                            issueSet);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 比較兩筆事件的實際發生時間是否不足以決定先後。
        /// </summary>
        private static bool HasAmbiguousOccurrenceTime(
            ApplicationEvent first,
            ApplicationEvent second)
        {
            bool containsDateOnlyValue =
                string.Equals(
                    first.TimePrecision,
                    ApplicationEvent.DateTimePrecision,
                    StringComparison.Ordinal)
                || string.Equals(
                    second.TimePrecision,
                    ApplicationEvent.DateTimePrecision,
                    StringComparison.Ordinal);

            if (containsDateOnlyValue)
            {
                return first.OccurredAt.Value.Date == second.OccurredAt.Value.Date;
            }

            return first.OccurredAt.Value.Equals(second.OccurredAt.Value);
        }

        /// <summary>
        /// 將會影響流程的事件種類轉成對應 ApplicationStage。
        /// </summary>
        private static ApplicationStage GetStage(ApplicationEvent applicationEvent)
        {
            switch (applicationEvent.EventType)
            {
                case ApplicationEventType.Saved:
                    return ApplicationStage.Saved;
                case ApplicationEventType.Applied:
                    return ApplicationStage.Applied;
                case ApplicationEventType.Viewed:
                    return ApplicationStage.Viewed;
                case ApplicationEventType.Contacted:
                    return ApplicationStage.Contacted;
                case ApplicationEventType.InterviewScheduled:
                    return ApplicationStage.InterviewScheduled;
                case ApplicationEventType.InterviewCompleted:
                    return ApplicationStage.InterviewCompleted;
                case ApplicationEventType.WaitingResponseStarted:
                    return ApplicationStage.WaitingResponse;
                case ApplicationEventType.RejectedByCompany:
                    return ApplicationStage.RejectedByCompany;
                case ApplicationEventType.ClosedByCandidate:
                    return ApplicationStage.ClosedByCandidate;
                case ApplicationEventType.OfferReceived:
                    return ApplicationStage.OfferReceived;
                case ApplicationEventType.MigrationSnapshot:
                    return applicationEvent.SnapshotStage.Value;
                default:
                    return ApplicationStage.Unknown;
            }
        }

        /// <summary>
        /// 判斷新事件是否可以成為目前階段；面試完成或等待回覆後可以再安排下一輪面試。
        /// </summary>
        private static bool CanAdvance(ApplicationStage currentStage, ApplicationStage nextStage)
        {
            if (currentStage == ApplicationStage.Unknown || currentStage == nextStage)
            {
                return true;
            }

            if (IsTerminal(nextStage))
            {
                return true;
            }

            bool canStartAnotherInterviewRound =
                (currentStage == ApplicationStage.InterviewCompleted
                    || currentStage == ApplicationStage.WaitingResponse)
                && (nextStage == ApplicationStage.Contacted
                    || nextStage == ApplicationStage.InterviewScheduled);

            if (canStartAnotherInterviewRound)
            {
                return true;
            }

            return GetProgressRank(nextStage) >= GetProgressRank(currentStage);
        }

        /// <summary>
        /// 提供一般單向流程的比較順位；合法的多輪面試回圈由 CanAdvance 額外處理。
        /// </summary>
        private static int GetProgressRank(ApplicationStage stage)
        {
            switch (stage)
            {
                case ApplicationStage.Saved:
                    return 0;
                case ApplicationStage.Applied:
                    return 1;
                case ApplicationStage.Viewed:
                    return 2;
                case ApplicationStage.Contacted:
                    return 3;
                case ApplicationStage.InterviewScheduled:
                    return 4;
                case ApplicationStage.InterviewCompleted:
                    return 5;
                case ApplicationStage.WaitingResponse:
                    return 6;
                case ApplicationStage.OfferReceived:
                    return 7;
                case ApplicationStage.RejectedByCompany:
                case ApplicationStage.ClosedByCandidate:
                    return 8;
                default:
                    return -1;
            }
        }

        /// <summary>
        /// 公司拒絕與本人主動停止都是本次 Application 的結案階段。
        /// </summary>
        private static bool IsTerminal(ApplicationStage stage)
        {
            return stage == ApplicationStage.RejectedByCompany
                || stage == ApplicationStage.ClosedByCandidate;
        }

        /// <summary>
        /// 只加入尚未出現的異常代碼，避免多筆同類資料造成重複訊息。
        /// </summary>
        private static void AddIssue(
            ApplicationStateReductionIssue issue,
            ICollection<ApplicationStateReductionIssue> issues,
            ISet<ApplicationStateReductionIssue> issueSet)
        {
            if (issueSet.Add(issue))
            {
                issues.Add(issue);
            }
        }

        /// <summary>
        /// 將內部計算結果轉成不會洩漏可變集合的公開回傳值。
        /// </summary>
        private static ApplicationStateReductionResult CreateResult(
            ApplicationStage currentStage,
            IEnumerable<ApplicationStateReductionIssue> issues)
        {
            return new ApplicationStateReductionResult(currentStage, issues);
        }
    }
}
