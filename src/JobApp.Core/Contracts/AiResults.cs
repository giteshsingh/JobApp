namespace JobApp.Core.Contracts;

using JobApp.Core.Domain;

/// <summary>Cheap first-pass classification + score for a single job.</summary>
public record TriageResult(
    WorkModel WorkModel,
    int? OfficeDaysPerWeek,
    double WorkModelConfidence,
    string? WorkModelEvidence,
    int SkillScore,
    int SeniorityScore,
    int? RequiredYearsExperience,
    bool? MentionsVisaSponsorship,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingMustHaves,
    IReadOnlyList<string> MissingResumeKeywords,
    IReadOnlyList<string> RedFlags,
    string Rationale);

/// <summary>A proposed edit to one resume line, before the user rules on it.</summary>
public record SuggestionDraft(
    ResumeSectionKind Section,
    string? SectionHeading,
    string OriginalText,
    string SuggestedText,
    string? Rationale,
    IReadOnlyList<string> KeywordsAdded);

/// <summary>Everything Claude produced for a "tailor my resume to this JD" run.</summary>
public record TailoringResult(
    string? TailoredSummary,
    string? TailoredSkillsLine,
    IReadOnlyList<string> KeywordsToAdd,
    string? HonestyNotes,
    string? CoverLetterDraft,
    IReadOnlyList<SuggestionDraft> Suggestions);

/// <summary>Normalised job fields recovered from pasted text or a fetched page.</summary>
public record ParsedJobPosting(
    string Title,
    string Company,
    string? Location,
    string? City,
    string? CountryCode,
    string Description,
    DateTimeOffset? PostedAt,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? SalaryCurrency);

/// <summary>Token accounting for one API call.</summary>
public record AiCallUsage(
    string Model,
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    decimal CostUsd);

/// <summary>An AI result plus what it cost to obtain.</summary>
public record AiResponse<T>(T Value, AiCallUsage Usage);
