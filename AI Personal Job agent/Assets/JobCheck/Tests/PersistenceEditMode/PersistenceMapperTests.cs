using System;
using System.Collections.Generic;
using System.Linq;
using JobCheck.Domain;
using NUnit.Framework;

namespace JobCheck.Persistence.Tests
{
    /// <summary>
    /// 驗證四個核心模型的 DTO／Domain 雙向轉換、錯誤定位與集合隔離。
    /// </summary>
    public class PersistenceMapperTests
    {
        private static readonly DateTimeOffset BaseTime = new DateTimeOffset(
            2026,
            9,
            6,
            14,
            30,
            0,
            TimeSpan.FromHours(8));

        /// <summary>
        /// 確認有任何 Issue 時結果不會暴露看似可用的半套 Value。
        /// </summary>
        [Test]
        public void ConversionResult_WithIssue_DiscardsValue()
        {
            var issues = new List<PersistenceConversionIssue>
            {
                new PersistenceConversionIssue(
                    PersistenceConversionError.InvalidEnum,
                    "current_stage",
                    "banana")
            };

            var result = new PersistenceConversionResult<Company>(
                new Company(),
                issues);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Value);
            Assert.AreEqual(1, result.Issues.Count);
        }

        /// <summary>
        /// 確認 Company 經 DTO 往返後保留時間 offset 與所有欄位。
        /// </summary>
        [Test]
        public void CompanyMapper_CompleteCompany_RoundTrips()
        {
            Company original = CreateCompany();

            PersistenceConversionResult<CompanyDto> toDto = CompanyDtoMapper.ToDto(original);
            PersistenceConversionResult<Company> toDomain = CompanyDtoMapper.ToDomain(toDto.Value);

            Assert.IsTrue(toDto.IsSuccess);
            Assert.IsTrue(toDomain.IsSuccess);
            Assert.AreEqual(original.Id, toDomain.Value.Id);
            Assert.AreEqual(original.Name, toDomain.Value.Name);
            Assert.AreEqual(original.CreatedAt, toDomain.Value.CreatedAt);
            Assert.AreEqual(original.UpdatedAt, toDomain.Value.UpdatedAt);
            CollectionAssert.AreEqual(original.RiskFlags, toDomain.Value.RiskFlags);
        }

        /// <summary>
        /// 確認 Company 的風險集合不會在 Domain 與 DTO 之間共用可變 List。
        /// </summary>
        [Test]
        public void CompanyMapper_ToDto_CopiesRiskFlags()
        {
            Company original = CreateCompany();
            CompanyDto dto = CompanyDtoMapper.ToDto(original).Value;

            dto.risk_flags.Add("DTO only");

            CollectionAssert.DoesNotContain(original.RiskFlags, "DTO only");
        }

        /// <summary>
        /// 確認 Company optional 時間若包含錯誤文字，會回報欄位而不建立 Domain Value。
        /// </summary>
        [Test]
        public void CompanyMapper_InvalidOptionalDate_ReturnsLocatedIssue()
        {
            var dto = new CompanyDto
            {
                id = "cmp_0123456789abcdef0123456789abcdef",
                schema_version = "0.2",
                name = "測試公司",
                updated_at = "昨天"
            };

            PersistenceConversionResult<Company> result = CompanyDtoMapper.ToDomain(dto);

            AssertIssue(
                result.Issues,
                PersistenceConversionError.InvalidDateTime,
                "updated_at",
                "昨天");
            Assert.IsNull(result.Value);
        }

        /// <summary>
        /// 確認完整 JobPosting 的巢狀物件與集合往返後保持一致。
        /// </summary>
        [Test]
        public void JobPostingMapper_CompleteJobPosting_RoundTrips()
        {
            JobPosting original = CreateJobPosting();

            PersistenceConversionResult<JobPostingDto> toDto =
                JobPostingDtoMapper.ToDto(original);
            PersistenceConversionResult<JobPosting> toDomain =
                JobPostingDtoMapper.ToDomain(toDto.Value);

            Assert.IsTrue(toDto.IsSuccess);
            Assert.IsTrue(toDomain.IsSuccess);
            Assert.AreEqual(original.CapturedAt, toDomain.Value.CapturedAt);
            Assert.AreEqual(original.Compensation.Minimum, toDomain.Value.Compensation.Minimum);
            Assert.AreEqual(original.Compensation.Maximum, toDomain.Value.Compensation.Maximum);
            Assert.AreEqual(original.Location.RemoteAllowed, toDomain.Value.Location.RemoteAllowed);
            Assert.AreEqual("TypeScript", toDomain.Value.Requirements.Tools[1]);
            Assert.AreEqual("技術面談", toDomain.Value.RecruitmentProcess[1]);
            Assert.AreEqual("英文", toDomain.Value.Requirements.Languages[0].Name);
            Assert.AreEqual("績效獎金", toDomain.Value.Benefits.SalaryBonus[0]);
        }

