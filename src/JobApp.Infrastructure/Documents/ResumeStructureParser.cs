namespace JobApp.Infrastructure.Documents;

using System.Text.RegularExpressions;
using JobApp.Core.Domain;

/// <summary>
/// Turns a flat list of resume lines into sections and bullets. Heuristic by
/// necessity — resumes have no schema — but it only has to be good enough for
/// the tailoring model to quote bullets verbatim for the diff.
/// </summary>
internal static partial class ResumeStructureParser
{
    /// <summary>Headings we recognise, mapped to a section kind.</summary>
    private static readonly (string[] Keywords, ResumeSectionKind Kind)[] HeadingMap =
    [
        (["technical skill", "skills", "core competenc", "technolog", "tech stack"], ResumeSectionKind.Skills),
        (["work experience", "experience", "employment", "professional experience", "career"], ResumeSectionKind.Experience),
        (["key project", "projects", "selected project"], ResumeSectionKind.Projects),
        (["education", "academic", "qualification"], ResumeSectionKind.Education),
        (["summary", "profile", "objective", "about"], ResumeSectionKind.Summary),
    ];

    /// <summary>Sections that carry no tailoring value and only add prompt cost.</summary>
    private static readonly string[] IgnoredHeadings =
    [
        "declaration", "personal profile", "personal details", "personal information",
        "references", "hobbies", "interests",
    ];

    /// <summary>Splits lines into ordered sections.</summary>
    public static List<ResumeSection> Parse(IReadOnlyList<string> lines)
    {
        var sections = new List<ResumeSection>();
        var order = 0;

        // Anything before the first heading is the summary/contact preamble.
        var current = new ResumeSection
        {
            Kind = ResumeSectionKind.Summary,
            Heading = "Summary",
            Order = order++,
        };

        var skipping = false;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (IsHeading(line, out var kind))
            {
                // Close the section we were building.
                if (current.Bullets.Count > 0)
                {
                    sections.Add(current);
                }

                skipping = IgnoredHeadings.Any(h => line.Contains(h, StringComparison.OrdinalIgnoreCase));

                current = new ResumeSection
                {
                    Kind = kind,
                    Heading = Titleise(line),
                    Order = order++,
                };
                continue;
            }

            if (skipping)
            {
                continue;
            }

            // A date range on its own line labels the entry that follows.
            if (DateRangeRegex().IsMatch(line) && line.Length < 60)
            {
                current.DateRange = string.IsNullOrWhiteSpace(current.DateRange)
                    ? line
                    : $"{current.DateRange} {line}";
                continue;
            }

            // "Company. Job Title." style entry headers become the subject.
            if (current.Kind is ResumeSectionKind.Experience or ResumeSectionKind.Projects
                && current.Subject is null
                && LooksLikeEntryHeader(line))
            {
                current.Subject = line.TrimEnd('.');
                continue;
            }

            current.Bullets.Add(StripBulletGlyph(line));
        }

        if (current.Bullets.Count > 0)
        {
            sections.Add(current);
        }

        return sections;
    }

    /// <summary>
    /// A heading is short, has no sentence-ending punctuation, and either matches
    /// a known keyword or is written in caps.
    /// </summary>
    private static bool IsHeading(string line, out ResumeSectionKind kind)
    {
        kind = ResumeSectionKind.Other;

        if (line.Length > 45 || line.EndsWith('.') || line.Contains(':'))
        {
            return false;
        }

        var lower = line.ToLowerInvariant().Trim();

        foreach (var (keywords, k) in HeadingMap)
        {
            if (keywords.Any(kw => lower == kw || lower.StartsWith(kw, StringComparison.Ordinal)))
            {
                kind = k;
                return true;
            }
        }

        if (IgnoredHeadings.Any(h => lower.StartsWith(h, StringComparison.Ordinal)))
        {
            kind = ResumeSectionKind.Other;
            return true;
        }

        // ALL-CAPS short line with no digits is almost always a heading.
        var letters = line.Where(char.IsLetter).ToArray();
        if (letters.Length >= 4 && letters.All(char.IsUpper) && !line.Any(char.IsDigit))
        {
            kind = ResumeSectionKind.Other;
            return true;
        }

        return false;
    }

    /// <summary>Company/role lines end in a period and contain no bullet-style verbs.</summary>
    private static bool LooksLikeEntryHeader(string line)
        => line.Length < 110
           && (line.Contains(" Ltd", StringComparison.OrdinalIgnoreCase)
               || line.Contains(" Inc", StringComparison.OrdinalIgnoreCase)
               || line.Contains(" Pvt", StringComparison.OrdinalIgnoreCase)
               || line.Contains("Lead", StringComparison.OrdinalIgnoreCase)
               || line.Contains("Engineer", StringComparison.OrdinalIgnoreCase)
               || line.Contains("Developer", StringComparison.OrdinalIgnoreCase)
               || line.Contains("Manager", StringComparison.OrdinalIgnoreCase))
           && !line.EndsWith(',');

    /// <summary>"WORK EXPERIENCE" reads better as "Work Experience" in the UI.</summary>
    private static string Titleise(string heading)
    {
        var letters = heading.Where(char.IsLetter).ToArray();
        if (letters.Length == 0 || !letters.All(char.IsUpper))
        {
            return heading;
        }

        return string.Join(' ', heading.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }

    private static string StripBulletGlyph(string line)
        => BulletGlyphRegex().Replace(line, "").Trim();

    /// <summary>Pulls a skills list out of "Languages: C#, TypeScript" style lines.</summary>
    public static List<string> ExtractSkills(IEnumerable<ResumeSection> sections)
    {
        var skills = new List<string>();

        foreach (var section in sections.Where(s => s.Kind == ResumeSectionKind.Skills))
        {
            foreach (var bullet in section.Bullets)
            {
                // Drop the "Languages:" style label before splitting.
                var body = bullet.Contains(':') ? bullet[(bullet.IndexOf(':') + 1)..] : bullet;

                skills.AddRange(body
                    .Split([',', ';', '|', '/'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim(' ', '.', ')', '('))
                    .Where(s => s.Length is > 1 and < 40));
            }
        }

        return skills.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Finds "14 Year Exp" / "over 14 years of experience" in the text.</summary>
    public static int? ExtractYearsExperience(string plainText)
    {
        var match = YearsRegex().Match(plainText);
        return match.Success && int.TryParse(match.Groups["years"].Value, out var years) && years is > 0 and < 60
            ? years
            : null;
    }

    [GeneratedRegex(@"^\s*(?:[•●▪·‣⁃o\-\*–—]|\d+[.)])\s+", RegexOptions.CultureInvariant)]
    private static partial Regex BulletGlyphRegex();

    [GeneratedRegex(@"\b(?:jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*\s*\d{4}\b|\b(?:19|20)\d{2}\s*(?:-|–|to)\s*(?:(?:19|20)\d{2}|present|current|date)\b|\btill\s*date\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DateRangeRegex();

    [GeneratedRegex(@"(?<years>\d{1,2})\+?\s*(?:year|yr)s?\s*(?:of\s*)?(?:exp|experience)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex YearsRegex();
}
