using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace JobCheck.Persistence
{
    [Serializable]
    public sealed class CareerProfileDto
    {
        public string id;
        public string schema_version;
        public string summary;
        public List<CareerProfileLinkDto> links = new List<CareerProfileLinkDto>();
        public List<CareerSkillDto> skills = new List<CareerSkillDto>();
        public List<CareerExperienceDto> experiences = new List<CareerExperienceDto>();
        public List<CareerProjectDto> projects = new List<CareerProjectDto>();
        public List<CareerEducationDto> educations = new List<CareerEducationDto>();
        public List<CareerLanguageDto> languages = new List<CareerLanguageDto>();
        [OptionalField]
        public JobSearchPreferencesDto job_preferences;
        public string created_at;
        public string updated_at;
    }

    [Serializable]
    public sealed class CareerProfileLinkDto
    {
        public string id;
        public string label;
        public string url;
    }

    [Serializable]
    public sealed class CareerSkillDto
    {
        public string id;
        public string name;
        public string notes;
        [OptionalField]
        public string skill_id;
        [OptionalField]
        public string level;
        [OptionalField]
        public bool has_claimed_months;
        [OptionalField]
        public int claimed_months;
    }

    [Serializable]
    public sealed class CareerExperienceDto
    {
        public string id;
        public string organization;
        public string role;
        public string start_date;
        public string end_date;
        public bool is_current;
        public string description;
        [OptionalField]
        public List<string> skill_ids = new List<string>();
    }

    [Serializable]
    public sealed class CareerProjectDto
    {
        public string id;
        public string name;
        public string description;
        public List<string> technologies = new List<string>();
        public string url;
    }

    [Serializable]
    public sealed class CareerEducationDto
    {
        public string id;
        public string institution;
        public string program;
        public string start_date;
        public string end_date;
        public string notes;
        [OptionalField]
        public string degree_level;
        [OptionalField]
        public string completion_status;
        [OptionalField]
        public List<string> field_tags = new List<string>();
    }

    [Serializable]
    public sealed class CareerLanguageDto
    {
        public string id;
        public string name;
        public string level;
        public string notes;
        [OptionalField]
        public string language_id;
        [OptionalField]
        public string proficiency;
        [OptionalField]
        public List<string> certifications = new List<string>();
    }

    [Serializable]
    public sealed class JobSearchPreferencesDto
    {
        public List<string> target_roles = new List<string>();
        public List<string> industries = new List<string>();
        public List<string> accepted_regions = new List<string>();
        public List<string> employment_types = new List<string>();
        public List<string> work_modes = new List<string>();
        public List<string> work_schedules = new List<string>();
        public string salary_period;
        public bool has_minimum_salary;
        public int minimum_salary;
        public bool has_desired_salary;
        public int desired_salary;
        public bool has_maximum_commute_minutes;
        public int maximum_commute_minutes;
        public string overtime_preference;
        public string travel_preference;
        public string relocation_preference;
        public string notes;
        public string target_importance;
        public string salary_importance;
        public string arrangement_importance;
        public string schedule_importance;
        public string updated_at;
    }
}
