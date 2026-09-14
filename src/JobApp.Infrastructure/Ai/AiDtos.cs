namespace JobApp.Infrastructure.Ai;

/// <summary>Wire shapes matching <see cref="AiSchemas"/>. Deserialised with web (camelCase) options.</summary>
internal static class AiDtos
{
    internal record TriageDto(
        string? WorkModel,
        int? OfficeDaysPerWeek,
        double WorkModelConfidence,
        string? WorkModelEvidence,
        int SkillScore,
        int SeniorityScore,
        int? RequiredYearsExperience,
        bool? MentionsVisaSponsorship,
        List<string>? MatchedSkills,
        List<string>? MissingMustHaves,
        List<string>? MissingResumeKeywords,
        List<string>? RedFlags,
        string? Rationale);

    internal record SuggestionDto(
        string? Section,
        string? SectionHeading,
        string? OriginalText,
        string? SuggestedText,
        string? Rationale,
        List<string>? KeywordsAdded);

    internal record TailoringDto(
        string? TailoredSummary,
        string? TailoredSkillsLine,
        List<string>? KeywordsToAdd,
        string? HonestyNotes,
        string? CoverLetterDraft,
        List<SuggestionDto>? Suggestions);

    internal record JobParsingDto(
        string? Title,
        string? Company,
        string? Location,
        string? City,
        string? CountryCode,
        string? Description,
        string? PostedAt,
        int? PostedDaysAgo,
        decimal? SalaryMin,
        decimal? SalaryMax,
        string? SalaryCurrency);
}
