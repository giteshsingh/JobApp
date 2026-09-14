namespace JobApp.Core.Domain;

/// <summary>
/// One Claude API call, priced. Backs the spend dashboard and the optional
/// monthly cap that pauses background triage.
/// </summary>
public class AiUsageRecord
{
    public int Id { get; set; }

    public AiPurpose Purpose { get; set; }

    public string Model { get; set; } = "";

    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long CacheReadTokens { get; set; }
    public long CacheWriteTokens { get; set; }

    /// <summary>Cost in USD, computed from the rate table at call time.</summary>
    public decimal CostUsd { get; set; }

    /// <summary>Job this call was about, when applicable.</summary>
    public int? JobId { get; set; }

    public bool Succeeded { get; set; } = true;
    public string? Error { get; set; }

    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
