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
    }

    [Serializable]
    public sealed class CareerLanguageDto
    {
        public string id;
        public string name;
        public string level;
        public string notes;
    }
}
