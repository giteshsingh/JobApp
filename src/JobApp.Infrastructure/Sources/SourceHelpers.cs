namespace JobApp.Infrastructure.Sources;

using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using JobApp.Core.Domain;
using JobApp.Core.Scoring;

/// <summary>Shared normalisation used by every connector.</summary>
internal static partial class SourceHelpers
{
    /// <summary>
    /// Boards return descriptions as HTML. Flatten to text but keep line breaks,
    /// because bullet structure carries meaning for the scorer and the model.
    /// </summary>
    public static string HtmlToText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        // Not HTML at all — just tidy the whitespace.
        if (!html.Contains('<'))
        {
            return CollapseBlankLines(html).Trim();
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        foreach (var node in doc.DocumentNode.SelectNodes("//script|//style|//noscript") ?? Enumerable.Empty<HtmlNode>())
        {
            node.Remove();
        }

        var sb = new StringBuilder();
        Walk(doc.DocumentNode, sb);
        return CollapseBlankLines(WebUtility.HtmlDecode(sb.ToString())).Trim();
    }

    private static void Walk(HtmlNode node, StringBuilder sb)
    {
        foreach (var child in node.ChildNodes)
        {
            switch (child.Name.ToLowerInvariant())
            {
                case "#text":
                    var text = child.InnerText;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.Append(WhitespaceRegex().Replace(text, " "));
                    }

                    break;

                case "li":
                    sb.Append("\n- ");
                    Walk(child, sb);
                    break;

                case "br":
                    sb.Append('\n');
                    break;

                case "p":
                case "div":
                case "ul":
                case "ol":
                case "tr":
                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    sb.Append('\n');
                    Walk(child, sb);
                    sb.Append('\n');
                    break;

                default:
                    Walk(child, sb);
                    break;
            }
        }
    }

    /// <summary>Populates the fields every job needs regardless of source.</summary>
    public static Job Finish(Job job)
    {
        job.Title = Tidy(job.Title) ?? "Unknown";
        job.Company = Tidy(job.Company) ?? "Unknown";
        job.LocationRaw = Tidy(job.LocationRaw);
        job.City = Tidy(job.City);

        // Free keyword pass first; only ambiguous jobs cost an API call later.
        var (model, days, confidence, evidence) = WorkModelRules.Classify(job.Title, job.Description);
        if (confidence >= WorkModelRules.TrustThreshold)
        {
            job.WorkModel = model;
            job.OfficeDaysPerWeek = days;
            job.WorkModelConfidence = confidence;
            job.WorkModelEvidence = evidence;
            job.WorkModelOrigin = WorkModelOrigin.Rules;
        }

        job.DedupeKey = Dedupe.KeyFor(job);
        return job;
    }

    /// <summary>Marks a job the feed itself told us is remote — better than any guess.</summary>
    public static void ApplyFeedRemoteFlag(Job job, bool isRemote)
    {
        if (!isRemote)
        {
            return;
        }

        job.WorkModel = WorkModel.Remote;
        job.OfficeDaysPerWeek = 0;
        job.WorkModelConfidence = 0.95;
        job.WorkModelOrigin = WorkModelOrigin.SourceFeed;
        job.WorkModelEvidence = "Source feed flagged this posting as remote.";
    }

    /// <summary>Splits "Bengaluru, Karnataka, India" into a usable city guess.</summary>
    public static string? CityFrom(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return null;
        }

        var first = location.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return Tidy(first);
    }

    public static string? Tidy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return WhitespaceRegex().Replace(WebUtility.HtmlDecode(value), " ").Trim();
    }

    /// <summary>Annualises a figure a board reported per hour/day/week/month.</summary>
    public static decimal? Annualise(decimal? amount, string? period) => amount is null
        ? null
        : period?.ToLowerInvariant() switch
        {
            "hour" or "hourly" => amount * 2080m,
            "day" or "daily" => amount * 240m,
            "week" or "weekly" => amount * 52m,
            "month" or "monthly" => amount * 12m,
            _ => amount,
        };

    private static string CollapseBlankLines(string s)
        => BlankLinesRegex().Replace(s.Replace("\r\n", "\n"), "\n\n");

    [GeneratedRegex(@"[ \t\f\v]+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\n{3,}", RegexOptions.CultureInvariant)]
    private static partial Regex BlankLinesRegex();
}
