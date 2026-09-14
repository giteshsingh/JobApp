namespace JobApp.Infrastructure.Services;

using JobApp.Core.Domain;
using JobApp.Infrastructure.Ai;
using JobApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

/// <summary>Filters the dashboard sends when listing jobs.</summary>
public record JobQuery
{
    public int? ProfileId { get; init; }

    /// <summary>Freshness window. Defaults to the profile's setting.</summary>
    public int? PostedWithinDays { get; init; }

    /// <summary>Remote / Hybrid / Onsite / Unknown, empty means all.</summary>
    public List<WorkModel> WorkModels { get; init; } = [];

    public int? MinScore { get; init; }

    /// <summary>Free-text over title, company and location.</summary>
    public string? Search { get; init; }

    /// <summary>Only jobs whose hybrid day count is within your tolerance.</summary>
    public bool WithinOfficeDayTolerance { get; init; }

    /// <summary>Only jobs the AI wasn't confident about, for manual review.</summary>
    public bool NeedsWorkModelReview { get; init; }

    public bool IncludeArchived { get; init; }

    /// <summary>Hide jobs already on the kanban.</summary>
    public bool HideTracked { get; init; }

    /// <summary>"score" (default), "posted" or "salary".</summary>
    public string Sort { get; init; } = "score";

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;
}

/// <summary>One row in the job list.</summary>
public record JobListItem(
    int Id,
    string Title,
    string Company,
    string? Location,
    string? City,
    string? Url,
    string Source,
    DateTimeOffset? PostedAt,
    DateTimeOffset DiscoveredAt,
    int? AgeDays,
    string WorkModel,
    int? OfficeDaysPerWeek,
    double WorkModelConfidence,
    string WorkModelOrigin,
    string? WorkModelEvidence,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? SalaryCurrency,
    bool SalaryIsEstimate,
    int? OverallScore,
    int? SkillScore,
    int? SeniorityScore,
    int? LocationScore,
    int? FreshnessScore,
    List<string> MatchedSkills,
    List<string> MissingMustHaves,
    List<string> MissingResumeKeywords,
    List<string> RedFlags,
    string? Rationale,
    bool IsDeepAnalysis,
    bool IsScored,
    bool IsArchived,
    string? Stage);

/// <summary>Paged job list plus the counts the header strip shows.</summary>
public record JobListResult(
    List<JobListItem> Items,
    int Total,
    int Page,
    int PageSize,
    DashboardCounts Counts);

/// <summary>Headline numbers for the holistic view.</summary>
public record DashboardCounts(
    int FreshTotal,
    int Remote,
    int HybridWithinTolerance,
    int HybridOverTolerance,
    int Onsite,
    int UnknownWorkModel,
    int StrongMatches,
    int Unscored,
    int NeedsWorkModelReview);

/// <summary>Everything the dashboard's overview panel needs.</summary>
public record DashboardSummary(
    DashboardCounts Counts,
    Dictionary<string, int> BySource,
    Dictionary<string, int> Pipeline,
    List<JobListItem> TopMatches,
    List<DailyCount> PostedPerDay,
    decimal SpendThisMonthUsd,
    decimal? MonthlyBudgetUsd,
    int TotalJobs,
    DateTimeOffset? LastIngestedAt);

/// <summary>Jobs discovered on one date, for the sparkline.</summary>
public record DailyCount(string Date, int Count);

/// <summary>Read-side queries. Kept separate from the write services.</summary>
public class DashboardService(JobAppDbContext db, SpendGuard spend)
{
    /// <summary>Paged, filtered job list for the main board.</summary>
    public async Task<JobListResult> QueryAsync(JobQuery query, CancellationToken ct)
    {
        var profile = await ResolveProfileAsync(query.ProfileId, ct);
        var window = query.PostedWithinDays ?? profile?.PostedWithinDays ?? 7;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Clamp(window, 1, 3650));
        var tolerance = profile?.MaxOfficeDaysPerWeek ?? 2;

        var jobs = db.Jobs.AsNoTracking().AsQueryable();

        if (!query.IncludeArchived)
        {
            jobs = jobs.Where(j => !j.IsArchived);
        }

        // Fall back to discovery time when a board didn't give us a posted date.
        jobs = jobs.Where(j => (j.PostedAt ?? j.DiscoveredAt) >= cutoff);

        if (query.WorkModels.Count > 0)
        {
            jobs = jobs.Where(j => query.WorkModels.Contains(j.WorkModel));
        }

        if (query.WithinOfficeDayTolerance)
        {
            jobs = jobs.Where(j => j.WorkModel == WorkModel.Remote
                                   || (j.WorkModel == WorkModel.Hybrid
                                       && (j.OfficeDaysPerWeek == null || j.OfficeDaysPerWeek <= tolerance)));
        }

