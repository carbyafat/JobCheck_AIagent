using System.Collections.Generic;
using System.Linq;
using JobCheck.Domain;

namespace JobCheck.Persistence
{
    public static class CareerProfileDtoMapper
    {
        public static PersistenceConversionResult<CareerProfile> ToDomain(CareerProfileDto dto)
        {
            var context = new PersistenceMappingContext();
            if (dto == null)
            {
                context.Add(PersistenceConversionError.MissingSourceObject, "career_profile");
                return context.CreateResult<CareerProfile>(null);
            }

            var profile = new CareerProfile
            {
                Id = dto.id,
                SchemaVersion = dto.schema_version,
                Summary = dto.summary,
                Links = Map(dto.links, item => new CareerProfileLink
                {
                    Id = item.id,
                    Label = item.label,
                    Url = item.url
                }),
                Skills = Map(dto.skills, (item, index) => new CareerSkill
                {
                    Id = item.id,
                    Name = item.name,
                    Notes = item.notes,
                    SkillId = item.skill_id,
                    Level = context.ParseOptionalEnum<SkillLevel>(
                        item.level, "skills[" + index + "].level"),
                    ClaimedMonths = item.has_claimed_months ? item.claimed_months : (int?)null
                }),
                Experiences = Map(dto.experiences, item => new CareerExperience
                {
                    Id = item.id,
                    Organization = item.organization,
                    Role = item.role,
                    StartDate = item.start_date,
                    EndDate = item.end_date,
                    IsCurrent = item.is_current,
                    Description = item.description,
                    SkillIds = PersistenceListMapper.Copy(item.skill_ids)
                }),
                Projects = Map(dto.projects, item => new CareerProject
                {
                    Id = item.id,
                    Name = item.name,
                    Description = item.description,
                    Technologies = PersistenceListMapper.Copy(item.technologies),
                    Url = item.url
                }),
                Educations = Map(dto.educations, (item, index) => new CareerEducation
                {
                    Id = item.id,
                    Institution = item.institution,
                    Program = item.program,
                    StartDate = item.start_date,
                    EndDate = item.end_date,
                    Notes = item.notes,
                    DegreeLevel = context.ParseOptionalEnum<DegreeLevel>(
                        item.degree_level, "educations[" + index + "].degree_level"),
                    CompletionStatus = ParseEnumOrDefault(
                        context,
                        item.completion_status,
                        "educations[" + index + "].completion_status",
                        EducationCompletionStatus.Unknown),
                    FieldTags = PersistenceListMapper.Copy(item.field_tags)
                }),
                Languages = Map(dto.languages, (item, index) => new CareerLanguage
                {
                    Id = item.id,
                    Name = item.name,
                    Level = item.level,
                    Notes = item.notes,
                    LanguageId = item.language_id,
                    Proficiency = context.ParseOptionalEnum<LanguageProficiency>(
                        item.proficiency, "languages[" + index + "].proficiency"),
                    Certifications = PersistenceListMapper.Copy(item.certifications)
                }),
                JobPreferences = ToPreferences(dto.job_preferences, context),
                CreatedAt = context.ParseOptionalDateTime(dto.created_at, "created_at"),
                UpdatedAt = context.ParseOptionalDateTime(dto.updated_at, "updated_at")
            };

            return context.CreateResult(profile);
        }

