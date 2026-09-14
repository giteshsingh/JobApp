namespace JobApp.Core.Domain;

/// <summary>
/// Everything about *what* to look for. Fully user-editable from the dashboard —
/// nothing here is hardcoded in the backend.
/// </summary>
public class SearchProfile
{
    public int Id { get; set; }

    public string Name { get; set; } = "Default";

    /// <summary>Only active profiles are polled by the background worker.</summary>
    public bool IsActive { get; set; } = true;

    // ---- What roles ----------------------------------------------------

    /// <summary>Titles fed to the job-board queries, e.g. "Project Lead", "Engineering Manager".</summary>
    public List<string> TargetTitles { get; set; } = [];

    /// <summary>Titles/phrases that disqualify a job outright, e.g. "intern", "PHP".</summary>
    public List<string> ExcludeKeywords { get; set; } = [];

    /// <summary>Skills a job must mention to clear the bar.</summary>
    public List<string> MustHaveSkills { get; set; } = [];

    /// <summary>Skills that add score but are not required.</summary>
    public List<string> NiceToHaveSkills { get; set; } = [];

    public int? MinYearsExperience { get; set; }
    public int? MaxYearsExperience { get; set; }

    // ---- Where ---------------------------------------------------------

    /// <summary>Adzuna/JSearch country codes, e.g. "in", "gb", "us".</summary>
    public List<string> CountryCodes { get; set; } = [];

    /// <summary>City you live in; drives the commute-friendliness score.</summary>
    public string? HomeCity { get; set; }

    /// <summary>Other cities you would work in (onsite/hybrid).</summary>
    public List<string> AcceptableCities { get; set; } = [];

    /// <summary>Include roles anywhere in the world if they are fully remote.</summary>
    public bool AllowGlobalRemote { get; set; } = true;

    /// <summary>Surface roles that would require moving country.</summary>
    public bool OpenToRelocation { get; set; }

    /// <summary>Ask Claude to flag whether the JD mentions visa sponsorship.</summary>
    public bool TrackVisaSponsorship { get; set; }

    // ---- Work model preference ------------------------------------------

    /// <summary>Score weight 0-100 for a fully remote role.</summary>
    public int RemoteWeight { get; set; } = 100;

    /// <summary>Score weight 0-100 for hybrid within <see cref="MaxOfficeDaysPerWeek"/>.</summary>
    public int HybridWeight { get; set; } = 90;

    /// <summary>Score weight 0-100 for hybrid that exceeds the office-day tolerance.</summary>
    public int HybridOverLimitWeight { get; set; } = 45;

    /// <summary>Score weight 0-100 for full-time onsite.</summary>
    public int OnsiteWeight { get; set; } = 25;

    /// <summary>Your hybrid tolerance. Defaults to the 2-day week you asked for.</summary>
    public int MaxOfficeDaysPerWeek { get; set; } = 2;

    /// <summary>Hide anything Claude classified as Onsite.</summary>
    public bool ExcludeOnsite { get; set; }

    // ---- Money ---------------------------------------------------------

    /// <summary>Annual floor in <see cref="SalaryCurrency"/>. Null disables the filter.</summary>
    public decimal? MinSalary { get; set; }

    public string SalaryCurrency { get; set; } = "INR";

    /// <summary>
    /// Most Indian listings hide salary; keeping this true stops a floor from
    /// silently discarding good jobs.
    /// </summary>
    public bool IncludeUndisclosedSalary { get; set; } = true;

    // ---- Freshness / scoring -------------------------------------------

    /// <summary>The "most recent" window for the dashboard. Defaults to 7 days.</summary>
    public int PostedWithinDays { get; set; } = 7;

    /// <summary>Jobs scoring below this are collapsed into a low-match bucket.</summary>
    public int MinOverallScore { get; set; } = 55;

    /// <summary>Relative weight of Claude's skill/seniority judgement in the blended score.</summary>
    public int SkillFitWeight { get; set; } = 55;

    /// <summary>Relative weight of the deterministic location/work-model score.</summary>
    public int LocationFitWeight { get; set; } = 30;

    /// <summary>Relative weight of recency.</summary>
    public int FreshnessWeight { get; set; } = 15;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<JobScore> Scores { get; set; } = [];
}
