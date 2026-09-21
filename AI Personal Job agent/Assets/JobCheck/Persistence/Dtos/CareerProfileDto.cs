using System;
using System.Collections.Generic;

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
        public string skill_id;
        public string level;
        public bool has_claimed_months;
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
        public string degree_level;
        public string completion_status;
        public List<string> field_tags = new List<string>();
    }

    [Serializable]
    public sealed class CareerLanguageDto
    {
        public string id;
        public string name;
        public string level;
        public string notes;
        public string language_id;
        public string proficiency;
        public List<string> certifications = new List<string>();
    }
}
