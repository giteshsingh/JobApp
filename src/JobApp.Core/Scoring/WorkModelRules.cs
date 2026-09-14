namespace JobApp.Core.Scoring;

using System.Text.RegularExpressions;
using JobApp.Core.Domain;

/// <summary>
/// A free keyword pass over the JD. It only ever reports a *high confidence*
/// answer, so the AI classifier still handles anything ambiguous. Its real job
/// is to stop us paying Claude for the obvious cases.
/// </summary>
public static partial class WorkModelRules
{
    /// <summary>Confidence at or above this is treated as settled; below it, ask Claude.</summary>
    public const double TrustThreshold = 0.85;

    /// <summary>Best-effort classification. Confidence 0 means "no idea, ask Claude".</summary>
    public static (WorkModel Model, int? Days, double Confidence, string? Evidence) Classify(string title, string description)
    {
        var text = $"{title}\n{description}";

        // "2 days in office" / "3 days per week onsite" / "hybrid - 2 days"
        var dayMatch = OfficeDaysRegex().Match(text);
        if (dayMatch.Success && int.TryParse(dayMatch.Groups["days"].Value, out var days) && days is >= 1 and <= 5)
        {
            return (WorkModel.Hybrid, days, 0.92, Snippet(text, dayMatch.Index));
        }

        var wordDayMatch = OfficeDaysWordRegex().Match(text);
        if (wordDayMatch.Success)
        {
            var n = WordToNumber(wordDayMatch.Groups["days"].Value);
            if (n is int nd)
            {
                return (WorkModel.Hybrid, nd, 0.9, Snippet(text, wordDayMatch.Index));
            }
        }

        // Unambiguous full-remote phrasing.
        var remote = FullyRemoteRegex().Match(text);
        if (remote.Success && !HybridRegex().IsMatch(text))
        {
            return (WorkModel.Remote, 0, 0.88, Snippet(text, remote.Index));
        }

        // Unambiguous onsite phrasing.
        var onsite = FullyOnsiteRegex().Match(text);
        if (onsite.Success && !HybridRegex().IsMatch(text) && !remote.Success)
        {
            return (WorkModel.Onsite, 5, 0.86, Snippet(text, onsite.Index));
        }

        // Says hybrid but not how many days — genuinely needs Claude to read it.
        var hybrid = HybridRegex().Match(text);
        if (hybrid.Success)
        {
            return (WorkModel.Hybrid, null, 0.6, Snippet(text, hybrid.Index));
        }

        return (WorkModel.Unknown, null, 0d, null);
    }

    /// <summary>~180 chars around the match, for the "why" tooltip in the UI.</summary>
    private static string Snippet(string text, int index)
    {
        var start = Math.Max(0, index - 60);
        var len = Math.Min(180, text.Length - start);
        return text.Substring(start, len).Replace('\n', ' ').Trim();
    }

    private static int? WordToNumber(string word) => word.ToLowerInvariant() switch
    {
        "one" => 1,
        "two" => 2,
        "three" => 3,
        "four" => 4,
        "five" => 5,
        _ => null,
    };

    // Both alternatives reuse the "days" group name — .NET allows that, so the
    // caller reads one group regardless of which side matched.
    [GeneratedRegex(@"(?<days>[1-5])\s*(?:-|to|\+)?\s*days?\s*(?:a|per|each)?\s*week[^.]{0,40}?(?:office|on-?site|in\s*office|onsite)|(?:office|on-?site|onsite|in\s*office)[^.]{0,40}?(?<days>[1-5])\s*days?\s*(?:a|per|each)?\s*week",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OfficeDaysRegex();

    [GeneratedRegex(@"(?<days>one|two|three|four|five)\s*days?\s*(?:a|per|each)?\s*week[^.]{0,40}?(?:office|on-?site|onsite)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OfficeDaysWordRegex();

    [GeneratedRegex(@"\b(?:100%\s*remote|fully\s*remote|remote[- ]first|work\s*from\s*anywhere|permanently\s*remote|remote\s*\(anywhere\))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FullyRemoteRegex();

    [GeneratedRegex(@"\b(?:hybrid|flexi(?:ble)?\s*(?:work)?\s*model|part\s*remote|split\s*week)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HybridRegex();

    [GeneratedRegex(@"\b(?:5\s*days?\s*(?:a|per)\s*week\s*(?:from\s*)?office|work\s*from\s*office\s*only|no\s*remote|fully\s*on-?site|100%\s*on-?site|in-?office\s*role)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FullyOnsiteRegex();
}
