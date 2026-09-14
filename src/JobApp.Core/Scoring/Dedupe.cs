namespace JobApp.Core.Scoring;

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using JobApp.Core.Domain;

/// <summary>
/// The same role shows up on Adzuna, JSearch and LinkedIn with slightly
/// different titles. This collapses them to one card.
/// </summary>
public static partial class Dedupe
{
    /// <summary>Stable hash of normalised company + title + city.</summary>
    public static string KeyFor(Job job)
        => KeyFor(job.Company, job.Title, job.City ?? job.LocationRaw);

    /// <summary>Stable hash of normalised company + title + city.</summary>
    public static string KeyFor(string company, string title, string? city)
    {
        var raw = $"{Normalise(company)}|{NormaliseTitle(title)}|{Normalise(city)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..24];
    }

    /// <summary>Lowercase, strip punctuation and collapse whitespace.</summary>
    private static string Normalise(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var cleaned = NonAlphaNumRegex().Replace(value.ToLowerInvariant(), " ");
        return WhitespaceRegex().Replace(cleaned, " ").Trim();
    }

    /// <summary>
    /// Normalise, then drop the noise boards bolt onto titles — seniority
    /// prefixes stay (they matter), but req-ids, locations in brackets and
    /// "urgent hiring" style padding go.
    /// </summary>
    private static string NormaliseTitle(string? title)
    {
        var s = Normalise(title);
        s = TitleNoiseRegex().Replace(s, " ");
        s = WhitespaceRegex().Replace(s, " ").Trim();

        // Boards append req numbers; a trailing digit run is never meaningful.
        s = TrailingDigitsRegex().Replace(s, "").Trim();
        return s;
    }

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonAlphaNumRegex();

    [GeneratedRegex(@"\s{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\b(?:urgent|urgently|immediate|immediately|hiring|apply now|job|opening|openings|position|vacancy|f m|m f|w m|remote|hybrid|onsite|on site|work from home|wfh|full time|fulltime|permanent|req(?:uisition)? ?id)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex TitleNoiseRegex();

    [GeneratedRegex(@"\s+\d{3,}$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingDigitsRegex();
}
