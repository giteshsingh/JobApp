namespace JobApp.Core.Domain;

/// <summary>Kanban card tracking a job you decided to pursue.</summary>
public class JobApplication
{
    public int Id { get; set; }

    public int JobId { get; set; }
    public Job Job { get; set; } = null!;

    public ApplicationStage Stage { get; set; } = ApplicationStage.Saved;

    /// <summary>Manual ordering within a kanban column.</summary>
    public int SortOrder { get; set; }

    public DateTimeOffset? AppliedAt { get; set; }

    /// <summary>Which tailored resume you actually sent.</summary>
    public int? ResumeDocId { get; set; }
    public ResumeDoc? ResumeDoc { get; set; }

    public string? Notes { get; set; }

    /// <summary>Next thing you owe them, e.g. "send take-home".</summary>
    public string? NextAction { get; set; }
    public DateTimeOffset? NextActionAt { get; set; }

    public List<StageChange> History { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Audit row so the pipeline shows how long each stage took.</summary>
public class StageChange
{
    public int Id { get; set; }

    public int JobApplicationId { get; set; }
    public JobApplication JobApplication { get; set; } = null!;

    public ApplicationStage From { get; set; }
    public ApplicationStage To { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
