using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace JobCheck.Persistence.Tests
{
    /// <summary>
    /// 驗證 V0.2 DTO 維持 JsonUtility 可使用的 public fields、snake_case 與安全集合預設值。
    /// </summary>
    public class PersistenceDtoTests
    {
        private static readonly Type[] DtoTypes =
        {
            typeof(CompanyDto),
            typeof(JobPostingDto),
            typeof(JobSourceDto),
            typeof(JobCompensationDto),
            typeof(JobLocationDto),
            typeof(JobWorkConditionsDto),
            typeof(JobRequirementsDto),
            typeof(JobLanguageRequirementDto),
            typeof(JobBenefitsDto),
            typeof(ApplicationDto),
            typeof(ApplicationEventDto)
        };

        /// <summary>
        /// 確認所有 DTO 都能由 JsonUtility 使用無參數建構方式建立。
        /// </summary>
        [Test]
        public void Dtos_AllHavePublicParameterlessConstructors()
        {
            foreach (Type dtoType in DtoTypes)
            {
                Assert.IsNotNull(
                    dtoType.GetConstructor(Type.EmptyTypes),
                    "{0} must have a public parameterless constructor.",
                    dtoType.Name);
            }
        }

        /// <summary>
        /// 確認 DTO 沒有使用 properties 隱藏 JSON 欄位，所有保存資料皆為 public instance field。
        /// </summary>
        [Test]
        public void Dtos_DoNotDeclarePersistenceProperties()
        {
            foreach (Type dtoType in DtoTypes)
            {
                PropertyInfo[] properties = dtoType.GetProperties(
                    BindingFlags.Public
                    | BindingFlags.Instance
                    | BindingFlags.DeclaredOnly);

                Assert.IsEmpty(properties, "{0} contains persistence properties.", dtoType.Name);
                Assert.IsNotEmpty(
                    GetDeclaredFields(dtoType),
                    "{0} must expose at least one public field.",
                    dtoType.Name);
            }
        }

        /// <summary>
        /// 確認所有 JSON 欄位名稱均為小寫 snake_case，避免輸出 PascalCase 或混合命名。
        /// </summary>
        [Test]
        public void Dtos_AllFieldNamesUseLowerSnakeCase()
        {
            foreach (Type dtoType in DtoTypes)
            {
                foreach (FieldInfo field in GetDeclaredFields(dtoType))
                {
                    StringAssert.IsMatch(
                        "^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$",
                        field.Name,
                        "{0}.{1} is not lower snake_case.",
                        dtoType.Name,
                        field.Name);
                }
            }
        }

        /// <summary>
        /// 確認 Company 與 JobPosting 的多值欄位預設可直接加入資料。
        /// </summary>
        [Test]
        public void CompanyAndJobPostingDtos_InitializeCollections()
        {
            var company = new CompanyDto();
            var jobPosting = new JobPostingDto();

            Assert.IsNotNull(company.risk_flags);
            Assert.IsNotNull(jobPosting.responsibilities);
            Assert.IsNotNull(jobPosting.recruitment_process);
            Assert.IsNotNull(jobPosting.tags);
            Assert.IsNotNull(jobPosting.risk_flags);
        }

        [Test]
        public void OldJobRequirementsJson_LoadsWithoutV027Fields()
        {
            const string json = "{\"experience\":\"3 年以上\",\"education\":\"大學\","
                + "\"major\":null,\"languages\":[],\"tools\":[],\"skills\":[],"
                + "\"other_conditions\":[]}";

            Assert.IsTrue(PersistenceJsonSerializer.TryDeserialize(
                json, out JobRequirementsDto dto, out string error), error);
            Assert.AreEqual("3 年以上", dto.experience);
            Assert.IsNull(dto.education_requirement);
        }

        [Test]
        public void OldCareerEducationJson_LoadsWithoutV027Fields()
        {
            const string json = "{\"id\":\"edu_1\",\"institution\":\"學校\","
                + "\"program\":\"資訊工程\",\"start_date\":\"2020\","
                + "\"end_date\":\"2024\",\"notes\":null}";

            Assert.IsTrue(PersistenceJsonSerializer.TryDeserialize(
                json, out CareerEducationDto dto, out string error), error);
            Assert.AreEqual("學校", dto.institution);
            Assert.IsNull(dto.degree_level);
        }

        [Test]
        public void OtherOldCareerItems_LoadWithoutV027Fields()
        {
            const string skill = "{\"id\":\"skill_1\",\"name\":\"Unity\",\"notes\":null}";
            const string experience = "{\"id\":\"exp_1\",\"organization\":\"公司\","
                + "\"role\":\"工程師\",\"start_date\":\"2020/01\",\"end_date\":\"2024/01\","
                + "\"is_current\":false,\"description\":null}";
            const string language = "{\"id\":\"lang_1\",\"name\":\"中文\","
                + "\"level\":\"母語\",\"notes\":null}";

            Assert.IsTrue(PersistenceJsonSerializer.TryDeserialize(
                skill, out CareerSkillDto skillDto, out string skillError), skillError);
            Assert.IsTrue(PersistenceJsonSerializer.TryDeserialize(
                experience, out CareerExperienceDto experienceDto, out string experienceError),
                experienceError);
            Assert.IsTrue(PersistenceJsonSerializer.TryDeserialize(
                language, out CareerLanguageDto languageDto, out string languageError),
                languageError);
            Assert.AreEqual("Unity", skillDto.name);
            Assert.AreEqual("公司", experienceDto.organization);
            Assert.AreEqual("中文", languageDto.name);
        }

        [Test]
        public void OldLanguageRequirement_LoadsWithoutV027Fields()
        {
            const string json = "{\"name\":\"英文\",\"listening\":null,"
                + "\"speaking\":null,\"reading\":null,\"writing\":null,"
                + "\"raw_text\":\"英文流利\"}";

            Assert.IsTrue(PersistenceJsonSerializer.TryDeserialize(
                json, out JobLanguageRequirementDto dto, out string error), error);
            Assert.AreEqual("英文", dto.name);
            Assert.IsNull(dto.minimum_proficiency);
        }

        /// <summary>
        /// 確認 Application 第一版會內嵌事件集合，而且預設不是 null。
        /// </summary>
        [Test]
        public void ApplicationDto_InitializesEmbeddedEvents()
        {
            var application = new ApplicationDto();

            Assert.IsNotNull(application.events);
            Assert.IsEmpty(application.events);
        }

        /// <summary>
        /// 確認薪資 DTO 可以區分實際 0 元與來源未提供數值。
        /// </summary>
        [Test]
        public void CompensationDto_PresenceFlagsDistinguishZeroFromMissing()
        {
            var missing = new JobCompensationDto();
            var actualZero = new JobCompensationDto { has_min = true, min = 0 };

            Assert.IsFalse(missing.has_min);
            Assert.AreEqual(0, missing.min);
            Assert.IsTrue(actualZero.has_min);
            Assert.AreEqual(0, actualZero.min);
        }

        /// <summary>
        /// 確認遠端工作 DTO 可以區分明確 false 與來源未說明。
        /// </summary>
        [Test]
        public void LocationDto_PresenceFlagDistinguishesFalseFromMissing()
        {
            var missing = new JobLocationDto();
            var explicitlyNotRemote = new JobLocationDto
            {
                has_remote_allowed = true,
                remote_allowed = false
            };

            Assert.IsFalse(missing.has_remote_allowed);
            Assert.IsTrue(explicitlyNotRemote.has_remote_allowed);
            Assert.IsFalse(explicitlyNotRemote.remote_allowed);
        }

        /// <summary>
        /// 取得 DTO 自己宣告的 public instance fields，不包含繼承成員。
        /// </summary>
        private static IReadOnlyList<FieldInfo> GetDeclaredFields(Type dtoType)
        {
            return dtoType.GetFields(
                BindingFlags.Public
                | BindingFlags.Instance
                | BindingFlags.DeclaredOnly);
        }
    }
}