        /// <summary>
        /// 確認沒有薪資上限與沒有遠端資訊時，presence flags 往返後仍保持 null。
        /// </summary>
        [Test]
        public void JobPostingMapper_NullableDetailValues_RoundTripAsNull()
        {
            JobPosting original = CreateJobPosting();
            original.Compensation.Maximum = null;
            original.Location.RemoteAllowed = null;

            JobPostingDto dto = JobPostingDtoMapper.ToDto(original).Value;
            JobPosting mapped = JobPostingDtoMapper.ToDomain(dto).Value;

            Assert.IsFalse(dto.compensation.has_max);
            Assert.IsFalse(dto.location.has_remote_allowed);
            Assert.IsNull(mapped.Compensation.Maximum);
            Assert.IsNull(mapped.Location.RemoteAllowed);
        }

        /// <summary>
        /// 確認 optional 詳細物件完全缺漏時仍可轉換，不會自動建立假內容。
        /// </summary>
        [Test]
        public void JobPostingMapper_MissingOptionalObjects_PreservesNull()
        {
            JobPosting original = CreateJobPosting();
            original.Compensation = null;
            original.Location = null;
            original.WorkConditions = null;
            original.Requirements = null;
            original.Benefits = null;

            JobPosting mapped = JobPostingDtoMapper.ToDomain(
                JobPostingDtoMapper.ToDto(original).Value).Value;

            Assert.IsNull(mapped.Compensation);
            Assert.IsNull(mapped.Location);
            Assert.IsNull(mapped.WorkConditions);
            Assert.IsNull(mapped.Requirements);
            Assert.IsNull(mapped.Benefits);
        }

        /// <summary>
        /// 確認 DTO 薪資帶值卻沒有 has_min 時不會靜默丟掉該值。
        /// </summary>
        [Test]
        public void JobPostingMapper_MinWithoutPresenceFlag_ReturnsIssue()
        {
            JobPostingDto dto = CreateJobPostingDto();
            dto.compensation.has_min = false;
            dto.compensation.min = 45000;

            PersistenceConversionResult<JobPosting> result =
                JobPostingDtoMapper.ToDomain(dto);

            AssertIssue(
                result.Issues,
                PersistenceConversionError.ValuePresentWithoutPresenceFlag,
                "compensation.min",
                "45000");
            Assert.IsNull(result.Value);
        }

        /// <summary>
        /// 確認缺少 required source 物件時會明確失敗，而不是建立空 JobSource。
        /// </summary>
        [Test]
        public void JobPostingMapper_MissingSource_ReturnsIssue()
        {
            JobPostingDto dto = CreateJobPostingDto();
            dto.source = null;

            PersistenceConversionResult<JobPosting> result =
                JobPostingDtoMapper.ToDomain(dto);

            AssertIssue(
                result.Issues,
                PersistenceConversionError.MissingRequiredObject,
                "source",
                null);
            Assert.IsNull(result.Value);
        }

        /// <summary>
        /// 確認 captured_at 格式無效時會保留原值與欄位路徑。
        /// </summary>
        [Test]
        public void JobPostingMapper_InvalidCapturedAt_ReturnsLocatedIssue()
        {
            JobPostingDto dto = CreateJobPostingDto();
            dto.captured_at = "2026-09-06T14:30:00";

            PersistenceConversionResult<JobPosting> result =
                JobPostingDtoMapper.ToDomain(dto);

            AssertIssue(
                result.Issues,
                PersistenceConversionError.InvalidDateTime,
                "captured_at",
                dto.captured_at);
            Assert.IsNull(result.Value);
        }