        if (query.NeedsWorkModelReview)
        {
            jobs = jobs.Where(j => j.WorkModelOrigin != WorkModelOrigin.Manual
                                   && (j.WorkModel == WorkModel.Unknown || j.WorkModelConfidence < 0.7));
        }

        if (query.HideTracked)
        {
            jobs = jobs.Where(j => j.Application == null);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            jobs = jobs.Where(j => EF.Functions.Like(j.Title, $"%{term}%")
                                   || EF.Functions.Like(j.Company, $"%{term}%")
                                   || (j.LocationRaw != null && EF.Functions.Like(j.LocationRaw, $"%{term}%")));
        }

        // Left-join the score for the selected profile so unscored jobs still show.
        var rows = jobs.Select(j => new
        {
            Job = j,
            Score = profile == null
                ? null
                : j.Scores.FirstOrDefault(s => s.SearchProfileId == profile.Id),
            Stage = j.Application == null ? (ApplicationStage?)null : j.Application.Stage,
        });

        if (query.MinScore is int min)
        {
            rows = rows.Where(r => r.Score != null && r.Score.OverallScore >= min);
        }

        rows = query.Sort.ToLowerInvariant() switch
        {
            "posted" => rows.OrderByDescending(r => r.Job.PostedAt ?? r.Job.DiscoveredAt),
            "salary" => rows.OrderByDescending(r => r.Job.SalaryMax ?? r.Job.SalaryMin ?? 0m),
            // Unscored jobs sort last rather than as zero-score.
            _ => rows.OrderByDescending(r => r.Score == null ? -1 : r.Score.OverallScore)
                     .ThenByDescending(r => r.Job.PostedAt ?? r.Job.DiscoveredAt),
        };

