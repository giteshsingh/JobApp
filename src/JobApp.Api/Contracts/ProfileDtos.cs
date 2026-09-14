namespace JobApp.Api.Contracts;

using JobApp.Core.Domain;

/// <summary>Full search-profile shape, read and write. Every field is user-editable.</summary>
public record SearchProfileDto(
    int Id,
    string Name,
    bool IsActive,
    List<string> TargetTitles,
    List<string> ExcludeKeywords,
    List<string> MustHaveSkills,
    List<string> NiceToHaveSkills,
    int? MinYearsExperience,
    int? MaxYearsExperience,
    List<string> CountryCodes,
    string? HomeCity,
    List<string> AcceptableCities,
    bool AllowGlobalRemote,
    bool OpenToRelocation,
    bool TrackVisaSponsorship,
    int RemoteWeight,
    int HybridWeight,
    int HybridOverLimitWeight,
    int OnsiteWeight,
    int MaxOfficeDaysPerWeek,
    bool ExcludeOnsite,
    decimal? MinSalary,
    string SalaryCurrency,
    bool IncludeUndisclosedSalary,
    int PostedWithinDays,
    int MinOverallScore,
    int SkillFitWeight,
    int LocationFitWeight,
    int FreshnessWeight)
{
    public static SearchProfileDto From(SearchProfile p) => new(
        p.Id, p.Name, p.IsActive,
        p.TargetTitles, p.ExcludeKeywords, p.MustHaveSkills, p.NiceToHaveSkills,
        p.MinYearsExperience, p.MaxYearsExperience,
        p.CountryCodes, p.HomeCity, p.AcceptableCities, p.AllowGlobalRemote, p.OpenToRelocation, p.TrackVisaSponsorship,
        p.RemoteWeight, p.HybridWeight, p.HybridOverLimitWeight, p.OnsiteWeight, p.MaxOfficeDaysPerWeek, p.ExcludeOnsite,
        p.MinSalary, p.SalaryCurrency, p.IncludeUndisclosedSalary,
        p.PostedWithinDays, p.MinOverallScore, p.SkillFitWeight, p.LocationFitWeight, p.FreshnessWeight);

    /// <summary>Copies every editable field onto an existing entity.</summary>
    public void ApplyTo(SearchProfile p)
    {
        p.Name = string.IsNullOrWhiteSpace(Name) ? p.Name : Name.Trim();
        p.IsActive = IsActive;
        p.TargetTitles = Clean(TargetTitles);
        p.ExcludeKeywords = Clean(ExcludeKeywords);
        p.MustHaveSkills = Clean(MustHaveSkills);
        p.NiceToHaveSkills = Clean(NiceToHaveSkills);
        p.MinYearsExperience = MinYearsExperience;
        p.MaxYearsExperience = MaxYearsExperience;
        p.CountryCodes = Clean(CountryCodes).Select(c => c.ToLowerInvariant()).ToList();
        p.HomeCity = string.IsNullOrWhiteSpace(HomeCity) ? null : HomeCity.Trim();
        p.AcceptableCities = Clean(AcceptableCities);
        p.AllowGlobalRemote = AllowGlobalRemote;
        p.OpenToRelocation = OpenToRelocation;
        p.TrackVisaSponsorship = TrackVisaSponsorship;
        p.RemoteWeight = Math.Clamp(RemoteWeight, 0, 100);
        p.HybridWeight = Math.Clamp(HybridWeight, 0, 100);
        p.HybridOverLimitWeight = Math.Clamp(HybridOverLimitWeight, 0, 100);
        p.OnsiteWeight = Math.Clamp(OnsiteWeight, 0, 100);
        p.MaxOfficeDaysPerWeek = Math.Clamp(MaxOfficeDaysPerWeek, 0, 5);
        p.ExcludeOnsite = ExcludeOnsite;
        p.MinSalary = MinSalary is > 0 ? MinSalary : null;
        p.SalaryCurrency = string.IsNullOrWhiteSpace(SalaryCurrency) ? p.SalaryCurrency : SalaryCurrency.ToUpperInvariant();
        p.IncludeUndisclosedSalary = IncludeUndisclosedSalary;
        p.PostedWithinDays = Math.Clamp(PostedWithinDays, 1, 90);
        p.MinOverallScore = Math.Clamp(MinOverallScore, 0, 100);
        p.SkillFitWeight = Math.Max(0, SkillFitWeight);
        p.LocationFitWeight = Math.Max(0, LocationFitWeight);
        p.FreshnessWeight = Math.Max(0, FreshnessWeight);
        p.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static List<string> Clean(List<string>? values)
        => (values ?? [])
            .Select(v => v.Trim())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
