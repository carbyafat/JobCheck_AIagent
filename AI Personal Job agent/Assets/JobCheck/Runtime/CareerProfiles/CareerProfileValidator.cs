using System;
using System.Collections.Generic;
using System.Linq;

namespace JobCheck.Domain
{
    public enum CareerProfileValidationError
    {
        ProfileMissing,
        IdMissing,
        InvalidSchemaVersion,
        UpdatedBeforeCreated,
        NullCollection,
        NullItem,
        ItemIdMissing,
        DuplicateItemId,
        TooManyPreferenceValues,
        DuplicatePreferenceValue,
        NegativePreferenceNumber,
        DesiredSalaryBelowMinimum
    }

    public sealed class CareerProfileValidationIssue
    {
        public CareerProfileValidationIssue(CareerProfileValidationError error, string fieldPath)
        {
            Error = error;
            FieldPath = fieldPath;
        }

        public CareerProfileValidationError Error { get; }
        public string FieldPath { get; }
    }

    /// <summary>
    /// 只檢查儲存與同步所需的結構完整性；內容空白代表草稿，不是驗證錯誤。
    /// </summary>
    public static class CareerProfileValidator
    {
        public static IReadOnlyList<CareerProfileValidationIssue> Validate(CareerProfile profile)
        {
            var issues = new List<CareerProfileValidationIssue>();
            if (profile == null)
            {
                issues.Add(new CareerProfileValidationIssue(
                    CareerProfileValidationError.ProfileMissing,
                    "profile"));
                return issues.AsReadOnly();
            }

            if (string.IsNullOrWhiteSpace(profile.Id))
            {
                issues.Add(new CareerProfileValidationIssue(
                    CareerProfileValidationError.IdMissing,
                    "id"));
            }

            if (!string.Equals(
                profile.SchemaVersion,
                CareerProfile.CurrentSchemaVersion,
                StringComparison.Ordinal))
            {
                issues.Add(new CareerProfileValidationIssue(
                    CareerProfileValidationError.InvalidSchemaVersion,
                    "schema_version"));
            }

            if (profile.CreatedAt.HasValue
                && profile.UpdatedAt.HasValue
                && profile.UpdatedAt.Value < profile.CreatedAt.Value)
            {
                issues.Add(new CareerProfileValidationIssue(
                    CareerProfileValidationError.UpdatedBeforeCreated,
                    "updated_at"));
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            ValidateItems(profile.Links, "links", item => item?.Id, ids, issues);
            ValidateItems(profile.Skills, "skills", item => item?.Id, ids, issues);
            ValidateItems(profile.Experiences, "experiences", item => item?.Id, ids, issues);
            ValidateItems(profile.Projects, "projects", item => item?.Id, ids, issues);
            ValidateItems(profile.Educations, "educations", item => item?.Id, ids, issues);
            ValidateItems(profile.Languages, "languages", item => item?.Id, ids, issues);
            ValidatePreferences(profile.JobPreferences, issues);
            return issues.AsReadOnly();
        }

        private static void ValidatePreferences(
            JobSearchPreferences preferences,
            ICollection<CareerProfileValidationIssue> issues)
        {
            if (preferences == null)
            {
                issues.Add(new CareerProfileValidationIssue(
                    CareerProfileValidationError.NullCollection,
                    "job_preferences"));
                return;
            }

            ValidatePreferenceValues(preferences.TargetRoles, "job_preferences.target_roles", 5, issues);
            ValidatePreferenceValues(preferences.Industries, "job_preferences.industries", 5, issues);
            ValidatePreferenceValues(preferences.AcceptedRegions,
                "job_preferences.accepted_regions", 8, issues);
            ValidatePreferenceValues(preferences.EmploymentTypes,
                "job_preferences.employment_types", 4, issues);
            ValidatePreferenceValues(preferences.WorkModes, "job_preferences.work_modes", 3, issues);
            ValidatePreferenceValues(preferences.WorkSchedules,
                "job_preferences.work_schedules", 3, issues);

            if (preferences.MinimumSalary < 0 || preferences.DesiredSalary < 0
                || preferences.MaximumCommuteMinutes < 0)
            {
                issues.Add(new CareerProfileValidationIssue(
                    CareerProfileValidationError.NegativePreferenceNumber,
                    "job_preferences"));
            }
            if (preferences.MinimumSalary.HasValue && preferences.DesiredSalary.HasValue
                && preferences.DesiredSalary.Value < preferences.MinimumSalary.Value)
            {
                issues.Add(new CareerProfileValidationIssue(
                    CareerProfileValidationError.DesiredSalaryBelowMinimum,
                    "job_preferences.desired_salary"));
            }
        }

        private static void ValidatePreferenceValues(
            IList<string> values,
            string field,
            int limit,
            ICollection<CareerProfileValidationIssue> issues)
        {
            if (values == null)
            {
                issues.Add(new CareerProfileValidationIssue(
                    CareerProfileValidationError.NullCollection,
                    field));
                return;
            }
            if (values.Count > limit)
            {
                issues.Add(new CareerProfileValidationIssue(
                    CareerProfileValidationError.TooManyPreferenceValues,
                    field));
            }
            var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string value in values.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                if (!distinct.Add(value.Trim()))
                {
                    issues.Add(new CareerProfileValidationIssue(
                        CareerProfileValidationError.DuplicatePreferenceValue,
                        field));
                    break;
                }
            }
        }

        private static void ValidateItems<T>(
            IList<T> items,
            string field,
            Func<T, string> getId,
            ISet<string> ids,
            ICollection<CareerProfileValidationIssue> issues)
            where T : class
        {
            if (items == null)
            {
                issues.Add(new CareerProfileValidationIssue(
                    CareerProfileValidationError.NullCollection,
                    field));
                return;
            }

            for (int index = 0; index < items.Count; index++)
            {
                T item = items[index];
                string path = field + "[" + index + "]";
                if (item == null)
                {
                    issues.Add(new CareerProfileValidationIssue(
                        CareerProfileValidationError.NullItem,
                        path));
                    continue;
                }

                string id = getId(item);
                if (string.IsNullOrWhiteSpace(id))
                {
                    issues.Add(new CareerProfileValidationIssue(
                        CareerProfileValidationError.ItemIdMissing,
                        path + ".id"));
                }
                else if (!ids.Add(id))
                {
                    issues.Add(new CareerProfileValidationIssue(
                        CareerProfileValidationError.DuplicateItemId,
                        path + ".id"));
                }
            }
        }
    }
}
