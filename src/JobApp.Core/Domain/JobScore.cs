namespace JobApp.Core.Domain;

/// <summary>
/// How well one job fits one search profile. Blends a deterministic
/// location/freshness score with Claude's skill and seniority judgement.
/// </summary>
public class JobScore
{
    public int Id { get; set; }

    public int JobId { get; set; }
    public Job Job { get; set; } = null!;

    public int SearchProfileId { get; set; }
    public SearchProfile SearchProfile { get; set; } = null!;

    /// <summary>0-100 blended headline number the dashboard sorts on.</summary>
    public int OverallScore { get; set; }

    /// <summary>0-100 from Claude: how well your skills line up.</summary>
    public int SkillScore { get; set; }

    /// <summary>0-100 from Claude: seniority / years-of-experience fit.</summary>
    public int SeniorityScore { get; set; }

    /// <summary>0-100 deterministic: city + work-model preference.</summary>
    public int LocationScore { get; set; }

    /// <summary>0-100 deterministic: decays over <see cref="SearchProfile.PostedWithinDays"/>.</summary>
    public int FreshnessScore { get; set; }

    /// <summary>0-100 deterministic: salary vs floor. 50 when undisclosed.</summary>
    public int SalaryScore { get; set; }

    public List<string> MatchedSkills { get; set; } = [];

    /// <summary>Profile must-haves the JD does not mention.</summary>
    public List<string> MissingMustHaves { get; set; } = [];

    /// <summary>JD keywords absent from your resume — the ATS gap.</summary>
    public List<string> MissingResumeKeywords { get; set; } = [];

    public List<string> RedFlags { get; set; } = [];

    /// <summary>Two or three sentences on why this scored the way it did.</summary>
    public string? Rationale { get; set; }

    /// <summary>True once the pricier Opus deep-fit pass has run.</summary>
    public bool IsDeepAnalysis { get; set; }

    public string? ModelUsed { get; set; }
    public DateTimeOffset ScoredAt { get; set; } = DateTimeOffset.UtcNow;
}
