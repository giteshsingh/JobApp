namespace JobApp.Infrastructure.Sources;

using System.Text.Json;
using JobApp.Core.Abstractions;
using JobApp.Core.Domain;
using JobApp.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Remotive — free, no key, remote-only roles. Fills the global-remote half of
/// the funnel that the India-scoped boards miss.
/// </summary>
public class RemotiveJobSource(
    IHttpClientFactory httpFactory,
    IOptions<JobSourceOptions> options,
    ILogger<RemotiveJobSource> log) : IJobSource
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly JobSourceOptions opts = options.Value;

    public JobSourceKind Kind => JobSourceKind.Remotive;

    public string DisplayName => "Remotive";

    public bool IsConfigured => opts.EnableRemotive;

    public async Task<IReadOnlyList<Job>> FetchAsync(SearchProfile profile, CancellationToken ct)
    {
        // Every listing here is remote, so skip the call if you don't want remote.
        if (!IsConfigured || !profile.AllowGlobalRemote)
        {
            return [];
        }

        var http = httpFactory.CreateClient(nameof(RemotiveJobSource));
        var jobs = new List<Job>();
        var titles = profile.TargetTitles.Count > 0 ? profile.TargetTitles : ["software development"];

        foreach (var title in titles)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var url = $"https://remotive.com/api/remote-jobs?limit={Math.Clamp(opts.MaxResultsPerSource, 1, 100)}&search={Uri.EscapeDataString(title)}";
                using var response = await http.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    log.LogWarning("Remotive returned {Status} for {Title}", response.StatusCode, title);
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                var payload = await JsonSerializer.DeserializeAsync<RemotiveResponse>(stream, Json, ct);

                foreach (var r in payload?.Jobs ?? [])
                {
                    if (string.IsNullOrWhiteSpace(r.Title))
                    {
                        continue;
                    }

                    var job = new Job
                    {
                        Source = JobSourceKind.Remotive,
                        ExternalId = r.Id?.ToString(),
                        Url = r.Url,
                        Title = r.Title!,
                        Company = r.CompanyName ?? "Unknown",
                        // Remotive states eligibility, e.g. "Worldwide" or "India".
                        LocationRaw = r.CandidateRequiredLocation,
                        City = null,
                        CountryCode = null,
                        Description = BuildDescription(r),
                        PostedAt = r.PublicationDate,
                    };

                    SourceHelpers.Finish(job);
                    SourceHelpers.ApplyFeedRemoteFlag(job, true);
                    jobs.Add(job);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Remotive query failed for {Title}", title);
            }
        }

        return jobs;
    }

    /// <summary>
    /// Tags and the eligibility line matter for scoring — a "Worldwide" role is
    /// very different from one restricted to the US — so keep them in the text.
    /// </summary>
    private static string BuildDescription(RemotiveJob r)
    {
        var body = SourceHelpers.HtmlToText(r.Description);
        var header = new List<string>();

        if (!string.IsNullOrWhiteSpace(r.CandidateRequiredLocation))
        {
            header.Add($"Eligible locations: {r.CandidateRequiredLocation}");
        }

        if (!string.IsNullOrWhiteSpace(r.JobType))
        {
            header.Add($"Job type: {r.JobType}");
        }

        if (r.Tags is { Count: > 0 })
        {
            header.Add($"Tags: {string.Join(", ", r.Tags)}");
        }

        if (!string.IsNullOrWhiteSpace(r.Salary))
        {
            header.Add($"Salary: {r.Salary}");
        }

        return header.Count == 0 ? body : string.Join('\n', header) + "\n\n" + body;
    }

    private record RemotiveResponse(List<RemotiveJob>? Jobs);

    private record RemotiveJob(
        long? Id,
        string? Url,
        string? Title,
        string? CompanyName,
        string? JobType,
        DateTimeOffset? PublicationDate,
        string? CandidateRequiredLocation,
        string? Salary,
        string? Description,
        List<string>? Tags);
}
