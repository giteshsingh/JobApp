namespace JobApp.Core.Domain;

/// <summary>A single open role, normalised across every source.</summary>
public class Job
{
    public int Id { get; set; }

    public JobSourceKind Source { get; set; }

    /// <summary>Identifier from the upstream feed; null for pasted jobs.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Canonical apply/listing URL.</summary>
    public string? Url { get; set; }

    public string Title { get; set; } = "";
    public string Company { get; set; } = "";

    /// <summary>Location exactly as the source stated it.</summary>
    public string? LocationRaw { get; set; }

    public string? City { get; set; }
    public string? CountryCode { get; set; }

    /// <summary>Full job description, plain text.</summary>
    public string Description { get; set; } = "";

    public DateTimeOffset? PostedAt { get; set; }
    public DateTimeOffset DiscoveredAt { get; set; } = DateTimeOffset.UtcNow;

    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string? SalaryCurrency { get; set; }

    /// <summary>True when the figure is the board's guess, not the employer's.</summary>
    public bool SalaryIsEstimate { get; set; }

    // ---- Work model (the remote / 2-day-hybrid question) ----------------

    public WorkModel WorkModel { get; set; } = WorkModel.Unknown;

    /// <summary>Days per week in office when the JD actually states it.</summary>
    public int? OfficeDaysPerWeek { get; set; }

    /// <summary>0.0-1.0 confidence in <see cref="WorkModel"/>. Low values land in the "needs review" bucket.</summary>
    public double WorkModelConfidence { get; set; }

    public WorkModelOrigin WorkModelOrigin { get; set; } = WorkModelOrigin.None;

    /// <summary>Claude's quote from the JD that justified the classification.</summary>
    public string? WorkModelEvidence { get; set; }

    /// <summary>Whether the JD mentions visa sponsorship, when the profile asks for it.</summary>
    public bool? MentionsVisaSponsorship { get; set; }

    /// <summary>Years of experience the JD asks for, if stated.</summary>
    public int? RequiredYearsExperience { get; set; }

    // ---- Housekeeping ---------------------------------------------------

    /// <summary>Hash of company+title+location, used to collapse cross-board duplicates.</summary>
    public string DedupeKey { get; set; } = "";

    public bool IsArchived { get; set; }

    public List<JobScore> Scores { get; set; } = [];
    public JobApplication? Application { get; set; }
    public List<TailoringSession> Tailorings { get; set; } = [];
}
