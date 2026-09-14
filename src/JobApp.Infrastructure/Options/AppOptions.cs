namespace JobApp.Infrastructure.Options;

/// <summary>Claude wiring. Keys come from environment/user-secrets, never appsettings.</summary>
public class ClaudeOptions
{
    public const string Section = "Claude";

    /// <summary>Falls back to the ANTHROPIC_API_KEY environment variable.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Cheap model for classifying and scoring every new job.</summary>
    public string TriageModel { get; set; } = "claude-haiku-4-5";

    /// <summary>
    /// Stronger model for deep fit reads and resume tailoring. Sonnet 5 by
    /// choice — it is roughly 5x cheaper than Opus per tailoring run.
    /// </summary>
    public string TailoringModel { get; set; } = "claude-sonnet-5";

    public int TriageMaxTokens { get; set; } = 2_000;

    public int TailoringMaxTokens { get; set; } = 16_000;

    /// <summary>USD ceiling per calendar month. Null or 0 disables the cap.</summary>
    public decimal? MonthlyBudgetUsd { get; set; } = 20m;

    /// <summary>How many jobs the worker triages per cycle, to bound spend.</summary>
    public int MaxTriagePerCycle { get; set; } = 40;
}

/// <summary>Job board credentials and toggles.</summary>
public class JobSourceOptions
{
    public const string Section = "JobSources";

    public AdzunaOptions Adzuna { get; set; } = new();

    public JSearchOptions JSearch { get; set; } = new();

    /// <summary>Free, no-key remote boards.</summary>
    public bool EnableRemotive { get; set; } = true;

    public bool EnableArbeitnow { get; set; } = true;

    /// <summary>Max postings pulled per source per profile per cycle.</summary>
    public int MaxResultsPerSource { get; set; } = 50;
}

/// <summary>https://developer.adzuna.com — free tier, needs app id + key.</summary>
public class AdzunaOptions
{
    public string? AppId { get; set; }

    public string? AppKey { get; set; }
}

/// <summary>JSearch on RapidAPI — aggregates Google Jobs, LinkedIn, Indeed.</summary>
public class JSearchOptions
{
    public string? RapidApiKey { get; set; }

    public string Host { get; set; } = "jsearch.p.rapidapi.com";
}

/// <summary>Background polling cadence.</summary>
public class IngestionOptions
{
    public const string Section = "Ingestion";

    public bool Enabled { get; set; } = true;

    public int PollIntervalMinutes { get; set; } = 60;

    /// <summary>Wait before the first poll so the API is up and reachable.</summary>
    public int StartupDelaySeconds { get; set; } = 20;

    /// <summary>Archive jobs untouched and unapplied for this long.</summary>
    public int ArchiveAfterDays { get; set; } = 45;
}
