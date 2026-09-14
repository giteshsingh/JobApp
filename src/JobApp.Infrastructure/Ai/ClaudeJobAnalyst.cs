namespace JobApp.Infrastructure.Ai;

using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using JobApp.Core.Abstractions;
using JobApp.Core.Contracts;
using JobApp.Core.Domain;
using JobApp.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CoreWorkModel = JobApp.Core.Domain.WorkModel;

/// <summary>
/// Claude-backed analysis. Triage runs on the cheap model over every new job;
/// deep fit and tailoring run on the expensive model only when you ask.
/// </summary>
public class ClaudeJobAnalyst : IJobAnalyst
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly AnthropicClient client;
    private readonly ClaudeOptions options;
    private readonly ClaudePricing pricing;
    private readonly ILogger<ClaudeJobAnalyst> log;

    public ClaudeJobAnalyst(
        IOptions<ClaudeOptions> options,
        IOptions<ClaudePricing> pricing,
        ILogger<ClaudeJobAnalyst> log)
    {
        this.options = options.Value;
        this.pricing = pricing.Value;
        this.log = log;

        var key = this.options.ApiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        client = string.IsNullOrWhiteSpace(key) ? new AnthropicClient() : new AnthropicClient { ApiKey = key };
    }

    /// <summary>False when no key is configured, so callers can degrade instead of throwing.</summary>
    public static bool HasApiKey(ClaudeOptions o)
        => !string.IsNullOrWhiteSpace(o.ApiKey)
           || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));

    public Task<AiResponse<TriageResult>> TriageAsync(Job job, SearchProfile profile, string resumeText, CancellationToken ct)
        => RunTriageAsync(job, profile, resumeText, deep: false, ct);

    public Task<AiResponse<TriageResult>> DeepFitAsync(Job job, SearchProfile profile, string resumeText, CancellationToken ct)
        => RunTriageAsync(job, profile, resumeText, deep: true, ct);

    private async Task<AiResponse<TriageResult>> RunTriageAsync(Job job, SearchProfile profile, string resumeText, bool deep, CancellationToken ct)
    {
        var model = deep ? options.TailoringModel : options.TriageModel;

        var request = new MessageCreateParams
        {
            Model = model,
            MaxTokens = deep ? Math.Max(options.TriageMaxTokens, 4_000) : options.TriageMaxTokens,

            // Cached: identical on every call in the cycle.
            System = new List<TextBlockParam>
            {
                new() { Text = Prompts.TriageSystem, CacheControl = new CacheControlEphemeral() },
            },
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = deep
                        ? Prompts.DeepFitUser(job, profile, resumeText)
                        : Prompts.TriageUser(job, profile, resumeText),
                },
            ],
            OutputConfig = new OutputConfig
            {
                // Effort only exists on the frontier models; the cheap triage
                // model rejects it, so it is set on the deep pass only.
                Effort = deep ? Effort.High : null,
                Format = new JsonOutputFormat { Schema = AiSchemas.Triage },
            },
        };

        var (dto, usage) = await SendAsync<AiDtos.TriageDto>(request, model, ct);

        var result = new TriageResult(
            ParseWorkModel(dto.WorkModel),
            NormaliseDays(dto.OfficeDaysPerWeek),
            Math.Clamp(dto.WorkModelConfidence, 0d, 1d),
            dto.WorkModelEvidence,
            Math.Clamp(dto.SkillScore, 0, 100),
            Math.Clamp(dto.SeniorityScore, 0, 100),
            dto.RequiredYearsExperience,
            dto.MentionsVisaSponsorship,
            dto.MatchedSkills ?? [],
            dto.MissingMustHaves ?? [],
            dto.MissingResumeKeywords ?? [],
            dto.RedFlags ?? [],
            dto.Rationale ?? "");

        return new AiResponse<TriageResult>(result, usage);
    }

    public async Task<AiResponse<TailoringResult>> TailorResumeAsync(Job job, ResumeDoc resume, bool includeCoverLetter, CancellationToken ct)
    {
        var model = options.TailoringModel;

        var request = new MessageCreateParams
        {
            Model = model,
            MaxTokens = options.TailoringMaxTokens,
            System = new List<TextBlockParam>
            {
                new() { Text = Prompts.TailoringSystem, CacheControl = new CacheControlEphemeral() },
            },
            Messages =
            [
                new() { Role = Role.User, Content = Prompts.TailoringUser(job, resume, includeCoverLetter) },
            ],
            OutputConfig = new OutputConfig
            {
                Effort = Effort.High,
                Format = new JsonOutputFormat { Schema = AiSchemas.Tailoring },
            },
        };

        var (dto, usage) = await SendAsync<AiDtos.TailoringDto>(request, model, ct);

        var suggestions = (dto.Suggestions ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s.SuggestedText))
            .Select((s, i) => new SuggestionDraft(
                ParseSection(s.Section),
                s.SectionHeading,
                s.OriginalText ?? "",
                s.SuggestedText!,
                s.Rationale,
                s.KeywordsAdded ?? []))
            .ToList();

        var result = new TailoringResult(
            dto.TailoredSummary,
            dto.TailoredSkillsLine,
            dto.KeywordsToAdd ?? [],
            dto.HonestyNotes,
            includeCoverLetter ? dto.CoverLetterDraft : null,
            suggestions);

        return new AiResponse<TailoringResult>(result, usage);
    }

    public async Task<AiResponse<ParsedJobPosting>> ParseJobPostingAsync(string rawText, string? sourceUrl, CancellationToken ct)
    {
        // Parsing is mechanical extraction — the cheap model handles it fine.
        var model = options.TriageModel;

        var request = new MessageCreateParams
        {
            Model = model,
            MaxTokens = 8_000,
            System = new List<TextBlockParam>
            {
                new() { Text = Prompts.JobParsingSystem, CacheControl = new CacheControlEphemeral() },
            },
            Messages =
            [
                new() { Role = Role.User, Content = Prompts.JobParsingUser(rawText, sourceUrl) },
            ],
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat { Schema = AiSchemas.JobParsing },
            },
        };

        var (dto, usage) = await SendAsync<AiDtos.JobParsingDto>(request, model, ct);

        var posted = ResolvePostedAt(dto.PostedAt, dto.PostedDaysAgo);

        var result = new ParsedJobPosting(
            string.IsNullOrWhiteSpace(dto.Title) ? "Unknown" : dto.Title!.Trim(),
            string.IsNullOrWhiteSpace(dto.Company) ? "Unknown" : dto.Company!.Trim(),
            dto.Location,
            dto.City,
            dto.CountryCode?.ToLowerInvariant(),
            dto.Description ?? "",
            posted,
            dto.SalaryMin,
            dto.SalaryMax,
            dto.SalaryCurrency);

        return new AiResponse<ParsedJobPosting>(result, usage);
    }

    /// <summary>Sends the request and deserialises the structured-output payload.</summary>
    private async Task<(T Value, AiCallUsage Usage)> SendAsync<T>(MessageCreateParams request, string model, CancellationToken ct)
    {
        var response = await client.Messages.Create(request, cancellationToken: ct);

        var usage = pricing.Usage(
            model,
            response.Usage?.InputTokens ?? 0,
            response.Usage?.OutputTokens ?? 0,
            response.Usage?.CacheReadInputTokens ?? 0,
            response.Usage?.CacheCreationInputTokens ?? 0);

        var text = string.Concat(response.Content
            .Select(b => b.Value)
            .OfType<TextBlock>()
            .Select(t => t.Text));

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException(
                $"{model} returned no text content (stop reason: {response.StopReason}).");
        }

        T? value;
        try
        {
            value = JsonSerializer.Deserialize<T>(text, Json);
        }
        catch (JsonException ex)
        {
            log.LogError(ex, "Could not parse {Model} structured output: {Snippet}", model, Snippet(text));
            throw new InvalidOperationException($"{model} returned unparseable JSON.", ex);
        }

        if (value is null)
        {
            throw new InvalidOperationException($"{model} returned null JSON: {Snippet(text)}");
        }

        return (value, usage);
    }

    private static string Snippet(string s) => s.Length <= 400 ? s : s[..400] + "...";

    private static CoreWorkModel ParseWorkModel(string? value)
        => Enum.TryParse<CoreWorkModel>(value, ignoreCase: true, out var parsed) ? parsed : CoreWorkModel.Unknown;

    private static ResumeSectionKind ParseSection(string? value)
        => Enum.TryParse<ResumeSectionKind>(value, ignoreCase: true, out var parsed) ? parsed : ResumeSectionKind.Other;

    /// <summary>Keeps only a sane 0-5 day count.</summary>
    private static int? NormaliseDays(int? days) => days is >= 0 and <= 5 ? days : null;

    /// <summary>Prefers an explicit date, falls back to "N days ago".</summary>
    private static DateTimeOffset? ResolvePostedAt(string? iso, int? daysAgo)
    {
        if (!string.IsNullOrWhiteSpace(iso)
            && DateTimeOffset.TryParse(iso, out var parsed))
        {
            return parsed;
        }

        if (daysAgo is >= 0 and <= 365)
        {
            return DateTimeOffset.UtcNow.AddDays(-daysAgo.Value);
        }

        return null;
    }
}
