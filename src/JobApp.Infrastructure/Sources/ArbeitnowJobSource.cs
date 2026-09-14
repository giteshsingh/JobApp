namespace JobApp.Infrastructure.Sources;

using System.Text.Json;
using JobApp.Core.Abstractions;
using JobApp.Core.Domain;
using JobApp.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Arbeitnow's public job board feed — free, no key. Europe-weighted with a
/// solid remote slice, and it includes visa-sponsorship tags, which matters if
/// you turn on relocation.
/// </summary>
public class ArbeitnowJobSource(
    IHttpClientFactory httpFactory,
    IOptions<JobSourceOptions> options,
    ILogger<ArbeitnowJobSource> log) : IJobSource
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly JobSourceOptions opts = options.Value;

    public JobSourceKind Kind => JobSourceKind.Arbeitnow;

    public string DisplayName => "Arbeitnow";

    public bool IsConfigured => opts.EnableArbeitnow;

    public async Task<IReadOnlyList<Job>> FetchAsync(SearchProfile profile, CancellationToken ct)
    {
        // Only worth calling if you'd take a remote or overseas role.
        if (!IsConfigured || (!profile.AllowGlobalRemote && !profile.OpenToRelocation))
        {
            return [];
        }

        var http = httpFactory.CreateClient(nameof(ArbeitnowJobSource));
        var jobs = new List<Job>();

        try
        {
            // The feed has no query parameter, so filter client-side by title.
            using var response = await http.GetAsync("https://www.arbeitnow.com/api/job-board-api", ct);
            if (!response.IsSuccessStatusCode)
            {
                log.LogWarning("Arbeitnow returned {Status}", response.StatusCode);
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var payload = await JsonSerializer.DeserializeAsync<ArbeitnowResponse>(stream, Json, ct);

            var titles = profile.TargetTitles;
            var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Clamp(profile.PostedWithinDays, 1, 90));

            foreach (var r in payload?.Data ?? [])
            {
                if (string.IsNullOrWhiteSpace(r.Title))
                {
                    continue;
                }

                var posted = r.CreatedAt is long ts && ts > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(ts)
                    : (DateTimeOffset?)null;

                if (posted is not null && posted < cutoff)
                {
                    continue;
                }

                // Loose relevance filter: any target title word must appear.
                if (titles.Count > 0 && !titles.Any(t => TitleMatches(r.Title!, t)))
                {
                    continue;
                }

                var job = new Job
                {
                    Source = JobSourceKind.Arbeitnow,
                    ExternalId = r.Slug,
                    Url = r.Url,
                    Title = r.Title!,
                    Company = r.CompanyName ?? "Unknown",
                    LocationRaw = r.Location,
                    City = SourceHelpers.CityFrom(r.Location),
                    Description = BuildDescription(r),
                    PostedAt = posted,
                };

                SourceHelpers.Finish(job);
                SourceHelpers.ApplyFeedRemoteFlag(job, r.Remote == true);
                jobs.Add(job);

                if (jobs.Count >= opts.MaxResultsPerSource)
                {
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Arbeitnow fetch failed");
        }

        return jobs;
    }

    /// <summary>True when any significant word of the target title is in the job title.</summary>
    private static bool TitleMatches(string jobTitle, string targetTitle)
        => targetTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length > 3)
            .Any(w => jobTitle.Contains(w, StringComparison.OrdinalIgnoreCase));

    private static string BuildDescription(ArbeitnowJob r)
    {
        var body = SourceHelpers.HtmlToText(r.Description);
        var header = new List<string>();

        if (r.Tags is { Count: > 0 })
        {
            header.Add($"Tags: {string.Join(", ", r.Tags)}");
        }

        if (r.JobTypes is { Count: > 0 })
        {
            header.Add($"Job types: {string.Join(", ", r.JobTypes)}");
        }

        if (r.Remote == true)
        {
            header.Add("Remote: yes (per source feed)");
        }

        return header.Count == 0 ? body : string.Join('\n', header) + "\n\n" + body;
    }

    private record ArbeitnowResponse(List<ArbeitnowJob>? Data);

    private record ArbeitnowJob(
        string? Slug,
        string? CompanyName,
        string? Title,
        string? Description,
        bool? Remote,
        string? Url,
        List<string>? Tags,
        List<string>? JobTypes,
        string? Location,
        long? CreatedAt);
}
