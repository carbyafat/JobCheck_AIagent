using System.Collections.Generic;
using JobCheck.Domain;

namespace JobCheck.Persistence
{
    /// <summary>
    /// 在 JobPosting Domain 模型與完整職缺 DTO 之間轉換欄位與 nullable presence flags。
    /// </summary>
    public static class JobPostingDtoMapper
    {
        /// <summary>
        /// 將 JobPostingDto 轉成 Domain；薪資或清單內容是否合理仍交給 JobPostingValidator。
        /// </summary>
        public static PersistenceConversionResult<JobPosting> ToDomain(JobPostingDto dto)
        {
            var context = new PersistenceMappingContext();
            if (dto == null)
            {
                context.Add(PersistenceConversionError.MissingSourceObject, "job_posting");
                return context.CreateResult<JobPosting>(null);
            }

            JobSource source = MapSourceToDomain(dto.source, context);
            var jobPosting = new JobPosting
            {
                Id = dto.id,
                SchemaVersion = dto.schema_version,
                CompanyId = dto.company_id,
                Title = dto.title,
                Source = source,
                CapturedAt = context.ParseRequiredDateTime(dto.captured_at, "captured_at"),
                Department = dto.department,
                Category = dto.category,
                Compensation = MapCompensationToDomain(dto.compensation, context),
                Location = MapLocationToDomain(dto.location, context),
                WorkConditions = MapWorkConditionsToDomain(dto.work_conditions),
                Responsibilities = PersistenceListMapper.Copy(dto.responsibilities),
                Requirements = MapRequirementsToDomain(dto.requirements),
                Benefits = MapBenefitsToDomain(dto.benefits),
                RecruitmentProcess = PersistenceListMapper.Copy(dto.recruitment_process),
                Tags = PersistenceListMapper.Copy(dto.tags),
                RiskFlags = PersistenceListMapper.Copy(dto.risk_flags),
                RawDescription = dto.raw_description,
                LegacyId = dto.legacy_id
            };

            return context.CreateResult(jobPosting);
        }

        /// <summary>
        /// 將 Domain JobPosting 轉成 DTO；undefined enum 不在此模型中，內容規則仍由 Validator 負責。
        /// </summary>
        public static PersistenceConversionResult<JobPostingDto> ToDto(JobPosting jobPosting)
        {
            var context = new PersistenceMappingContext();
            if (jobPosting == null)
            {
                context.Add(PersistenceConversionError.MissingSourceObject, "job_posting");
                return context.CreateResult<JobPostingDto>(null);
            }

            JobSourceDto source = MapSourceToDto(jobPosting.Source, context);
            var dto = new JobPostingDto
            {
                id = jobPosting.Id,
                schema_version = jobPosting.SchemaVersion,
                company_id = jobPosting.CompanyId,
                title = jobPosting.Title,
                source = source,
                captured_at = PersistenceDateTimeConverter.Format(jobPosting.CapturedAt),
                department = jobPosting.Department,
                category = jobPosting.Category,
                compensation = MapCompensationToDto(jobPosting.Compensation),
                location = MapLocationToDto(jobPosting.Location),
                work_conditions = MapWorkConditionsToDto(jobPosting.WorkConditions),
                responsibilities = PersistenceListMapper.Copy(jobPosting.Responsibilities),
                requirements = MapRequirementsToDto(jobPosting.Requirements),
                benefits = MapBenefitsToDto(jobPosting.Benefits),
                recruitment_process = PersistenceListMapper.Copy(jobPosting.RecruitmentProcess),
                tags = PersistenceListMapper.Copy(jobPosting.Tags),
                risk_flags = PersistenceListMapper.Copy(jobPosting.RiskFlags),
                raw_description = jobPosting.RawDescription,
                legacy_id = jobPosting.LegacyId
            };

            return context.CreateResult(dto);
        }

        private static JobSource MapSourceToDomain(
            JobSourceDto dto,
            PersistenceMappingContext context)
        {
            if (dto == null)
            {
                context.Add(PersistenceConversionError.MissingRequiredObject, "source");
                return null;
            }

            return new JobSource { Platform = dto.platform, Url = dto.url };
        }

        private static JobSourceDto MapSourceToDto(
            JobSource source,
            PersistenceMappingContext context)
        {
            if (source == null)
            {
                context.Add(PersistenceConversionError.MissingRequiredObject, "source");
                return null;
            }

            return new JobSourceDto { platform = source.Platform, url = source.Url };
        }

        private static JobCompensation MapCompensationToDomain(
            JobCompensationDto dto,
            PersistenceMappingContext context)
        {
            if (dto == null)
            {
                return null;
            }

            if (!dto.has_min && dto.min != 0)
            {
                context.Add(
                    PersistenceConversionError.ValuePresentWithoutPresenceFlag,
                    "compensation.min",
                    dto.min.ToString());
            }

            if (!dto.has_max && dto.max != 0)
            {
                context.Add(
                    PersistenceConversionError.ValuePresentWithoutPresenceFlag,
                    "compensation.max",
                    dto.max.ToString());
            }

            return new JobCompensation
            {
                Type = dto.type,
                Period = dto.period,
                Minimum = dto.has_min ? dto.min : (int?)null,
                Maximum = dto.has_max ? dto.max : (int?)null,
                Currency = dto.currency,
                RawText = dto.raw_text,
                Notes = dto.notes
            };
        }

