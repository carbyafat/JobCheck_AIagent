using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace JobCheck.Domain.Tests
{
    /// <summary>
    /// 驗證 ApplicationStateReducer 能以事件歷史穩定推算目前階段，並保守標記異常資料。
    /// </summary>
    public class ApplicationStateReducerTests
    {
        private const string ApplicationId = "app_0123456789abcdef0123456789abcdef";

        private static readonly DateTimeOffset BaseTime = new DateTimeOffset(
            2026,
            9,
            6,
            9,
            0,
            0,
            TimeSpan.FromHours(8));

        /// <summary>
        /// 確認沒有異常的結果不需要人工確認。
        /// </summary>
        [Test]
        public void Result_WithoutIssues_DoesNotNeedReview()
        {
            var result = new ApplicationStateReductionResult(
                ApplicationStage.Applied,
                null);

            Assert.AreEqual(ApplicationStage.Applied, result.CurrentStage);
            Assert.IsEmpty(result.Issues);
            Assert.IsFalse(result.NeedsReview);
        }

        /// <summary>
        /// 確認結果會複製異常集合，不會受到呼叫端後續修改影響。
        /// </summary>
        [Test]
        public void Result_CopiesIssueCollection()
        {
            var sourceIssues = new List<ApplicationStateReductionIssue>
            {
                ApplicationStateReductionIssue.EmptyEventHistory
            };

            var result = new ApplicationStateReductionResult(
                ApplicationStage.Unknown,
                sourceIssues);
            sourceIssues.Clear();

            CollectionAssert.AreEqual(
                new[] { ApplicationStateReductionIssue.EmptyEventHistory },
                result.Issues);
            Assert.IsTrue(result.NeedsReview);
        }

        /// <summary>
        /// 確認缺少整個輸入物件時安全回傳 Unknown 與 MissingInput。
        /// </summary>
        [Test]
        public void Reduce_NullInput_ReturnsUnknownAndIssue()
        {
            ApplicationStateReductionResult result = ApplicationStateReducer.Reduce(null);

            Assert.AreEqual(ApplicationStage.Unknown, result.CurrentStage);
            CollectionAssert.Contains(result.Issues, ApplicationStateReductionIssue.MissingInput);
        }

        /// <summary>
        /// 確認缺少目標 Application ID 時不會猜測事件歸屬。
        /// </summary>
        [Test]
        public void Reduce_MissingApplicationId_ReturnsUnknownAndIssue()
        {
            var input = new ApplicationStateReducerInput(
                " ",
                new[] { CreateEvent(ApplicationEventType.Applied, 0) });

            ApplicationStateReductionResult result = ApplicationStateReducer.Reduce(input);

            Assert.AreEqual(ApplicationStage.Unknown, result.CurrentStage);
            CollectionAssert.Contains(
                result.Issues,
                ApplicationStateReductionIssue.MissingApplicationId);
        }

        /// <summary>
        /// 確認完全沒有事件時回傳 Unknown 並要求人工確認。
        /// </summary>
        [Test]
        public void Reduce_EmptyHistory_ReturnsUnknownAndIssue()
        {
            var input = new ApplicationStateReducerInput(
                ApplicationId,
                Array.Empty<ApplicationEvent>());

            ApplicationStateReductionResult result = ApplicationStateReducer.Reduce(input);

            Assert.AreEqual(ApplicationStage.Unknown, result.CurrentStage);
            CollectionAssert.Contains(
                result.Issues,
                ApplicationStateReductionIssue.EmptyEventHistory);
        }

        /// <summary>
        /// 確認 null 事件會被排除並留下明確異常，而不是造成例外。
        /// </summary>
        [Test]
        public void Reduce_NullEvent_ReturnsUnknownAndIssues()
        {
            var input = new ApplicationStateReducerInput(
                ApplicationId,
                new ApplicationEvent[] { null });

            ApplicationStateReductionResult result = ApplicationStateReducer.Reduce(input);

            Assert.AreEqual(ApplicationStage.Unknown, result.CurrentStage);
            CollectionAssert.Contains(result.Issues, ApplicationStateReductionIssue.NullEvent);
            CollectionAssert.Contains(
                result.Issues,
                ApplicationStateReductionIssue.NoEffectiveStageEvent);
        }

        /// <summary>
        /// 確認其他 Application 的事件不會污染本次階段推算。
        /// </summary>
        [Test]
        public void Reduce_ForeignApplicationEvent_IsExcluded()
        {
            ApplicationEvent applicationEvent = CreateEvent(ApplicationEventType.Applied, 0);
            applicationEvent.ApplicationId = ApplicationIdGenerator.Create();

            ApplicationStateReductionResult result = Reduce(applicationEvent);

            Assert.AreEqual(ApplicationStage.Unknown, result.CurrentStage);
            CollectionAssert.Contains(
                result.Issues,
                ApplicationStateReductionIssue.EventBelongsToDifferentApplication);
        }

        /// <summary>
        /// 確認未通過 ApplicationEventValidator 的事件不會參與推算。
        /// </summary>
        [Test]
        public void Reduce_InvalidEvent_IsExcluded()
        {
            ApplicationEvent applicationEvent = CreateEvent(ApplicationEventType.Applied, 0);
            applicationEvent.Id = "bad-event-id";

            ApplicationStateReductionResult result = Reduce(applicationEvent);

            Assert.AreEqual(ApplicationStage.Unknown, result.CurrentStage);
            CollectionAssert.Contains(
                result.Issues,
                ApplicationStateReductionIssue.InvalidEventData);
        }

        /// <summary>
        /// 確認相同 ID 的事件全部排除，避免依輸入順序任選一筆。
        /// </summary>
        [Test]
        public void Reduce_DuplicateEventIds_AreExcluded()
        {
            ApplicationEvent first = CreateEvent(ApplicationEventType.Applied, 0);
            ApplicationEvent second = CreateEvent(ApplicationEventType.Viewed, 5);
            second.Id = first.Id;

            ApplicationStateReductionResult result = Reduce(first, second);

            Assert.AreEqual(ApplicationStage.Unknown, result.CurrentStage);
            CollectionAssert.Contains(
                result.Issues,
                ApplicationStateReductionIssue.DuplicateEventId);
        }

        /// <summary>
        /// 確認每種一般流程事件都能轉成預期的目前階段。
        /// </summary>
        [TestCase(ApplicationEventType.Saved, ApplicationStage.Saved)]
        [TestCase(ApplicationEventType.Applied, ApplicationStage.Applied)]
        [TestCase(ApplicationEventType.Viewed, ApplicationStage.Viewed)]
        [TestCase(ApplicationEventType.Contacted, ApplicationStage.Contacted)]
        [TestCase(ApplicationEventType.InterviewScheduled, ApplicationStage.InterviewScheduled)]
        [TestCase(ApplicationEventType.InterviewCompleted, ApplicationStage.InterviewCompleted)]
        [TestCase(ApplicationEventType.WaitingResponseStarted, ApplicationStage.WaitingResponse)]
        [TestCase(ApplicationEventType.OfferReceived, ApplicationStage.OfferReceived)]
        [TestCase(ApplicationEventType.RejectedByCompany, ApplicationStage.RejectedByCompany)]
        [TestCase(ApplicationEventType.ClosedByCandidate, ApplicationStage.ClosedByCandidate)]
        public void Reduce_SingleStageEvent_ReturnsExpectedStage(
            ApplicationEventType eventType,
            ApplicationStage expectedStage)
        {
            ApplicationStateReductionResult result = Reduce(CreateEvent(eventType, 0));

            Assert.AreEqual(expectedStage, result.CurrentStage);
            Assert.IsFalse(result.NeedsReview);
        }

        /// <summary>
        /// 確認 Reducer 會自行排序亂序輸入，而不是把清單最後一筆當成最新狀態。
        /// </summary>
        [Test]
        public void Reduce_UnorderedEvents_SortsByOccurredTime()
        {
            ApplicationEvent completed = CreateEvent(ApplicationEventType.InterviewCompleted, 30);
            ApplicationEvent applied = CreateEvent(ApplicationEventType.Applied, 0);
            ApplicationEvent contacted = CreateEvent(ApplicationEventType.Contacted, 10);

            ApplicationStateReductionResult result = Reduce(completed, applied, contacted);

            Assert.AreEqual(ApplicationStage.InterviewCompleted, result.CurrentStage);
            Assert.IsFalse(result.NeedsReview);
        }

        /// <summary>
        /// 確認 NoResponseMarked 只是異常標記，不會覆蓋原本的流程階段。
        /// </summary>
        [Test]
        public void Reduce_NoResponseMarked_DoesNotChangeStage()
        {
            ApplicationStateReductionResult result = Reduce(
                CreateEvent(ApplicationEventType.Applied, 0),
                CreateEvent(ApplicationEventType.NoResponseMarked, 10));

            Assert.AreEqual(ApplicationStage.Applied, result.CurrentStage);
            Assert.IsFalse(result.NeedsReview);
        }

        /// <summary>
        /// 確認 migration snapshot 可作為舊資料起點，後續正常事件仍能繼續推進。
        /// </summary>
        [Test]
        public void Reduce_MigrationSnapshotThenNewEvent_AdvancesFromSnapshot()
        {
            ApplicationEvent snapshot = CreateMigrationSnapshot(ApplicationStage.Viewed, -1440);
            ApplicationEvent contacted = CreateEvent(ApplicationEventType.Contacted, 0);

            ApplicationStateReductionResult result = Reduce(contacted, snapshot);

            Assert.AreEqual(ApplicationStage.Contacted, result.CurrentStage);
            Assert.IsFalse(result.NeedsReview);
        }

        /// <summary>
        /// 確認無法判定的舊資料 snapshot 保留 Unknown 並要求人工確認。
        /// </summary>
        [Test]
        public void Reduce_UnknownMigrationSnapshot_ReturnsReviewIssue()
        {
            ApplicationStateReductionResult result = Reduce(
                CreateMigrationSnapshot(ApplicationStage.Unknown, 0));

            Assert.AreEqual(ApplicationStage.Unknown, result.CurrentStage);
            CollectionAssert.Contains(
                result.Issues,
                ApplicationStateReductionIssue.UnknownSnapshotStage);
        }

        /// <summary>
        /// 確認 DataCorrected 能排除誤登事件，讓階段由剩餘歷史重新計算。
        /// </summary>
        [Test]
        public void Reduce_DataCorrection_ExcludesTargetEvent()
        {
            ApplicationEvent applied = CreateEvent(ApplicationEventType.Applied, 0);
            ApplicationEvent rejected = CreateEvent(ApplicationEventType.RejectedByCompany, 10);
            ApplicationEvent correction = CreateCorrection(rejected.Id, 20);

            ApplicationStateReductionResult result = Reduce(applied, rejected, correction);

            Assert.AreEqual(ApplicationStage.Applied, result.CurrentStage);
            Assert.IsFalse(result.NeedsReview);
        }

        /// <summary>
        /// 確認找不到 DataCorrected 指定的原事件時不會假裝已完成修正。
        /// </summary>
        [Test]
        public void Reduce_CorrectionTargetMissing_ReturnsIssue()
        {
            ApplicationEvent applied = CreateEvent(ApplicationEventType.Applied, 0);
            ApplicationEvent correction = CreateCorrection(ApplicationEventIdGenerator.Create(), 10);

            ApplicationStateReductionResult result = Reduce(applied, correction);

            Assert.AreEqual(ApplicationStage.Applied, result.CurrentStage);
            CollectionAssert.Contains(
                result.Issues,
                ApplicationStateReductionIssue.CorrectionTargetNotFound);
        }

        /// <summary>
        /// 確認目前版本不會自行解讀 DataCorrected 指向另一筆 DataCorrected 的修正鏈。
        /// </summary>
        [Test]
        public void Reduce_CorrectionTargetsCorrection_ReturnsIssue()
        {
            ApplicationEvent applied = CreateEvent(ApplicationEventType.Applied, 0);
            ApplicationEvent firstCorrection = CreateCorrection(applied.Id, 10);
            ApplicationEvent secondCorrection = CreateCorrection(firstCorrection.Id, 20);

            ApplicationStateReductionResult result = Reduce(
                applied,
                firstCorrection,
                secondCorrection);

            CollectionAssert.Contains(
                result.Issues,
                ApplicationStateReductionIssue.CorrectionTargetsCorrection);
        }

        /// <summary>
        /// 確認不同階段事件同時發生時仍有穩定結果，但會要求人工確認真實順序。
        /// </summary>
        [Test]
        public void Reduce_DifferentStagesAtSameTime_ReturnsAmbiguousOrderIssue()
        {
            ApplicationEvent applied = CreateEvent(ApplicationEventType.Applied, 0);
            ApplicationEvent viewed = CreateEvent(ApplicationEventType.Viewed, 0);
            viewed.RecordedAt = viewed.RecordedAt.Value.AddMinutes(1);

            ApplicationStateReductionResult result = Reduce(viewed, applied);

            Assert.AreEqual(ApplicationStage.Viewed, result.CurrentStage);
            CollectionAssert.Contains(
                result.Issues,
                ApplicationStateReductionIssue.AmbiguousEventOrder);
        }

        /// <summary>
        /// 確認只有日期精度的同日事件無法假裝具有可靠先後順序。
        /// </summary>
        [Test]
        public void Reduce_DateOnlyEventsOnSameDay_ReturnsAmbiguousOrderIssue()
        {
            ApplicationEvent applied = CreateEvent(ApplicationEventType.Applied, 0);
            ApplicationEvent viewed = CreateEvent(ApplicationEventType.Viewed, 360);
            applied.TimePrecision = ApplicationEvent.DateTimePrecision;
            viewed.TimePrecision = ApplicationEvent.DateTimePrecision;

            ApplicationStateReductionResult result = Reduce(applied, viewed);

            Assert.AreEqual(ApplicationStage.Viewed, result.CurrentStage);
            CollectionAssert.Contains(
                result.Issues,
                ApplicationStateReductionIssue.AmbiguousEventOrder);
        }

        /// <summary>
        /// 確認較晚事件回到較早階段時保留最後可信階段並標記倒退。
        /// </summary>
        [Test]
        public void Reduce_LaterLowerStage_KeepsStageAndReturnsRegressionIssue()
        {
            ApplicationEvent applied = CreateEvent(ApplicationEventType.Applied, 0);
            ApplicationEvent contacted = CreateEvent(ApplicationEventType.Contacted, 10);
            ApplicationEvent viewed = CreateEvent(ApplicationEventType.Viewed, 20);

            ApplicationStateReductionResult result = Reduce(applied, contacted, viewed);

            Assert.AreEqual(ApplicationStage.Contacted, result.CurrentStage);
            CollectionAssert.Contains(
                result.Issues,
                ApplicationStateReductionIssue.StageRegression);
        }

        /// <summary>
        /// 確認完成面試或等待回覆後可以再安排下一輪面試，不會被誤判成階段倒退。
        /// </summary>
        [Test]
        public void Reduce_AnotherInterviewRound_AdvancesNormally()
        {
            ApplicationStateReductionResult result = Reduce(
                CreateEvent(ApplicationEventType.Applied, 0),
                CreateEvent(ApplicationEventType.InterviewCompleted, 10),
                CreateEvent(ApplicationEventType.WaitingResponseStarted, 20),
                CreateEvent(ApplicationEventType.InterviewScheduled, 30),
                CreateEvent(ApplicationEventType.InterviewCompleted, 40));

            Assert.AreEqual(ApplicationStage.InterviewCompleted, result.CurrentStage);
            Assert.IsFalse(result.NeedsReview);
        }

        /// <summary>
        /// 確認結案後的一般進度事件不會自行重新開啟同一筆 Application。
        /// </summary>
        [Test]
        public void Reduce_EventAfterTerminalState_KeepsTerminalStageAndReturnsIssue()
        {
            ApplicationStateReductionResult result = Reduce(
                CreateEvent(ApplicationEventType.Applied, 0),
                CreateEvent(ApplicationEventType.RejectedByCompany, 10),
                CreateEvent(ApplicationEventType.Contacted, 20));

            Assert.AreEqual(ApplicationStage.RejectedByCompany, result.CurrentStage);
            CollectionAssert.Contains(
                result.Issues,
                ApplicationStateReductionIssue.EventAfterTerminalState);
        }

        /// <summary>
        /// 確認公司拒絕與本人主動結案同時存在時，不會靜默挑選後者覆蓋前者。
        /// </summary>
        [Test]
        public void Reduce_ConflictingTerminalEvents_KeepsFirstAndReturnsIssues()
        {
            ApplicationStateReductionResult result = Reduce(
                CreateEvent(ApplicationEventType.Applied, 0),
                CreateEvent(ApplicationEventType.RejectedByCompany, 10),
                CreateEvent(ApplicationEventType.ClosedByCandidate, 20));

            Assert.AreEqual(ApplicationStage.RejectedByCompany, result.CurrentStage);
            CollectionAssert.Contains(
                result.Issues,
                ApplicationStateReductionIssue.ConflictingTerminalEvents);
            CollectionAssert.Contains(
                result.Issues,
                ApplicationStateReductionIssue.EventAfterTerminalState);
        }

        /// <summary>
        /// 確認收到 Offer 後由本人決定停止是合法流程，Offer 本身不是不可變的結案狀態。
        /// </summary>
        [Test]
        public void Reduce_OfferThenCandidateClosure_ReturnsCandidateClosure()
        {
            ApplicationStateReductionResult result = Reduce(
                CreateEvent(ApplicationEventType.Applied, 0),
                CreateEvent(ApplicationEventType.OfferReceived, 10),
                CreateEvent(ApplicationEventType.ClosedByCandidate, 20));

            Assert.AreEqual(ApplicationStage.ClosedByCandidate, result.CurrentStage);
            Assert.IsFalse(result.NeedsReview);
        }

        /// <summary>
        /// 確認 Reducer 自行建立排序結果，不會重新排列呼叫端的事件清單。
        /// </summary>
        [Test]
        public void Reduce_UnorderedInput_DoesNotMutateCallerList()
        {
            ApplicationEvent later = CreateEvent(ApplicationEventType.Contacted, 10);
            ApplicationEvent earlier = CreateEvent(ApplicationEventType.Applied, 0);
            var events = new List<ApplicationEvent> { later, earlier };

            ApplicationStateReducer.Reduce(
                new ApplicationStateReducerInput(ApplicationId, events));

            Assert.AreSame(later, events[0]);
            Assert.AreSame(earlier, events[1]);
        }

        /// <summary>
        /// 使用指定事件集合建立輸入並執行 Reducer。
        /// </summary>
        private static ApplicationStateReductionResult Reduce(params ApplicationEvent[] events)
        {
            return ApplicationStateReducer.Reduce(
                new ApplicationStateReducerInput(ApplicationId, events));
        }

        /// <summary>
        /// 建立一筆具有可靠時間與必要欄位的普通事件。
        /// </summary>
        private static ApplicationEvent CreateEvent(
            ApplicationEventType eventType,
            int occurredAfterMinutes)
        {
            DateTimeOffset occurredAt = BaseTime.AddMinutes(occurredAfterMinutes);

            return new ApplicationEvent
            {
                Id = ApplicationEventIdGenerator.Create(),
                ApplicationId = ApplicationId,
                EventType = eventType,
                OccurredAt = occurredAt,
                RecordedAt = occurredAt,
                Actor = EventActor.Candidate
            };
        }

        /// <summary>
        /// 建立一筆 migration 專用的狀態快照。
        /// </summary>
        private static ApplicationEvent CreateMigrationSnapshot(
            ApplicationStage snapshotStage,
            int occurredAfterMinutes)
        {
            ApplicationEvent snapshot = CreateEvent(
                ApplicationEventType.MigrationSnapshot,
                occurredAfterMinutes);
            snapshot.Actor = EventActor.System;
            snapshot.SnapshotStage = snapshotStage;
            snapshot.TimePrecision = ApplicationEvent.DateTimePrecision;
            return snapshot;
        }

        /// <summary>
        /// 建立一筆指向指定原事件的資料修正事件。
        /// </summary>
        private static ApplicationEvent CreateCorrection(
            string supersedesEventId,
            int occurredAfterMinutes)
        {
            ApplicationEvent correction = CreateEvent(
                ApplicationEventType.DataCorrected,
                occurredAfterMinutes);
            correction.Actor = EventActor.System;
            correction.SupersedesEventId = supersedesEventId;
            return correction;
        }
    }
}
