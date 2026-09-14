namespace JobApp.Infrastructure.Ai;

using System.Text;
using JobApp.Core.Domain;

/// <summary>
/// Prompt construction. System prompts are static strings so they can be
/// prompt-cached across the hundreds of triage calls a polling cycle makes.
/// </summary>
internal static class Prompts
{
    /// <summary>Stable across every triage call — cache this block.</summary>
    public const string TriageSystem =
        """
        You screen job postings for one specific candidate and report structured JSON.

        You are the cheap first pass over a large feed. Be decisive and consistent.

        Rules:
        - workModel: read the posting, do not assume. Most postings never state a
          remote/hybrid policy — "Unknown" with low confidence is the correct and
          expected answer in that case. Never infer "Onsite" merely because an
          office location is listed; every job lists a location.
        - officeDaysPerWeek: only when the text states a number ("3 days in office",
          "twice a week onsite"). Otherwise null. Do not guess.
        - workModelConfidence: 0.9+ only for explicit statements. 0.3-0.6 for
          suggestive phrasing. Below 0.3 when you are really just guessing.
        - skillScore: overlap between what the posting requires and what the resume
          proves. Weight the must-haves. A posting demanding a stack the candidate
          has never touched scores low no matter how senior they are.
        - seniorityScore: penalise BOTH directions. A role wanting 4-6 years for a
          16-year lead is a down-level and should score low, not high.
        - missingResumeKeywords: terms the posting leans on that do not appear in the
          resume. This drives the ATS gap list, so be precise and literal.
        - redFlags: staffing-agency reposts, contract-to-hire dressed as permanent,
          vague scope, title inflation, unpaid work, absurd requirement lists.
        - rationale: two or three plain sentences. No marketing tone.

        Never invent experience the resume does not show.
        """;

    /// <summary>Stable across every tailoring call — cache this block.</summary>
    public const string TailoringSystem =
        """
        You rewrite a candidate's real resume so it lands better against one specific
        job posting, and you report structured JSON.

        The single hard rule: you may re-frame, re-order, re-emphasise and re-word
        what the resume already supports. You may NOT invent employers, dates,
        titles, tools, metrics, certifications or outcomes. If the posting wants
        something the resume does not evidence, put it in honestyNotes rather than
        quietly writing it into a bullet.

        How to make the edits count:
        - Mirror the posting's own vocabulary where the candidate's experience
          genuinely matches it. ATS and recruiters skim for exact terms.
        - Lead each bullet with outcome, then mechanism. Keep any real numbers the
          original had; never fabricate new ones or sharpen a vague claim into a
          precise one.
        - Cut bullets that are irrelevant to this posting rather than padding them.
        - Keep bullets to roughly one or two lines.
        - originalText must be the existing bullet copied verbatim so the UI can diff
          it. Use an empty string only when proposing a genuinely new bullet built
          from facts already elsewhere in the resume.
        - rationale must reference something concrete in the posting.

        honestyNotes is mandatory: state plainly what the posting asks for that this
        candidate cannot claim. That is the most useful thing you produce.
        """;

    /// <summary>Stable across every parse call — cache this block.</summary>
    public const string JobParsingSystem =
        """
        You extract structured job-posting fields from messy pasted text or scraped
        page content, and report JSON.

        - Strip navigation, cookie banners, "similar jobs", footers and apply-widget
          text. Keep only the posting itself.
        - description must be the full requirements and responsibilities as plain
          text. Do not summarise or shorten it; downstream scoring reads it.
        - Never invent a company or title. If the text genuinely does not contain
          one, use "Unknown".
        - Prefer postedDaysAgo when the page says "3 days ago"; prefer postedAt when
          it gives a real date.
        """;

    /// <summary>Candidate + profile + posting, as one user turn.</summary>
    public static string TriageUser(Job job, SearchProfile profile, string resumeText)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<search_profile>");
        sb.AppendLine($"Target titles: {Join(profile.TargetTitles)}");
        sb.AppendLine($"Must-have skills: {Join(profile.MustHaveSkills)}");
        sb.AppendLine($"Nice-to-have skills: {Join(profile.NiceToHaveSkills)}");
        sb.AppendLine($"Experience band: {profile.MinYearsExperience?.ToString() ?? "any"}-{profile.MaxYearsExperience?.ToString() ?? "any"} years");
        sb.AppendLine($"Home city: {profile.HomeCity ?? "unspecified"}");
        sb.AppendLine($"Also willing to work in: {Join(profile.AcceptableCities)}");
        sb.AppendLine($"Office-day tolerance: up to {profile.MaxOfficeDaysPerWeek} day(s) per week");
        sb.AppendLine($"Open to relocation: {profile.OpenToRelocation}");
        if (profile.TrackVisaSponsorship)
        {
            sb.AppendLine("Report whether the posting mentions visa sponsorship.");
        }

