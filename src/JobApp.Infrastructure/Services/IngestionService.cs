namespace JobApp.Infrastructure.Services;

using JobApp.Core.Abstractions;
using JobApp.Core.Domain;
using JobApp.Core.Scoring;
using JobApp.Infrastructure.Data;
using JobApp.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>Result of one ingestion pass, surfaced to the dashboard.</summary>
public record IngestionReport(
    int Fetched,
    int Inserted,
    int Duplicates,
    int Excluded,
    int TooOld,
    Dictionary<string, int> BySource,
    List<string> Warnings);

/// <summary>
/// Pulls from every configured source, collapses duplicates, and stores what's
/// new. Scoring is deliberately a separate pass so a fetch failure never leaves
/// half-scored data.
/// </summary>
public class IngestionService(
    JobAppDbContext db,
    IEnumerable<IJobSource> sources,
    IOptions<IngestionOptions> ingestionOptions,
    ILogger<IngestionService> log)
{
    private readonly IngestionOptions opts = ingestionOptions.Value;

    /// <summary>Runs every active profile through every configured source.</summary>
    public async Task<IngestionReport> IngestAllAsync(CancellationToken ct)
    {
        var profiles = await db.SearchProfiles.Where(p => p.IsActive).ToListAsync(ct);
        if (profiles.Count == 0)
        {
            return new IngestionReport(0, 0, 0, 0, 0, [], ["No active search profile configured."]);
        }

        var fetched = 0;
        var inserted = 0;
        var duplicates = 0;
        var excluded = 0;
        var tooOld = 0;
        var bySource = new Dictionary<string, int>();
        var warnings = new List<string>();

        foreach (var profile in profiles)
        {
            foreach (var source in sources)
            {
                ct.ThrowIfCancellationRequested();

                if (!source.IsConfigured)
                {
                    warnings.Add($"{source.DisplayName} skipped — not configured.");
                    continue;
                }

                IReadOnlyList<Job> batch;
                try
                {
                    batch = await source.FetchAsync(profile, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "{Source} fetch threw", source.DisplayName);
                    warnings.Add($"{source.DisplayName} failed: {ex.Message}");
                    continue;
                }

                fetched += batch.Count;
                bySource[source.DisplayName] = bySource.GetValueOrDefault(source.DisplayName) + batch.Count;

                var (ins, dup, exc, old) = await StoreAsync(batch, profile, ct);
                inserted += ins;
                duplicates += dup;
                excluded += exc;
                tooOld += old;
            }
        }

        log.LogInformation(
            "Ingestion: fetched {Fetched}, inserted {Inserted}, duplicates {Dup}, excluded {Exc}, stale {Old}",
            fetched, inserted, duplicates, excluded, tooOld);

        return new IngestionReport(fetched, inserted, duplicates, excluded, tooOld, bySource, warnings);
    }

    /// <summary>Filters and inserts one source's batch.</summary>
    private async Task<(int Inserted, int Duplicates, int Excluded, int TooOld)> StoreAsync(
        IReadOnlyList<Job> batch, SearchProfile profile, CancellationToken ct)
    {
        if (batch.Count == 0)
        {
            return (0, 0, 0, 0);
        }

        var inserted = 0;
        var duplicates = 0;
        var excluded = 0;
        var tooOld = 0;

        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Clamp(profile.PostedWithinDays, 1, 365));

        // One round-trip for the whole batch instead of per-job existence checks.
        var keys = batch.Select(j => j.DedupeKey).Distinct().ToList();
        var existing = await db.Jobs
            .Where(j => keys.Contains(j.DedupeKey))
            .Select(j => j.DedupeKey)
            .ToListAsync(ct);

        var seen = new HashSet<string>(existing, StringComparer.Ordinal);

        foreach (var job in batch)
        {
            // Boards return the same role twice within one response, too.
            if (!seen.Add(job.DedupeKey))
            {
                duplicates++;
                continue;
            }

            if (job.PostedAt is not null && job.PostedAt < cutoff)
            {
                tooOld++;
                continue;
            }

            if (MatchScorer.IsExcluded(job, profile, out var keyword))
            {
                log.LogDebug("Excluded '{Title}' at {Company} on keyword '{Keyword}'", job.Title, job.Company, keyword);
                excluded++;
                continue;
            }

            if (profile.ExcludeOnsite && job.WorkModel == WorkModel.Onsite)
            {
                excluded++;
                continue;
            }

            db.Jobs.Add(job);
            inserted++;
        }

        if (inserted > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return (inserted, duplicates, excluded, tooOld);
    }

    /// <summary>
    /// Archives stale jobs you never engaged with, so the board stays readable.
    /// Anything on the kanban is left alone regardless of age.
    /// </summary>
    public async Task<int> ArchiveStaleAsync(CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, opts.ArchiveAfterDays));

        // A navigation-null check compiles to a LEFT JOIN, which SQLite's
        // ExecuteUpdate cannot translate. An Any() anti-join (NOT EXISTS) does.
        var count = await db.Jobs
            .Where(j => !j.IsArchived
                        && j.DiscoveredAt < cutoff
                        && !db.Applications.Any(a => a.JobId == j.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.IsArchived, true), ct);

        if (count > 0)
        {
            log.LogInformation("Archived {Count} stale jobs", count);
        }

        return count;
    }
}
