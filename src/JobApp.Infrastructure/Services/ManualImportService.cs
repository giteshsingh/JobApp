namespace JobApp.Infrastructure.Services;

using JobApp.Core.Abstractions;
using JobApp.Core.Domain;
using JobApp.Core.Scoring;
using JobApp.Infrastructure.Ai;
using JobApp.Infrastructure.Data;
using JobApp.Infrastructure.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>Outcome of a paste-import, including the nudge to paste text instead.</summary>
public record ImportOutcome(Job? Job, bool AlreadyExisted, string? Message);

/// <summary>
/// The LinkedIn/Naukri path: you paste a job URL or the description text, and
/// this normalises it into the same shape as an API-sourced job.
/// </summary>
public class ManualImportService(
    JobAppDbContext db,
    IJobAnalyst analyst,
    IJobPageFetcher fetcher,
    SpendGuard spend,
    ILogger<ManualImportService> log)
{
    /// <summary>Fetches the URL, falling back to asking you for the text.</summary>
    public async Task<ImportOutcome> ImportFromUrlAsync(string url, CancellationToken ct)
    {
        var text = await fetcher.FetchReadableTextAsync(url, ct);

        if (string.IsNullOrWhiteSpace(text))
        {
            return new ImportOutcome(
                null,
                false,
                "Could not read that page — LinkedIn and Naukri usually block anonymous requests. "
                + "Open the posting, copy the job description, and use 'Paste description' instead.");
        }

        return await ImportAsync(text, url, JobSourceKind.PastedUrl, ct);
    }

    /// <summary>Imports a job description you pasted as raw text.</summary>
    public Task<ImportOutcome> ImportFromTextAsync(string text, string? url, CancellationToken ct)
        => ImportAsync(text, url, JobSourceKind.PastedText, ct);

    private async Task<ImportOutcome> ImportAsync(string rawText, string? url, JobSourceKind kind, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawText) || rawText.Trim().Length < 120)
        {
            return new ImportOutcome(null, false, "That text is too short to be a job description.");
        }

        if (!await spend.CanSpendAsync(ct))
        {
            return new ImportOutcome(null, false, "Monthly Claude budget reached — raise it in settings to import.");
        }

        var response = await analyst.ParseJobPostingAsync(rawText, url, ct);
        await spend.RecordAsync(AiPurpose.JobParsing, response.Usage, null, ct);
        var parsed = response.Value;

        var job = new Job
        {
            Source = kind,
            ExternalId = null,
            Url = url,
            Title = parsed.Title,
            Company = parsed.Company,
            LocationRaw = parsed.Location,
            City = parsed.City ?? SourceHelpers.CityFrom(parsed.Location),
            CountryCode = parsed.CountryCode,
            Description = string.IsNullOrWhiteSpace(parsed.Description) ? rawText.Trim() : parsed.Description,
            // Assume "now" only when the page gave us nothing — you just found it.
            PostedAt = parsed.PostedAt ?? DateTimeOffset.UtcNow,
            SalaryMin = parsed.SalaryMin,
            SalaryMax = parsed.SalaryMax,
            SalaryCurrency = parsed.SalaryCurrency,
        };

        SourceHelpers.Finish(job);

        var existing = await db.Jobs.FirstOrDefaultAsync(j => j.DedupeKey == job.DedupeKey, ct);
        if (existing is not null)
        {
            // Already known from a board — enrich rather than duplicate.
            var enriched = false;

            if (string.IsNullOrWhiteSpace(existing.Url) && !string.IsNullOrWhiteSpace(url))
            {
                existing.Url = url;
                enriched = true;
            }

            if (existing.Description.Length < job.Description.Length)
            {
                existing.Description = job.Description;
                enriched = true;
            }

            if (existing.IsArchived)
            {
                existing.IsArchived = false;
                enriched = true;
            }

            if (enriched)
            {
                await db.SaveChangesAsync(ct);
            }

            return new ImportOutcome(existing, true, "You already had this job — updated the existing card.");
        }

        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);

        log.LogInformation("Imported '{Title}' at {Company} from {Kind}", job.Title, job.Company, kind);
        return new ImportOutcome(job, false, null);
    }
}
