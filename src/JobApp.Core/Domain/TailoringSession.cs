namespace JobApp.Core.Domain;

/// <summary>
/// One "customise my resume for this JD" run. Holds the suggestions so you can
/// accept or reject each one before exporting.
/// </summary>
public class TailoringSession
{
    public int Id { get; set; }

    public int JobId { get; set; }
    public Job Job { get; set; } = null!;

    public int ResumeDocId { get; set; }
    public ResumeDoc ResumeDoc { get; set; } = null!;

    public TailoringStatus Status { get; set; } = TailoringStatus.Pending;

    public string? Error { get; set; }

    /// <summary>Rewritten professional summary, targeted at this JD.</summary>
    public string? TailoredSummary { get; set; }

    /// <summary>Suggested skills line, reordered to lead with what the JD asks for.</summary>
    public string? TailoredSkillsLine { get; set; }

    /// <summary>Keywords worth adding for ATS/recruiter skim, with honesty caveats applied.</summary>
    public List<string> KeywordsToAdd { get; set; } = [];

    /// <summary>Claude's note on what it deliberately did not claim on your behalf.</summary>
    public string? HonestyNotes { get; set; }

    public string? CoverLetterDraft { get; set; }

    /// <summary>Per-bullet edits, each independently acceptable.</summary>
    public List<ResumeSuggestion> Suggestions { get; set; } = [];

    public string? ModelUsed { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>A single proposed edit to one resume bullet or line.</summary>
public class ResumeSuggestion
{
    public int Id { get; set; }

    public int TailoringSessionId { get; set; }
    public TailoringSession TailoringSession { get; set; } = null!;

    public ResumeSectionKind Section { get; set; }

    /// <summary>Heading the bullet lives under, so the UI can group the diff.</summary>
    public string? SectionHeading { get; set; }

    /// <summary>Empty when the suggestion is a brand-new bullet.</summary>
    public string OriginalText { get; set; } = "";

    public string SuggestedText { get; set; } = "";

    /// <summary>Why this change helps against this specific JD.</summary>
    public string? Rationale { get; set; }

    public List<string> KeywordsAdded { get; set; } = [];

    /// <summary>Null until you decide; true/false once you accept or reject.</summary>
    public bool? Accepted { get; set; }

    /// <summary>Your own override, used verbatim when present.</summary>
    public string? UserEditedText { get; set; }

    public int Order { get; set; }
}