        private static JobCompensationDto MapCompensationToDto(JobCompensation compensation)
        {
            if (compensation == null)
            {
                return null;
            }

            return new JobCompensationDto
            {
                type = compensation.Type,
                period = compensation.Period,
                has_min = compensation.Minimum.HasValue,
                min = compensation.Minimum.GetValueOrDefault(),
                has_max = compensation.Maximum.HasValue,
                max = compensation.Maximum.GetValueOrDefault(),
                currency = compensation.Currency,
                raw_text = compensation.RawText,
                notes = compensation.Notes
            };
        }

        private static JobLocation MapLocationToDomain(
            JobLocationDto dto,
            PersistenceMappingContext context)
        {
            if (dto == null)
            {
                return null;
            }

            if (!dto.has_remote_allowed && dto.remote_allowed)
            {
                context.Add(
                    PersistenceConversionError.ValuePresentWithoutPresenceFlag,
                    "location.remote_allowed",
                    "true");
            }

            return new JobLocation
            {
                WorkMode = dto.work_mode,
                City = dto.city,
                District = dto.district,
                Address = dto.address,
                RemoteAllowed = dto.has_remote_allowed
                    ? dto.remote_allowed
                    : (bool?)null,
                RawText = dto.raw_text
            };
        }

        private static JobLocationDto MapLocationToDto(JobLocation location)
        {
            if (location == null)
            {
                return null;
            }

            return new JobLocationDto
            {
                work_mode = location.WorkMode,
                city = location.City,
                district = location.District,
                address = location.Address,
                has_remote_allowed = location.RemoteAllowed.HasValue,
                remote_allowed = location.RemoteAllowed.GetValueOrDefault(),
                raw_text = location.RawText
            };
        }

        private static JobWorkConditions MapWorkConditionsToDomain(JobWorkConditionsDto dto)
        {
            return dto == null
                ? null
                : new JobWorkConditions
                {
                    EmploymentType = dto.employment_type,
                    WorkingHours = dto.working_hours,
                    BusinessTrip = dto.business_trip,
                    ManagementResponsibility = dto.management_responsibility,
                    LeavePolicy = dto.leave_policy,
                    StartDate = dto.start_date
                };
        }

        private static JobWorkConditionsDto MapWorkConditionsToDto(JobWorkConditions value)
        {
            return value == null
                ? null
                : new JobWorkConditionsDto
                {
                    employment_type = value.EmploymentType,
                    working_hours = value.WorkingHours,
                    business_trip = value.BusinessTrip,
                    management_responsibility = value.ManagementResponsibility,
                    leave_policy = value.LeavePolicy,
                    start_date = value.StartDate
                };
        }

        private static JobRequirements MapRequirementsToDomain(JobRequirementsDto dto)
        {
            if (dto == null)
            {
                return null;
            }

            var languages = new List<JobLanguageRequirement>();
            if (dto.languages != null)
            {
                foreach (JobLanguageRequirementDto language in dto.languages)
                {
                    languages.Add(language == null
                        ? null
                        : new JobLanguageRequirement
                        {
                            Name = language.name,
                            Listening = language.listening,
                            Speaking = language.speaking,
                            Reading = language.reading,
                            Writing = language.writing,
                            RawText = language.raw_text
                        });
                }
            }

            return new JobRequirements
            {
                Experience = dto.experience,
                Education = dto.education,
                Major = dto.major,
                Languages = languages,
                Tools = PersistenceListMapper.Copy(dto.tools),
                Skills = PersistenceListMapper.Copy(dto.skills),
                OtherConditions = PersistenceListMapper.Copy(dto.other_conditions)
            };
        }

        private static JobRequirementsDto MapRequirementsToDto(JobRequirements value)
        {
            if (value == null)
            {
                return null;
            }

            var languages = new List<JobLanguageRequirementDto>();
            if (value.Languages != null)
            {
                foreach (JobLanguageRequirement language in value.Languages)
                {
                    languages.Add(language == null
                        ? null
                        : new JobLanguageRequirementDto
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

            return new JobRequirementsDto
            {
                experience = value.Experience,
                education = value.Education,
                major = value.Major,
                languages = languages,
                tools = PersistenceListMapper.Copy(value.Tools),
                skills = PersistenceListMapper.Copy(value.Skills),
                other_conditions = PersistenceListMapper.Copy(value.OtherConditions)
            };
        }

        private static JobBenefits MapBenefitsToDomain(JobBenefitsDto dto)
        {
            return dto == null
                ? null
                : new JobBenefits
                {
                    SalaryBonus = PersistenceListMapper.Copy(dto.salary_bonus),
                    InsuranceHealth = PersistenceListMapper.Copy(dto.insurance_health),
                    Flexibility = PersistenceListMapper.Copy(dto.flexibility),
                    Training = PersistenceListMapper.Copy(dto.training),
                    Life = PersistenceListMapper.Copy(dto.life),
                    Other = PersistenceListMapper.Copy(dto.other)
                };
        }

        private static JobBenefitsDto MapBenefitsToDto(JobBenefits value)
        {
            return value == null
                ? null
                : new JobBenefitsDto
                {
                    salary_bonus = PersistenceListMapper.Copy(value.SalaryBonus),
                    insurance_health = PersistenceListMapper.Copy(value.InsuranceHealth),
                    flexibility = PersistenceListMapper.Copy(value.Flexibility),
                    training = PersistenceListMapper.Copy(value.Training),
                    life = PersistenceListMapper.Copy(value.Life),
                    other = PersistenceListMapper.Copy(value.Other)
                };
        }
    }
}
