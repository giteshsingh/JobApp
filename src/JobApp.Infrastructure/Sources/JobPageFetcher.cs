namespace JobApp.Infrastructure.Sources;

using System.Net;
using System.Text.Json;
using HtmlAgilityPack;
using JobApp.Core.Abstractions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Fetches a pasted job URL and returns readable text for the AI parser.
///
/// This does a single plain GET of a URL you supplied by hand — no login, no
/// session replay, no crawling. LinkedIn and Naukri frequently serve a login
/// wall to anonymous requests; when that happens this returns null and the
/// caller falls back to asking you to paste the description text instead.
/// </summary>
public class JobPageFetcher(IHttpClientFactory httpFactory, ILogger<JobPageFetcher> log) : IJobPageFetcher
{
    /// <summary>Pages shorter than this are almost certainly a login wall.</summary>
    private const int MinUsefulLength = 400;

    public async Task<string?> FetchReadableTextAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        try
        {
            var http = httpFactory.CreateClient(nameof(JobPageFetcher));
            using var response = await http.GetAsync(uri, ct);
            if (!response.IsSuccessStatusCode)
            {
                log.LogInformation("Fetch of {Url} returned {Status}; paste the description instead", uri, response.StatusCode);
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(ct);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Most job sites embed schema.org JobPosting JSON-LD, which is far
            // cleaner than anything we could scrape out of the rendered markup.
            var structured = ExtractJobPostingJsonLd(doc);
            if (!string.IsNullOrWhiteSpace(structured))
            {
                return structured;
            }

            var text = ExtractMainText(doc);
            if (string.IsNullOrWhiteSpace(text) || text.Length < MinUsefulLength)
            {
                log.LogInformation("Fetched {Url} but got only {Length} chars — likely a login wall", uri, text?.Length ?? 0);
                return null;
            }

            return text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Could not fetch {Url}", url);
            return null;
        }
    }

    /// <summary>Pulls the schema.org JobPosting block out of the page, if present.</summary>
    private static string? ExtractJobPostingJsonLd(HtmlDocument doc)
    {
        var nodes = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
        if (nodes is null)
        {
            return null;
        }

        foreach (var node in nodes)
        {
            var raw = WebUtility.HtmlDecode(node.InnerText)?.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            JsonDocument parsed;
            try
            {
                parsed = JsonDocument.Parse(raw);
            }
            catch (JsonException)
            {
                continue; // some sites emit invalid JSON-LD; just skip it
            }

            using (parsed)
            {
                var found = FindJobPosting(parsed.RootElement);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    /// <summary>JSON-LD may be a bare object, a @graph, or an array of either.</summary>
    private static string? FindJobPosting(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var found = FindJobPosting(item);
                    if (found is not null)
                    {
                        return found;
                    }
                }

                return null;

            case JsonValueKind.Object:
                if (element.TryGetProperty("@type", out var type) && IsJobPosting(type))
                {
                    return Flatten(element);
                }

                if (element.TryGetProperty("@graph", out var graph))
                {
                    return FindJobPosting(graph);
                }

                return null;

            default:
                return null;
        }
    }

    /// <summary>@type can be a string or an array of strings.</summary>
    private static bool IsJobPosting(JsonElement type) => type.ValueKind switch
    {
        JsonValueKind.String => string.Equals(type.GetString(), "JobPosting", StringComparison.OrdinalIgnoreCase),
        JsonValueKind.Array => type.EnumerateArray().Any(t =>
            string.Equals(t.GetString(), "JobPosting", StringComparison.OrdinalIgnoreCase)),
        _ => false,
    };

    /// <summary>
    /// Renders the JobPosting as labelled lines. The AI parser reads this, so
    /// readability beats fidelity — but the HTML description is unwrapped first.
    /// </summary>
    private static string Flatten(JsonElement posting)
    {
        var lines = new List<string> { "Structured JobPosting data extracted from the page:" };

        void Add(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                lines.Add($"{label}: {value.Trim()}");
            }
        }

        Add("Title", Str(posting, "title"));
        Add("Employment type", Str(posting, "employmentType"));
        Add("Date posted", Str(posting, "datePosted"));
        Add("Valid through", Str(posting, "validThrough"));

        if (posting.TryGetProperty("hiringOrganization", out var org))
        {
            Add("Company", Str(org, "name"));
        }

        // jobLocation can be one object or many.
        if (posting.TryGetProperty("jobLocation", out var loc))
        {
            var locations = loc.ValueKind == JsonValueKind.Array
                ? loc.EnumerateArray().ToArray()
                : new[] { loc };

            foreach (var one in locations)
            {
                if (one.TryGetProperty("address", out var addr))
                {
                    var parts = new[] { "addressLocality", "addressRegion", "addressCountry" }
                        .Select(k => Str(addr, k))
                        .Where(v => !string.IsNullOrWhiteSpace(v));
                    Add("Location", string.Join(", ", parts));
                }
            }
        }

        // Schema.org's explicit remote marker — the most reliable signal there is.
        Add("Job location type", Str(posting, "jobLocationType"));
        Add("Applicant location requirements", Str(posting, "applicantLocationRequirements"));

        if (posting.TryGetProperty("baseSalary", out var salary))
        {
            Add("Salary currency", Str(salary, "currency"));
            if (salary.TryGetProperty("value", out var val))
            {
                Add("Salary min", Str(val, "minValue"));
                Add("Salary max", Str(val, "maxValue"));
                Add("Salary unit", Str(val, "unitText"));
            }
        }

        var description = Str(posting, "description");
        if (!string.IsNullOrWhiteSpace(description))
        {
            lines.Add("Description:");
            lines.Add(SourceHelpers.HtmlToText(description));
        }

        return string.Join('\n', lines);
    }

    /// <summary>Reads a property as a string regardless of its JSON type.</summary>
    private static string? Str(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var v))
        {
            return null;
        }

        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Object => Str(v, "name") ?? Str(v, "value"),
            JsonValueKind.Array => string.Join(", ", v.EnumerateArray()
                .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : Str(e, "name"))
                .Where(s => !string.IsNullOrWhiteSpace(s))),
            _ => null,
        };
    }

    /// <summary>Fallback: strip chrome and return the densest text we can find.</summary>
    private static string ExtractMainText(HtmlDocument doc)
    {
        foreach (var node in doc.DocumentNode
                     .SelectNodes("//script|//style|//noscript|//nav|//header|//footer|//svg|//form")
                 ?? Enumerable.Empty<HtmlNode>())
        {
            node.Remove();
        }

        // Prefer a semantic container when the page offers one.
        var main = doc.DocumentNode.SelectSingleNode("//main")
                   ?? doc.DocumentNode.SelectSingleNode("//article")
                   ?? doc.DocumentNode.SelectSingleNode("//*[@role='main']")
                   ?? doc.DocumentNode.SelectSingleNode("//body");

        return SourceHelpers.HtmlToText(main?.InnerHtml);
    }
}