        var total = await rows.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 200);

        var pageRows = await rows.Skip((page - 1) * size).Take(size).ToListAsync(ct);

        var items = pageRows
            .Select(r => Map(r.Job, r.Score, r.Stage))
            .ToList();

        var counts = await CountsAsync(profile, window, ct);

        return new JobListResult(items, total, page, size, counts);
    }

    /// <summary>One job with full detail.</summary>
    public async Task<JobListItem?> GetAsync(int jobId, int? profileId, CancellationToken ct)
    {
        var profile = await ResolveProfileAsync(profileId, ct);

        var row = await db.Jobs.AsNoTracking()
            .Where(j => j.Id == jobId)
            .Select(j => new
            {
                Job = j,
                Score = profile == null ? null : j.Scores.FirstOrDefault(s => s.SearchProfileId == profile.Id),
                Stage = j.Application == null ? (ApplicationStage?)null : j.Application.Stage,
            })
            .FirstOrDefaultAsync(ct);

        return row is null ? null : Map(row.Job, row.Score, row.Stage);
    }

    /// <summary>Full job description, fetched separately to keep list payloads small.</summary>
    public Task<string?> GetDescriptionAsync(int jobId, CancellationToken ct)
        => db.Jobs.AsNoTracking()
            .Where(j => j.Id == jobId)
            .Select(j => j.Description)
            .FirstOrDefaultAsync(ct);

    /// <summary>The overview panel: counts, spend, pipeline, top matches.</summary>
    public async Task<DashboardSummary> SummaryAsync(int? profileId, CancellationToken ct)
    {
        var profile = await ResolveProfileAsync(profileId, ct);
        var window = profile?.PostedWithinDays ?? 7;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-window);

        var counts = await CountsAsync(profile, window, ct);

        var bySource = await db.Jobs.AsNoTracking()
            .Where(j => !j.IsArchived && (j.PostedAt ?? j.DiscoveredAt) >= cutoff)
            .GroupBy(j => j.Source)
            .Select(g => new { Source = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var pipeline = await db.Applications.AsNoTracking()
            .GroupBy(a => a.Stage)
            .Select(g => new { Stage = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var top = await QueryAsync(
            new JobQuery
            {
                ProfileId = profile?.Id,
                PostedWithinDays = window,
                MinScore = profile?.MinOverallScore,
                PageSize = 8,
                HideTracked = true,
            },
            ct);

        // Group by discovery day for the sparkline. The cutoff must be computed
        // before the query — inlining DateTimeOffset.UtcNow.AddDays(...) fails to
        // translate through the ticks value converter.
        var sparklineCutoff = DateTimeOffset.UtcNow.AddDays(-30);
        var perDay = await db.Jobs.AsNoTracking()
            .Where(j => j.DiscoveredAt >= sparklineCutoff)
            .Select(j => new { j.DiscoveredAt })
            .ToListAsync(ct);

        var postedPerDay = perDay
            .GroupBy(x => x.DiscoveredAt.UtcDateTime.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyCount(g.Key.ToString("yyyy-MM-dd"), g.Count()))
            .ToList();

        var lastIngested = await db.Jobs.AsNoTracking()
            .OrderByDescending(j => j.DiscoveredAt)
            .Select(j => (DateTimeOffset?)j.DiscoveredAt)
            .FirstOrDefaultAsync(ct);

        return new DashboardSummary(
            counts,
            bySource.ToDictionary(x => x.Source.ToString(), x => x.Count),
            pipeline.ToDictionary(x => x.Stage.ToString(), x => x.Count),
            top.Items,
            postedPerDay,
            await spend.MonthToDateAsync(ct),
            null,
            await db.Jobs.CountAsync(j => !j.IsArchived, ct),
            lastIngested);
    }

    /// <summary>The filter-strip counters, all over the same freshness window.</summary>
    private async Task<DashboardCounts> CountsAsync(SearchProfile? profile, int window, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Clamp(window, 1, 3650));
        var tolerance = profile?.MaxOfficeDaysPerWeek ?? 2;
        var threshold = profile?.MinOverallScore ?? 55;

        var fresh = db.Jobs.AsNoTracking()
            .Where(j => !j.IsArchived && (j.PostedAt ?? j.DiscoveredAt) >= cutoff);

        // One aggregate round-trip instead of nine COUNT queries.
        var agg = await fresh
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Remote = g.Count(j => j.WorkModel == WorkModel.Remote),
                HybridIn = g.Count(j => j.WorkModel == WorkModel.Hybrid
                                        && (j.OfficeDaysPerWeek == null || j.OfficeDaysPerWeek <= tolerance)),
                HybridOut = g.Count(j => j.WorkModel == WorkModel.Hybrid
                                         && j.OfficeDaysPerWeek != null && j.OfficeDaysPerWeek > tolerance),
                Onsite = g.Count(j => j.WorkModel == WorkModel.Onsite),
                Unknown = g.Count(j => j.WorkModel == WorkModel.Unknown),
                Review = g.Count(j => j.WorkModelOrigin != WorkModelOrigin.Manual
                                      && (j.WorkModel == WorkModel.Unknown || j.WorkModelConfidence < 0.7)),
                Unscored = profile == null
                    ? 0
                    : g.Count(j => !j.Scores.Any(s => s.SearchProfileId == profile.Id)),
                Strong = profile == null
                    ? 0
                    : g.Count(j => j.Scores.Any(s => s.SearchProfileId == profile.Id && s.OverallScore >= threshold)),
            })
            .FirstOrDefaultAsync(ct);

        return agg is null
            ? new DashboardCounts(0, 0, 0, 0, 0, 0, 0, 0, 0)
            : new DashboardCounts(
                agg.Total, agg.Remote, agg.HybridIn, agg.HybridOut,
                agg.Onsite, agg.Unknown, agg.Strong, agg.Unscored, agg.Review);
    }

    /// <summary>Explicit id, else the active profile, else any profile.</summary>
    private Task<SearchProfile?> ResolveProfileAsync(int? profileId, CancellationToken ct)
        => profileId is int id
            ? db.SearchProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
            : db.SearchProfiles.AsNoTracking()
                .OrderByDescending(p => p.IsActive)
                .ThenBy(p => p.Id)
                .FirstOrDefaultAsync(ct);

    private static JobListItem Map(Job j, JobScore? s, ApplicationStage? stage)
    {
        var reference = j.PostedAt ?? j.DiscoveredAt;
        var ageDays = (int)Math.Floor((DateTimeOffset.UtcNow - reference).TotalDays);

        return new JobListItem(
            j.Id,
            j.Title,
            j.Company,
            j.LocationRaw,
            j.City,
            j.Url,
            j.Source.ToString(),
            j.PostedAt,
            j.DiscoveredAt,
            Math.Max(0, ageDays),
            j.WorkModel.ToString(),
            j.OfficeDaysPerWeek,
            j.WorkModelConfidence,
            j.WorkModelOrigin.ToString(),
            j.WorkModelEvidence,
            j.SalaryMin,
            j.SalaryMax,
            j.SalaryCurrency,
            j.SalaryIsEstimate,
            s?.OverallScore,
            s?.SkillScore,
            s?.SeniorityScore,
            s?.LocationScore,
            s?.FreshnessScore,
            s?.MatchedSkills ?? [],
            s?.MissingMustHaves ?? [],
            s?.MissingResumeKeywords ?? [],
            s?.RedFlags ?? [],
            s?.Rationale,
            s?.IsDeepAnalysis ?? false,
            s is not null,
            j.IsArchived,
            stage?.ToString());
    }
}
