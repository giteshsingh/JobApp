namespace JobApp.Infrastructure.Sources;

using System.Text.Json;
using JobApp.Core.Abstractions;
using JobApp.Core.Domain;
using JobApp.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// JSearch on RapidAPI. It aggregates Google Jobs, which in turn indexes
/// LinkedIn, Indeed and Naukri postings — so this is the sanctioned way to see
/// LinkedIn-sourced roles without scraping LinkedIn.
/// Key: https://rapidapi.com/letscrape-6bRBa3QguO5/api/jsearch
/// </summary>
public class JSearchJobSource(
    IHttpClientFactory httpFactory,
    IOptions<JobSourceOptions> options,
    ILogger<JSearchJobSource> log) : IJobSource
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly JobSourceOptions opts = options.Value;

    public JobSourceKind Kind => JobSourceKind.JSearch;

    public string DisplayName => "JSearch (Google Jobs)";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(opts.JSearch.RapidApiKey);

    public async Task<IReadOnlyList<Job>> FetchAsync(SearchProfile profile, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return [];
        }

        var http = httpFactory.CreateClient(nameof(JSearchJobSource));
        http.DefaultRequestHeaders.Remove("X-RapidAPI-Key");
        http.DefaultRequestHeaders.Add("X-RapidAPI-Key", opts.JSearch.RapidApiKey);
        http.DefaultRequestHeaders.Remove("X-RapidAPI-Host");
        http.DefaultRequestHeaders.Add("X-RapidAPI-Host", opts.JSearch.Host);

        var jobs = new List<Job>();
        var titles = profile.TargetTitles.Count > 0 ? profile.TargetTitles : ["software engineer"];
        var countries = profile.CountryCodes.Count > 0 ? profile.CountryCodes : ["in"];

        foreach (var country in countries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var title in titles)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    jobs.AddRange(await QueryAsync(http, country, title, profile, remoteOnly: false, ct));

                    // A second remote-only sweep, since a city-scoped query hides
                    // the work-from-anywhere roles that rank highest for you.
                    if (profile.AllowGlobalRemote)
                    {
                        jobs.AddRange(await QueryAsync(http, country, title, profile, remoteOnly: true, ct));
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogWarning(ex, "JSearch query failed for {Country}/{Title}", country, title);
                }
            }
        }

        return jobs;
    }

    private async Task<List<Job>> QueryAsync(
        HttpClient http, string country, string title, SearchProfile profile, bool remoteOnly, CancellationToken ct)
    {
        // JSearch takes location as part of the free-text query, not a separate field.
        var terms = title;
        if (!remoteOnly && !string.IsNullOrWhiteSpace(profile.HomeCity))
        {
            terms = $"{title} in {profile.HomeCity}";
        }

        var query = new List<string>
        {
            $"query={Uri.EscapeDataString(terms)}",
            "page=1",
            "num_pages=1",
            $"country={country.ToLowerInvariant()}",
            $"date_posted={DatePosted(profile.PostedWithinDays)}",
        };

        if (remoteOnly)
        {
            query.Add("work_from_home=true");
        }

        using var response = await http.GetAsync($"https://{opts.JSearch.Host}/search?{string.Join('&', query)}", ct);
        if (!response.IsSuccessStatusCode)
        {
            log.LogWarning("JSearch returned {Status} for {Terms}", response.StatusCode, terms);
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var payload = await JsonSerializer.DeserializeAsync<JSearchResponse>(stream, Json, ct);

        var jobs = new List<Job>();
        foreach (var r in (payload?.Data ?? []).Take(opts.MaxResultsPerSource))
        {
            if (string.IsNullOrWhiteSpace(r.JobTitle))
            {
                continue;
            }

            var location = string.Join(", ", new[] { r.JobCity, r.JobState, r.JobCountry }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

            var job = new Job
            {
                Source = JobSourceKind.JSearch,
                ExternalId = r.JobId,
                Url = r.JobApplyLink,
                Title = r.JobTitle!,
                Company = r.EmployerName ?? "Unknown",
                LocationRaw = string.IsNullOrWhiteSpace(location) ? null : location,
                City = r.JobCity,
                CountryCode = r.JobCountry?.ToLowerInvariant() ?? country.ToLowerInvariant(),
                Description = SourceHelpers.HtmlToText(r.JobDescription),
                PostedAt = ResolvePosted(r),
                SalaryMin = SourceHelpers.Annualise(r.JobMinSalary, r.JobSalaryPeriod),
                SalaryMax = SourceHelpers.Annualise(r.JobMaxSalary, r.JobSalaryPeriod),
                SalaryCurrency = r.JobSalaryCurrency,
                SalaryIsEstimate = false,
            };

            SourceHelpers.Finish(job);

            // The feed's own remote flag beats anything we could infer.
            if (r.JobIsRemote == true)
            {
                SourceHelpers.ApplyFeedRemoteFlag(job, true);
            }

            jobs.Add(job);
        }

        return jobs;
    }

    /// <summary>Maps a day count onto JSearch's coarse date buckets.</summary>
    private static string DatePosted(int days) => days switch
    {
        <= 1 => "today",
        <= 3 => "3days",
        <= 7 => "week",
        <= 31 => "month",
        _ => "all",
    };

    private static DateTimeOffset? ResolvePosted(JSearchResult r)
    {
        if (r.JobPostedAtTimestamp is long ts && ts > 0)
        {
            return DateTimeOffset.FromUnixTimeSeconds(ts);
        }

        return DateTimeOffset.TryParse(r.JobPostedAtDatetimeUtc, out var parsed) ? parsed : null;
    }

    private record JSearchResponse(string? Status, List<JSearchResult>? Data);

    private record JSearchResult(
        string? JobId,
        string? JobTitle,
        string? EmployerName,
        string? JobApplyLink,
        string? JobDescription,
        bool? JobIsRemote,
        long? JobPostedAtTimestamp,
        string? JobPostedAtDatetimeUtc,
        string? JobCity,
        string? JobState,
        string? JobCountry,
        decimal? JobMinSalary,
        decimal? JobMaxSalary,
        string? JobSalaryCurrency,
        string? JobSalaryPeriod);
}