        /// <summary>
        /// 確認 Mapper 會複製 JobPosting 集合，不讓 DTO 修改回頭污染 Domain。
        /// </summary>
        [Test]
        public void JobPostingMapper_ToDto_CopiesNestedCollections()
        {
            JobPosting original = CreateJobPosting();
            JobPostingDto dto = JobPostingDtoMapper.ToDto(original).Value;

            dto.requirements.tools.Add("DTO only");
            dto.benefits.other.Add("DTO only");

            CollectionAssert.DoesNotContain(original.Requirements.Tools, "DTO only");
            CollectionAssert.DoesNotContain(original.Benefits.Other, "DTO only");
        }

        /// <summary>
        /// 確認一般 Application 與內嵌事件往返後保留 enum、時間、關聯與事件數量。
        /// </summary>
        [Test]
        public void ApplicationMapper_ApplicationWithEvent_RoundTrips()
        {
            Application application = CreateApplication();
            ApplicationEvent applicationEvent = CreateApplicationEvent(application.Id);

            PersistenceConversionResult<ApplicationDto> toDto = ApplicationDtoMapper.ToDto(
                application,
                new[] { applicationEvent });
            PersistenceConversionResult<ApplicationPersistenceBundle> toDomain =
                ApplicationDtoMapper.ToDomain(toDto.Value);

            Assert.IsTrue(toDto.IsSuccess);
            Assert.AreEqual("my_application", toDto.Value.source_type);
            Assert.AreEqual("interview_scheduled", toDto.Value.current_stage);
            Assert.AreEqual("interview_scheduled", toDto.Value.events[0].event_type);
            Assert.IsTrue(toDomain.IsSuccess);
            Assert.AreEqual(application.CurrentStage, toDomain.Value.Application.CurrentStage);
            Assert.AreEqual(application.CreatedAt, toDomain.Value.Application.CreatedAt);
            Assert.AreEqual(1, toDomain.Value.Events.Count);
            Assert.AreEqual(applicationEvent.OccurredAt, toDomain.Value.Events[0].OccurredAt);
        }

        /// <summary>
        /// 確認 migration snapshot 的 optional SnapshotStage 可以正常雙向轉換。
        /// </summary>
        [Test]
        public void ApplicationEventMapper_MigrationSnapshot_RoundTripsSnapshotStage()
        {
            ApplicationEvent applicationEvent = CreateApplicationEvent(
                "app_0123456789abcdef0123456789abcdef");
            applicationEvent.EventType = ApplicationEventType.MigrationSnapshot;
            applicationEvent.Actor = EventActor.System;
            applicationEvent.SnapshotStage = ApplicationStage.WaitingResponse;
            applicationEvent.TimePrecision = ApplicationEvent.DateTimePrecision;

            ApplicationEventDto dto = ApplicationEventDtoMapper.ToDto(applicationEvent).Value;
            ApplicationEvent mapped = ApplicationEventDtoMapper.ToDomain(dto).Value;

            Assert.AreEqual("migration_snapshot", dto.event_type);
            Assert.AreEqual("waiting_response", dto.snapshot_stage);
            Assert.AreEqual(ApplicationStage.WaitingResponse, mapped.SnapshotStage);
        }

        /// <summary>
        /// 確認 Application 的未知 source_type 不會被轉成 MyApplication。
        /// </summary>
        [Test]
        public void ApplicationMapper_InvalidSourceType_ReturnsLocatedIssue()
        {
            ApplicationDto dto = CreateApplicationDto();
            dto.source_type = "banana";

            PersistenceConversionResult<ApplicationPersistenceBundle> result =
                ApplicationDtoMapper.ToDomain(dto);

            AssertIssue(
                result.Issues,
                PersistenceConversionError.InvalidEnum,
                "source_type",
                "banana");
            Assert.IsNull(result.Value);
        }