        public static PersistenceConversionResult<CareerProfileDto> ToDto(CareerProfile profile)
        {
            var context = new PersistenceMappingContext();
            if (profile == null)
            {
                context.Add(PersistenceConversionError.MissingSourceObject, "career_profile");
                return context.CreateResult<CareerProfileDto>(null);
            }

            var dto = new CareerProfileDto
            {
                id = profile.Id,
                schema_version = profile.SchemaVersion,
                summary = profile.Summary,
                links = Map(profile.Links, item => new CareerProfileLinkDto
                {
                    id = item.Id,
                    label = item.Label,
                    url = item.Url
                }),
                skills = Map(profile.Skills, item => new CareerSkillDto
                {
                    id = item.Id,
                    name = item.Name,
                    notes = item.Notes,
                    skill_id = item.SkillId,
                    level = FormatOptionalEnum(item.Level),
                    has_claimed_months = item.ClaimedMonths.HasValue,
                    claimed_months = item.ClaimedMonths.GetValueOrDefault()
                }),
                experiences = Map(profile.Experiences, item => new CareerExperienceDto
                {
                    id = item.Id,
                    organization = item.Organization,
                    role = item.Role,
                    start_date = item.StartDate,
                    end_date = item.EndDate,
                    is_current = item.IsCurrent,
                    description = item.Description,
                    skill_ids = PersistenceListMapper.Copy(item.SkillIds)
                }),
                projects = Map(profile.Projects, item => new CareerProjectDto
                {
                    id = item.Id,
                    name = item.Name,
                    description = item.Description,
                    technologies = PersistenceListMapper.Copy(item.Technologies),
                    url = item.Url
                }),
                educations = Map(profile.Educations, item => new CareerEducationDto
                {
                    id = item.Id,
                    institution = item.Institution,
                    program = item.Program,
                    start_date = item.StartDate,
                    end_date = item.EndDate,
                    notes = item.Notes,
                    degree_level = FormatOptionalEnum(item.DegreeLevel),
                    completion_status = PersistenceEnumConverter.Format(item.CompletionStatus),
                    field_tags = PersistenceListMapper.Copy(item.FieldTags)
                }),
                languages = Map(profile.Languages, item => new CareerLanguageDto
                {
                    id = item.Id,
                    name = item.Name,
                    level = item.Level,
                    notes = item.Notes,
                    language_id = item.LanguageId,
                    proficiency = FormatOptionalEnum(item.Proficiency),
                    certifications = PersistenceListMapper.Copy(item.Certifications)
                }),
                job_preferences = ToPreferencesDto(profile.JobPreferences),
                created_at = PersistenceDateTimeConverter.Format(profile.CreatedAt),
                updated_at = PersistenceDateTimeConverter.Format(profile.UpdatedAt)
            };

            return context.CreateResult(dto);
        }

        private static JobSearchPreferences ToPreferences(
            JobSearchPreferencesDto dto,
            PersistenceMappingContext context)
        {
            if (dto == null) return new JobSearchPreferences();
            return new JobSearchPreferences
            {
                TargetRoles = PersistenceListMapper.Copy(dto.target_roles),
                Industries = PersistenceListMapper.Copy(dto.industries),
                AcceptedRegions = PersistenceListMapper.Copy(dto.accepted_regions),
                EmploymentTypes = PersistenceListMapper.Copy(dto.employment_types),
                WorkModes = PersistenceListMapper.Copy(dto.work_modes),
                WorkSchedules = PersistenceListMapper.Copy(dto.work_schedules),
                SalaryPeriod = ParseEnumOrDefault(
                    context, dto.salary_period, "job_preferences.salary_period", SalaryPeriod.Monthly),
                MinimumSalary = dto.has_minimum_salary ? dto.minimum_salary : (int?)null,
                DesiredSalary = dto.has_desired_salary ? dto.desired_salary : (int?)null,
                MaximumCommuteMinutes = dto.has_maximum_commute_minutes
                    ? dto.maximum_commute_minutes : (int?)null,
                OvertimePreference = dto.overtime_preference,
                TravelPreference = dto.travel_preference,
                RelocationPreference = dto.relocation_preference,
                Notes = dto.notes,
                TargetImportance = ParseEnumOrDefault(
                    context, dto.target_importance, "job_preferences.target_importance",
                    PreferenceImportance.Required),
                SalaryImportance = ParseEnumOrDefault(
                    context, dto.salary_importance, "job_preferences.salary_importance",
                    PreferenceImportance.Required),
                ArrangementImportance = ParseEnumOrDefault(
                    context, dto.arrangement_importance, "job_preferences.arrangement_importance",
                    PreferenceImportance.Preferred),
                ScheduleImportance = ParseEnumOrDefault(
                    context, dto.schedule_importance, "job_preferences.schedule_importance",
                    PreferenceImportance.Preferred),
                UpdatedAt = context.ParseOptionalDateTime(
                    dto.updated_at, "job_preferences.updated_at")
            };
        }

