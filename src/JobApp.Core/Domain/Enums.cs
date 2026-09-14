namespace JobApp.Core.Domain;

/// <summary>Where a job record came from.</summary>
public enum JobSourceKind
{
    Unknown = 0,
    Adzuna = 1,
    JSearch = 2,
    Remotive = 3,
    Arbeitnow = 4,
    /// <summary>Pasted LinkedIn/Naukri URL, parsed server-side.</summary>
    PastedUrl = 10,
    /// <summary>Job description pasted as raw text.</summary>
    PastedText = 11,
}

/// <summary>How much time the role requires in an office.</summary>
public enum WorkModel
{
    Unknown = 0,
    Remote = 1,
    Hybrid = 2,
    Onsite = 3,
}

/// <summary>Who decided the <see cref="WorkModel"/>.</summary>
public enum WorkModelOrigin
{
    /// <summary>Not yet classified.</summary>
    None = 0,
    /// <summary>Structured field supplied by the upstream API.</summary>
    SourceFeed = 1,
    /// <summary>Deterministic keyword rules.</summary>
    Rules = 2,
    /// <summary>Inferred by Claude from the job description.</summary>
    Ai = 3,
    /// <summary>Corrected by the user in the dashboard.</summary>
    Manual = 4,
}

/// <summary>Kanban stage for a job you are pursuing.</summary>
public enum ApplicationStage
{
    Saved = 0,
    Applied = 1,
    Screen = 2,
    Interview = 3,
    Offer = 4,
    Rejected = 5,
    Dismissed = 6,
}

/// <summary>Why an AI call was made, for cost attribution.</summary>
public enum AiPurpose
{
    Triage = 0,
    DeepFit = 1,
    ResumeTailoring = 2,
    CoverLetter = 3,
    JobParsing = 4,
    ResumeParsing = 5,
}

/// <summary>Lifecycle of a tailoring run.</summary>
public enum TailoringStatus
{
    Pending = 0,
    Ready = 1,
    Failed = 2,
}

/// <summary>Which resume section a suggestion targets.</summary>
public enum ResumeSectionKind
{
    Summary = 0,
    Skills = 1,
    Experience = 2,
    Projects = 3,
    Education = 4,
    Other = 5,
}
