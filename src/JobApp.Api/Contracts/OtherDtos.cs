namespace JobApp.Api.Contracts;

using JobApp.Core.Domain;

// ---- Jobs ---------------------------------------------------------------

public record ImportUrlRequest(string Url);

public record ImportTextRequest(string Text, string? Url);

public record SetWorkModelRequest(WorkModel WorkModel, int? OfficeDaysPerWeek);

// ---- Resumes --------------------------------------------------------------

public record ResumeSummaryDto(
    int Id,
    string Name,
    bool IsMaster,
    bool IsCoverLetter,
    int SectionCount,
    List<string> ExtractedSkills,
    int? YearsExperience,
    int? DerivedFromTailoringId,
    DateTimeOffset UpdatedAt)
{
    public static ResumeSummaryDto From(ResumeDoc r) => new(
        r.Id, r.Name, r.IsMaster, r.IsCoverLetter, r.Sections.Count,
        r.ExtractedSkills, r.YearsExperience, r.DerivedFromTailoringId, r.UpdatedAt);
}

public record ResumeSectionDto(string Kind, string Heading, string? Subject, string? DateRange, List<string> Bullets);

public record ResumeDetailDto(
    int Id, string Name, bool IsMaster, bool IsCoverLetter,
    List<ResumeSectionDto> Sections, List<string> ExtractedSkills, int? YearsExperience)
{
    public static ResumeDetailDto From(ResumeDoc r) => new(
        r.Id, r.Name, r.IsMaster, r.IsCoverLetter,
        r.Sections.OrderBy(s => s.Order)
            .Select(s => new ResumeSectionDto(s.Kind.ToString(), s.Heading, s.Subject, s.DateRange, s.Bullets))
            .ToList(),
        r.ExtractedSkills, r.YearsExperience);
}

// ---- Tailoring --------------------------------------------------------------

public record CreateTailoringRequest(int JobId, int? ResumeId, bool IncludeCoverLetter);

public record SuggestionDto(
    int Id, string Section, string? SectionHeading, string OriginalText, string SuggestedText,
    string? Rationale, List<string> KeywordsAdded, bool? Accepted, string? UserEditedText)
{
    public static SuggestionDto From(ResumeSuggestion s) => new(
        s.Id, s.Section.ToString(), s.SectionHeading, s.OriginalText, s.SuggestedText,
        s.Rationale, s.KeywordsAdded, s.Accepted, s.UserEditedText);
}

public record TailoringSessionDto(
    int Id, int JobId, int ResumeDocId, string Status, string? Error,
    string? TailoredSummary, string? TailoredSkillsLine, List<string> KeywordsToAdd,
    string? HonestyNotes, string? CoverLetterDraft, List<SuggestionDto> Suggestions,
    string? ModelUsed, DateTimeOffset CreatedAt)
{
    public static TailoringSessionDto From(TailoringSession s) => new(
        s.Id, s.JobId, s.ResumeDocId, s.Status.ToString(), s.Error,
        s.TailoredSummary, s.TailoredSkillsLine, s.KeywordsToAdd,
        s.HonestyNotes, s.CoverLetterDraft,
        s.Suggestions.OrderBy(x => x.Order).Select(SuggestionDto.From).ToList(),
        s.ModelUsed, s.CreatedAt);
}

public record DecideSuggestionRequest(bool? Accepted, string? EditedText);

// ---- Pipeline --------------------------------------------------------------

public record TrackJobRequest(ApplicationStage Stage = ApplicationStage.Saved);

public record MoveCardRequest(ApplicationStage Stage, int? SortOrder, string? Note);

public record UpdateCardRequest(string? Notes, string? NextAction, DateTimeOffset? NextActionAt, int? ResumeDocId);

// ---- Ingestion / scoring --------------------------------------------------------------

public record DeepAnalyseRequest(int ProfileId);

public record IngestionReportDto(
    int Fetched, int Inserted, int Duplicates, int Excluded, int TooOld,
    Dictionary<string, int> BySource, List<string> Warnings);

public record TriageReportDto(int Scored, int Failed, int Skipped, decimal CostUsd, List<string> Warnings);
