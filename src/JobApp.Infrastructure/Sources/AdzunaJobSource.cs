namespace JobApp.Infrastructure.Sources;

using System.Text.Json;
using JobApp.Core.Abstractions;
using JobApp.Core.Domain;
using JobApp.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Adzuna search API. Broad coverage including India, and it supports a real
/// max_days_old filter, which is what makes the "last 7 days" view honest.
/// Register at https://developer.adzuna.com for a free app id + key.
/// </summary>
public class AdzunaJobSource(
    IHttpClientFactory httpFactory,
    IOptions<JobSourceOptions> options,
    ILogger<AdzunaJobSource> log) : IJobSource
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly JobSourceOptions opts = options.Value;

    public JobSourceKind Kind => JobSourceKind.Adzuna;

    public string DisplayName => "Adzuna";

    public bool IsConfigured
        => !string.IsNullOrWhiteSpace(opts.Adzuna.AppId) && !string.IsNullOrWhiteSpace(opts.Adzuna.AppKey);

    public async Task<IReadOnlyList<Job>> FetchAsync(SearchProfile profile, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return [];
        }

        var http = httpFactory.CreateClient(nameof(AdzunaJobSource));
        var jobs = new List<Job>();

        // Adzuna is one request per country per title, so keep the matrix small.
        var countries = profile.CountryCodes.Count > 0 ? profile.CountryCodes : ["in"];
        var titles = profile.TargetTitles.Count > 0 ? profile.TargetTitles : ["software"];
        var perQuery = Math.Max(5, opts.MaxResultsPerSource / Math.Max(1, titles.Count));

        foreach (var country in countries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var title in titles)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    jobs.AddRange(await QueryAsync(http, country, title, profile, perQuery, ct));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A connector must never take down the whole polling cycle.
                    log.LogWarning(ex, "Adzuna query failed for {Country}/{Title}", country, title);
                }
            }
        }

        return jobs;
    }

    private async Task<List<Job>> QueryAsync(
        HttpClient http, string country, string title, SearchProfile profile, int take, CancellationToken ct)
    {
        var query = new List<string>
        {
            $"app_id={Uri.EscapeDataString(opts.Adzuna.AppId!)}",
            $"app_key={Uri.EscapeDataString(opts.Adzuna.AppKey!)}",
            $"results_per_page={Math.Clamp(take, 1, 50)}",
            $"what={Uri.EscapeDataString(title)}",
            $"max_days_old={Math.Clamp(profile.PostedWithinDays, 1, 90)}",
            "sort_by=date",
            "content-type=application/json",
        };

        // Location narrowing only makes sense when we are not chasing global remote.
        if (!string.IsNullOrWhiteSpace(profile.HomeCity) && !profile.AllowGlobalRemote)
        {
            query.Add($"where={Uri.EscapeDataString(profile.HomeCity)}");
        }

        if (profile.MinSalary is decimal min && min > 0
            && profile.SalaryCurrency.Equals(CurrencyFor(country), StringComparison.OrdinalIgnoreCase))
        {
            query.Add($"salary_min={(long)min}");
            // Keep undisclosed-salary listings when the profile wants them.
            query.Add($"salary_include_unknown={(profile.IncludeUndisclosedSalary ? 1 : 0)}");
        }

        var url = $"https://api.adzuna.com/v1/api/jobs/{country.ToLowerInvariant()}/search/1?{string.Join('&', query)}";

        using var response = await http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            log.LogWarning("Adzuna returned {Status} for {Country}/{Title}", response.StatusCode, country, title);
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var payload = await JsonSerializer.DeserializeAsync<AdzunaResponse>(stream, Json, ct);

        var jobs = new List<Job>();
        foreach (var r in payload?.Results ?? [])
        {
            if (string.IsNullOrWhiteSpace(r.Title))
            {
                continue;
            }

            var location = r.Location?.DisplayName;

            var job = new Job
            {
                Source = JobSourceKind.Adzuna,
                ExternalId = r.Id,
                Url = r.RedirectUrl,
                Title = r.Title!,
                Company = r.Company?.DisplayName ?? "Unknown",
                LocationRaw = location,
                // Adzuna's area array runs country-first; the last entry is finest-grained.
                City = r.Location?.Area?.LastOrDefault() ?? SourceHelpers.CityFrom(location),
                CountryCode = country.ToLowerInvariant(),
                Description = SourceHelpers.HtmlToText(r.Description),
                PostedAt = r.Created,
                SalaryMin = r.SalaryMin,
                SalaryMax = r.SalaryMax,
                SalaryCurrency = r.SalaryMin is null && r.SalaryMax is null ? null : CurrencyFor(country),
                SalaryIsEstimate = r.SalaryIsPredicted == "1",
            };

            jobs.Add(SourceHelpers.Finish(job));
        }

        return jobs;
    }

    /// <summary>Adzuna reports salary in the local currency of the country endpoint.</summary>
    private static string CurrencyFor(string country) => country.ToLowerInvariant() switch
    {
        "in" => "INR",
        "gb" => "GBP",
        "us" => "USD",
        "ca" => "CAD",
        "au" => "AUD",
        "nz" => "NZD",
        "sg" => "SGD",
        "za" => "ZAR",
        "ch" => "CHF",
        "pl" => "PLN",
        "br" => "BRL",
        "mx" => "MXN",
        _ => "EUR",
    };

    private record AdzunaResponse(List<AdzunaResult>? Results);

    private record AdzunaResult(
        string? Id,
        string? Title,
        string? Description,
        string? RedirectUrl,
        DateTimeOffset? Created,
        decimal? SalaryMin,
        decimal? SalaryMax,
        string? SalaryIsPredicted,
        AdzunaCompany? Company,
        AdzunaLocation? Location);

    private record AdzunaCompany(string? DisplayName);

    private record AdzunaLocation(string? DisplayName, List<string>? Area);
}
