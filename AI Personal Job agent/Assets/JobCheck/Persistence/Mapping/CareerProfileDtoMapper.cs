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
                Skills = Map(dto.skills, item => new CareerSkill
                {
                    Id = item.id,
                    Name = item.name,
                    Notes = item.notes
                }),
                Experiences = Map(dto.experiences, item => new CareerExperience
                {
                    Id = item.id,
                    Organization = item.organization,
                    Role = item.role,
                    StartDate = item.start_date,
                    EndDate = item.end_date,
                    IsCurrent = item.is_current,
                    Description = item.description
                }),
                Projects = Map(dto.projects, item => new CareerProject
                {
                    Id = item.id,
                    Name = item.name,
                    Description = item.description,
                    Technologies = PersistenceListMapper.Copy(item.technologies),
                    Url = item.url
                }),
                Educations = Map(dto.educations, item => new CareerEducation
                {
                    Id = item.id,
                    Institution = item.institution,
                    Program = item.program,
                    StartDate = item.start_date,
                    EndDate = item.end_date,
                    Notes = item.notes
                }),
                Languages = Map(dto.languages, item => new CareerLanguage
                {
                    Id = item.id,
                    Name = item.name,
                    Level = item.level,
                    Notes = item.notes
                }),
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
                    notes = item.Notes
                }),
                experiences = Map(profile.Experiences, item => new CareerExperienceDto
                {
                    id = item.Id,
                    organization = item.Organization,
                    role = item.Role,
                    start_date = item.StartDate,
                    end_date = item.EndDate,
                    is_current = item.IsCurrent,
                    description = item.Description
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
                    notes = item.Notes
                }),
                languages = Map(profile.Languages, item => new CareerLanguageDto
                {
                    id = item.Id,
                    name = item.Name,
                    level = item.Level,
                    notes = item.Notes
                }),
                created_at = PersistenceDateTimeConverter.Format(profile.CreatedAt),
                updated_at = PersistenceDateTimeConverter.Format(profile.UpdatedAt)
            };

            return context.CreateResult(dto);
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
    }
}