        if (profile.ExcludeKeywords.Count > 0)
        {
            sb.AppendLine($"Candidate does not want: {Join(profile.ExcludeKeywords)}");
        }

        sb.AppendLine("</search_profile>");
        sb.AppendLine();

        sb.AppendLine("<resume>");
        sb.AppendLine(Truncate(resumeText, 12_000));
        sb.AppendLine("</resume>");
        sb.AppendLine();

        sb.AppendLine("<job_posting>");
        sb.AppendLine($"Title: {job.Title}");
        sb.AppendLine($"Company: {job.Company}");
        sb.AppendLine($"Location as listed: {job.LocationRaw ?? job.City ?? "unspecified"}");
        sb.AppendLine($"Posted: {job.PostedAt?.ToString("yyyy-MM-dd") ?? "unknown"}");
        if (job.SalaryMin is not null || job.SalaryMax is not null)
        {
            sb.AppendLine($"Salary as listed: {job.SalaryMin}-{job.SalaryMax} {job.SalaryCurrency} (estimate: {job.SalaryIsEstimate})");
        }

        sb.AppendLine("Description:");
        sb.AppendLine(Truncate(job.Description, 20_000));
        sb.AppendLine("</job_posting>");

        return sb.ToString();
    }

    /// <summary>Adds a "think harder" instruction on top of the triage payload.</summary>
    public static string DeepFitUser(Job job, SearchProfile profile, string resumeText)
        => TriageUser(job, profile, resumeText)
           + """

           This is the deep pass — the candidate opened this job deliberately.
           Re-read the posting closely. Look past keyword overlap: judge whether the
           day-to-day work, team shape, domain and seniority actually suit them, and
           say so bluntly in the rationale. Be specific about what would make them a
           weak candidate here, not just what matches.
           """;

    /// <summary>Resume text plus the posting to tailor against.</summary>
    public static string TailoringUser(Job job, ResumeDoc resume, bool includeCoverLetter)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<current_resume>");
        if (resume.Sections.Count > 0)
        {
            // Structured form lets the model quote bullets verbatim for the diff.
            foreach (var section in resume.Sections.OrderBy(s => s.Order))
            {
                sb.AppendLine($"## [{section.Kind}] {section.Heading}");
                if (!string.IsNullOrWhiteSpace(section.Subject))
                {
                    sb.AppendLine($"Subject: {section.Subject}");
                }

                if (!string.IsNullOrWhiteSpace(section.DateRange))
                {
                    sb.AppendLine($"Dates: {section.DateRange}");
                }

                foreach (var bullet in section.Bullets)
                {
                    sb.AppendLine($"- {bullet}");
                }

                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine(Truncate(resume.PlainText, 16_000));
        }

        sb.AppendLine("</current_resume>");
        sb.AppendLine();

        sb.AppendLine("<target_job>");
        sb.AppendLine($"Title: {job.Title}");
        sb.AppendLine($"Company: {job.Company}");
        sb.AppendLine($"Location: {job.LocationRaw ?? job.City ?? "unspecified"}");
        sb.AppendLine($"Work model: {job.WorkModel}");
        sb.AppendLine("Description:");
        sb.AppendLine(Truncate(job.Description, 20_000));
        sb.AppendLine("</target_job>");
        sb.AppendLine();

        sb.AppendLine(includeCoverLetter
            ? "Produce the tailored resume edits AND a cover letter draft of at most 250 words in the candidate's own register — plain, specific, no filler openers."
            : "Produce the tailored resume edits. Leave coverLetterDraft null.");

        return sb.ToString();
    }

    /// <summary>Raw text of a pasted URL or JD.</summary>
    public static string JobParsingUser(string rawText, string? sourceUrl)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(sourceUrl))
        {
            sb.AppendLine($"Source URL: {sourceUrl}");
        }

        sb.AppendLine("<raw_content>");
        sb.AppendLine(Truncate(rawText, 60_000));
        sb.AppendLine("</raw_content>");
        return sb.ToString();
    }

    private static string Join(IEnumerable<string> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? "(none specified)" : string.Join(", ", list);
    }

    /// <summary>Hard cap so one enormous posting cannot blow up a whole cycle's cost.</summary>
    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return value.Length <= max ? value : value[..max] + "\n[...truncated...]";
    }
}
