namespace JobApp.Core.Domain;

/// <summary>A resume or cover letter you own. One is flagged as the master.</summary>
public class ResumeDoc
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    /// <summary>The baseline everything gets tailored from.</summary>
    public bool IsMaster { get; set; }

    /// <summary>True for cover letters, so they are offered separately.</summary>
    public bool IsCoverLetter { get; set; }

    /// <summary>Where it was imported from, for reference.</summary>
    public string? SourceFilePath { get; set; }

    /// <summary>Flattened text, used for keyword-gap analysis and prompts.</summary>
    public string PlainText { get; set; } = "";

    /// <summary>Parsed structure so tailoring can target individual bullets.</summary>
    public List<ResumeSection> Sections { get; set; } = [];

    /// <summary>Skills lifted out of the resume, used as the profile seed.</summary>
    public List<string> ExtractedSkills { get; set; } = [];

    public int? YearsExperience { get; set; }

    /// <summary>Set when this document was produced by a tailoring run.</summary>
    public int? DerivedFromTailoringId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>One heading of a resume plus its bullets.</summary>
public class ResumeSection
{
    public ResumeSectionKind Kind { get; set; }

    /// <summary>Heading as written, e.g. "TECHNICAL SKILLS".</summary>
    public string Heading { get; set; } = "";

    /// <summary>Employer / project / institution, when the section has one.</summary>
    public string? Subject { get; set; }

    public string? DateRange { get; set; }

    public List<string> Bullets { get; set; } = [];

    /// <summary>Position in the document, so rendering keeps your original order.</summary>
    public int Order { get; set; }
}