        /// <summary>
        /// 確認內嵌事件時間錯誤時，整份 Application 失敗並指出 events[index] 欄位。
        /// </summary>
        [Test]
        public void ApplicationMapper_InvalidEmbeddedEventDate_FailsWholeResult()
        {
            ApplicationDto dto = CreateApplicationDto();
            dto.events[0].occurred_at = "昨天";

            PersistenceConversionResult<ApplicationPersistenceBundle> result =
                ApplicationDtoMapper.ToDomain(dto);

            AssertIssue(
                result.Issues,
                PersistenceConversionError.InvalidDateTime,
                "events[0].occurred_at",
                "昨天");
            Assert.IsNull(result.Value);
        }

        /// <summary>
        /// 確認 Domain 使用未定義 enum 數值時，寫出 DTO 也會失敗而非產生未知字串。
        /// </summary>
        [Test]
        public void ApplicationMapper_UndefinedDomainStage_ReturnsIssue()
        {
            Application application = CreateApplication();
            application.CurrentStage = (ApplicationStage)999;

            PersistenceConversionResult<ApplicationDto> result =
                ApplicationDtoMapper.ToDto(application, null);

            AssertIssue(
                result.Issues,
                PersistenceConversionError.InvalidEnum,
                "current_stage",
                "999");
            Assert.IsNull(result.Value);
        }

        /// <summary>
        /// 確認 ApplicationPersistenceBundle 會固定事件成員，不受來源 List 後續新增影響。
        /// </summary>
        [Test]
        public void ApplicationBundle_SourceListChanges_DoesNotChangeEvents()
        {
            var events = new List<ApplicationEvent>
            {
                CreateApplicationEvent("app_0123456789abcdef0123456789abcdef")
            };
            var bundle = new ApplicationPersistenceBundle(CreateApplication(), events);

            events.Add(CreateApplicationEvent("app_0123456789abcdef0123456789abcdef"));

            Assert.AreEqual(1, bundle.Events.Count);
        }

        /// <summary>
        /// 確認所有公開 Mapper 對 null 根物件都回報 MissingSourceObject。
        /// </summary>
        [Test]
        public void Mappers_NullRootObjects_ReturnMissingSourceObject()
        {
            AssertHasMissingSource(CompanyDtoMapper.ToDomain(null).Issues);
            AssertHasMissingSource(CompanyDtoMapper.ToDto(null).Issues);
            AssertHasMissingSource(JobPostingDtoMapper.ToDomain(null).Issues);
            AssertHasMissingSource(JobPostingDtoMapper.ToDto(null).Issues);
            AssertHasMissingSource(ApplicationEventDtoMapper.ToDomain(null).Issues);
            AssertHasMissingSource(ApplicationEventDtoMapper.ToDto(null).Issues);
            AssertHasMissingSource(ApplicationDtoMapper.ToDomain(null).Issues);
            AssertHasMissingSource(ApplicationDtoMapper.ToDto(null, null).Issues);
        }

        /// <summary>
        /// 確認指定問題集合包含 MissingSourceObject。
        /// </summary>
        private static void AssertHasMissingSource(
            IReadOnlyList<PersistenceConversionIssue> issues)
        {
            Assert.IsTrue(issues.Any(issue =>
                issue.Error == PersistenceConversionError.MissingSourceObject));
        }

        /// <summary>
        /// 確認問題集合包含指定代碼、欄位路徑與原始值。
        /// </summary>
        private static void AssertIssue(
            IReadOnlyList<PersistenceConversionIssue> issues,
            PersistenceConversionError error,
            string fieldPath,
            string rawValue)
        {
            PersistenceConversionIssue issue = issues.FirstOrDefault(candidate =>
                candidate.Error == error && candidate.FieldPath == fieldPath);

            Assert.IsNotNull(
                issue,
                "Expected {0} at {1}, but got: {2}",
                error,
                fieldPath,
                string.Join(", ", issues.Select(candidate =>
                    candidate.Error + "@" + candidate.FieldPath)));
            Assert.AreEqual(rawValue, issue.RawValue);
        }

        private static Company CreateCompany()
        {
            return new Company
            {
                Id = "cmp_0123456789abcdef0123456789abcdef",
                SchemaVersion = Company.CurrentSchemaVersion,
                Name = "測試公司",
                Industry = "軟體服務",
                Notes = "公司備註",
                RiskFlags = new List<string> { "資訊不透明" },
                CreatedAt = BaseTime,
                UpdatedAt = BaseTime.AddHours(1)
            };
        }