        private static JobSearchPreferencesDto ToPreferencesDto(JobSearchPreferences value)
        {
            value = value ?? new JobSearchPreferences();
            return new JobSearchPreferencesDto
            {
                target_roles = PersistenceListMapper.Copy(value.TargetRoles),
                industries = PersistenceListMapper.Copy(value.Industries),
                accepted_regions = PersistenceListMapper.Copy(value.AcceptedRegions),
                employment_types = PersistenceListMapper.Copy(value.EmploymentTypes),
                work_modes = PersistenceListMapper.Copy(value.WorkModes),
                work_schedules = PersistenceListMapper.Copy(value.WorkSchedules),
                salary_period = PersistenceEnumConverter.Format(value.SalaryPeriod),
                has_minimum_salary = value.MinimumSalary.HasValue,
                minimum_salary = value.MinimumSalary.GetValueOrDefault(),
                has_desired_salary = value.DesiredSalary.HasValue,
                desired_salary = value.DesiredSalary.GetValueOrDefault(),
                has_maximum_commute_minutes = value.MaximumCommuteMinutes.HasValue,
                maximum_commute_minutes = value.MaximumCommuteMinutes.GetValueOrDefault(),
                overtime_preference = value.OvertimePreference,
                travel_preference = value.TravelPreference,
                relocation_preference = value.RelocationPreference,
                notes = value.Notes,
                target_importance = PersistenceEnumConverter.Format(value.TargetImportance),
                salary_importance = PersistenceEnumConverter.Format(value.SalaryImportance),
                arrangement_importance = PersistenceEnumConverter.Format(
                    value.ArrangementImportance),
                schedule_importance = PersistenceEnumConverter.Format(value.ScheduleImportance),
                updated_at = PersistenceDateTimeConverter.Format(value.UpdatedAt)
            };
        }

        private static System.DateTimeOffset? ParseOptionalDateTime(string value)
        {
            return System.DateTimeOffset.TryParse(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out System.DateTimeOffset parsed)
                ? parsed
                : (System.DateTimeOffset?)null;
        }

        private static List<TTarget> Map<TSource, TTarget>(
            IEnumerable<TSource> source,
            System.Func<TSource, TTarget> map)
            where TSource : class
        {
            return source == null
                ? new List<TTarget>()
                : source.Where(item => item != null).Select(map).ToList();
        }

        private static List<TTarget> Map<TSource, TTarget>(
            IEnumerable<TSource> source,
            System.Func<TSource, int, TTarget> map)
            where TSource : class
        {
            return source == null
                ? new List<TTarget>()
                : source.Select((item, index) => new { item, index })
                    .Where(entry => entry.item != null)
                    .Select(entry => map(entry.item, entry.index))
                    .ToList();
        }

        private static TEnum ParseEnumOrDefault<TEnum>(
            PersistenceMappingContext context,
            string value,
            string fieldPath,
            TEnum fallback)
            where TEnum : struct
        {
            // Older files may omit newly introduced fields.  Missing values retain the
            // established fallback, but a present unrecognised value must fail the load.
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : context.ParseRequiredEnum<TEnum>(value, fieldPath);
        }

        private static string FormatOptionalEnum<TEnum>(TEnum? value)
            where TEnum : struct
        {
            return value.HasValue ? PersistenceEnumConverter.Format(value.Value) : null;
        }
    }
}