        private static JobPosting CreateJobPosting()
        {
            return new JobPosting
            {
                Id = "job_0123456789abcdef0123456789abcdef",
                SchemaVersion = JobPosting.CurrentSchemaVersion,
                CompanyId = "cmp_0123456789abcdef0123456789abcdef",
                Title = "Unity 工程師",
                Source = new JobSource
                {
                    Platform = "104",
                    Url = "https://www.104.com.tw/job/example"
                },
                CapturedAt = BaseTime,
                Department = "研發部",
                Category = "軟體工程師",
                Compensation = new JobCompensation
                {
                    Type = "range",
                    Period = "monthly",
                    Minimum = 45000,
                    Maximum = 65000,
                    Currency = "TWD",
                    RawText = "月薪 45,000~65,000 元",
                    Notes = "依經驗調整"
                },
                Location = new JobLocation
                {
                    WorkMode = "onsite",
                    City = "台北市",
                    District = "信義區",
                    Address = "台北市信義區測試路100號",
                    RemoteAllowed = false,
                    RawText = "台北市信義區測試路100號"
                },
                WorkConditions = new JobWorkConditions
                {
                    EmploymentType = "全職",
                    WorkingHours = "日班",
                    BusinessTrip = "無需出差",
                    ManagementResponsibility = "不需管理",
                    LeavePolicy = "依公司規定",
                    StartDate = "一個月內"
                },
                Responsibilities = new List<string> { "開發", "維護" },
                Requirements = new JobRequirements
                {
                    Experience = "一年以上",
                    Education = "專科以上",
                    Major = "不拘",
                    Languages = new List<JobLanguageRequirement>
                    {
                        new JobLanguageRequirement { Name = "英文", Reading = "中等" }
                    },
                    Tools = new List<string> { "Unity", "TypeScript" },
                    Skills = new List<string> { "程式設計" },
                    OtherConditions = new List<string> { "溝通能力" }
                },
                Benefits = new JobBenefits
                {
                    SalaryBonus = new List<string> { "績效獎金" },
                    InsuranceHealth = new List<string> { "勞健保" },
                    Flexibility = new List<string> { "彈性上下班" },
                    Training = new List<string> { "教育訓練" },
                    Life = new List<string> { "部門聚餐" },
                    Other = new List<string> { "設備補助" }
                },
                RecruitmentProcess = new List<string> { "初步面談", "技術面談" },
                Tags = new List<string> { "Unity" },
                RiskFlags = new List<string> { "薪資上限未保證" },
                RawDescription = "完整職缺描述",
                LegacyId = "demo_job_001"
            };
        }

        private static JobPostingDto CreateJobPostingDto()
        {
            return JobPostingDtoMapper.ToDto(CreateJobPosting()).Value;
        }

        private static Application CreateApplication()
        {
            return new Application
            {
                Id = "app_0123456789abcdef0123456789abcdef",
                SchemaVersion = Application.CurrentSchemaVersion,
                JobPostingId = "job_0123456789abcdef0123456789abcdef",
                SourceType = SourceType.MyApplication,
                CurrentStage = ApplicationStage.InterviewScheduled,
                CreatedAt = BaseTime,
                UpdatedAt = BaseTime.AddMinutes(30),
                Notes = "應徵備註",
                IsFavorite = true,
                ManualFollowUpAt = BaseTime.AddDays(3)
            };
        }

        private static ApplicationEvent CreateApplicationEvent(string applicationId)
        {
            return new ApplicationEvent
            {
                Id = "evt_0123456789abcdef0123456789abcdef",
                SchemaVersion = ApplicationEvent.CurrentSchemaVersion,
                ApplicationId = applicationId,
                EventType = ApplicationEventType.InterviewScheduled,
                OccurredAt = BaseTime.AddMinutes(20),
                RecordedAt = BaseTime.AddMinutes(21),
                Actor = EventActor.Company,
                Notes = "安排面試"
            };
        }

        private static ApplicationDto CreateApplicationDto()
        {
            return ApplicationDtoMapper.ToDto(
                CreateApplication(),
                new[]
                {
                    CreateApplicationEvent("app_0123456789abcdef0123456789abcdef")
                }).Value;
        }
    }
}
