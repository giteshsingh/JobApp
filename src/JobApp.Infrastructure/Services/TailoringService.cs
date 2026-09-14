namespace JobApp.Infrastructure.Services;

using JobApp.Core.Abstractions;
using JobApp.Core.Domain;
using JobApp.Infrastructure.Ai;
using JobApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Runs the "customise my resume for this job" flow and turns your accepted
/// suggestions into a downloadable document.
/// </summary>
public class TailoringService(
    JobAppDbContext db,
    IJobAnalyst analyst,
    SpendGuard spend,
    IEnumerable<IResumeRenderer> renderers,
    ILogger<TailoringService> log)
{
    /// <summary>Asks Claude for tailoring suggestions and stores them for review.</summary>
    public async Task<TailoringSession> CreateAsync(
        int jobId, int? resumeId, bool includeCoverLetter, CancellationToken ct)
    {
        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, ct)
                  ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        var resume = resumeId is int id
            ? await db.Resumes.FirstOrDefaultAsync(r => r.Id == id, ct)
            : await db.Resumes
                .Where(r => !r.IsCoverLetter)
                .OrderByDescending(r => r.IsMaster)
                .ThenByDescending(r => r.UpdatedAt)
                .FirstOrDefaultAsync(ct);

        if (resume is null)
        {
            throw new InvalidOperationException("Upload a resume before tailoring.");
        }

        if (!await spend.CanSpendAsync(ct))
        {
            throw new InvalidOperationException("Monthly Claude budget reached. Raise it in settings to continue.");
        }

        var session = new TailoringSession
        {
            JobId = job.Id,
            ResumeDocId = resume.Id,
            Status = TailoringStatus.Pending,
        };

        db.Tailorings.Add(session);
        await db.SaveChangesAsync(ct);

        try
        {
            var response = await analyst.TailorResumeAsync(job, resume, includeCoverLetter, ct);
            var result = response.Value;

            session.TailoredSummary = result.TailoredSummary;
            session.TailoredSkillsLine = result.TailoredSkillsLine;
            session.KeywordsToAdd = [.. result.KeywordsToAdd];
            session.HonestyNotes = result.HonestyNotes;
            session.CoverLetterDraft = result.CoverLetterDraft;
            session.ModelUsed = response.Usage.Model;
            session.Status = TailoringStatus.Ready;

            session.Suggestions = result.Suggestions
                .Select((s, i) => new ResumeSuggestion
                {
                    Section = s.Section,
                    SectionHeading = s.SectionHeading,
                    OriginalText = s.OriginalText,
                    SuggestedText = s.SuggestedText,
                    Rationale = s.Rationale,
                    KeywordsAdded = [.. s.KeywordsAdded],
                    Order = i,
                })
                .ToList();

            await db.SaveChangesAsync(ct);
            await spend.RecordAsync(
                includeCoverLetter ? AiPurpose.CoverLetter : AiPurpose.ResumeTailoring,
                response.Usage,
                job.Id,
                ct);

            return session;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Tailoring failed for job {JobId}", jobId);

            session.Status = TailoringStatus.Failed;
            session.Error = ex.Message;
            await db.SaveChangesAsync(ct);

            throw;
        }
    }

    /// <summary>Records your accept/reject/edit decision on one suggestion.</summary>
    public async Task<ResumeSuggestion> DecideAsync(
        int suggestionId, bool? accepted, string? editedText, CancellationToken ct)
    {
        var suggestion = await db.Suggestions.FirstOrDefaultAsync(s => s.Id == suggestionId, ct)
                         ?? throw new KeyNotFoundException($"Suggestion {suggestionId} not found.");

        suggestion.Accepted = accepted;

        // An edit is an implicit accept — you wouldn't rewrite something you're rejecting.
        if (editedText is not null)
        {
            suggestion.UserEditedText = string.IsNullOrWhiteSpace(editedText) ? null : editedText.Trim();
            if (suggestion.UserEditedText is not null && accepted is null)
            {
                suggestion.Accepted = true;
            }
        }

        await db.SaveChangesAsync(ct);
        return suggestion;
    }

    /// <summary>
    /// Applies the accepted suggestions to the base resume and saves the result as
    /// a new resume version, so you keep the original untouched and can attach the
    /// exact version you sent to the kanban card.
    /// </summary>
    public async Task<ResumeDoc> BuildTailoredResumeAsync(int sessionId, CancellationToken ct)
    {
        var session = await db.Tailorings
            .Include(s => s.Suggestions)
            .Include(s => s.ResumeDoc)
            .Include(s => s.Job)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new KeyNotFoundException($"Tailoring session {sessionId} not found.");

        var accepted = session.Suggestions.Where(s => s.Accepted == true).ToList();

        // Deep copy the sections so edits never touch the master document.
        var sections = session.ResumeDoc.Sections
            .OrderBy(s => s.Order)
            .Select(s => new ResumeSection
            {
                Kind = s.Kind,
                Heading = s.Heading,
                Subject = s.Subject,
                DateRange = s.DateRange,
                Bullets = [.. s.Bullets],
                Order = s.Order,
            })
            .ToList();

        ApplySummary(sections, session);
        ApplySkills(sections, session);
        ApplyBulletEdits(sections, accepted);

        var tailored = new ResumeDoc
        {
            Name = BuildName(session),
            IsMaster = false,
            SourceFilePath = session.ResumeDoc.SourceFilePath,
            Sections = sections,
            ExtractedSkills = [.. session.ResumeDoc.ExtractedSkills],
            YearsExperience = session.ResumeDoc.YearsExperience,
            DerivedFromTailoringId = session.Id,
            PlainText = Flatten(sections),
        };

        db.Resumes.Add(tailored);
        await db.SaveChangesAsync(ct);

        log.LogInformation(
            "Built tailored resume {Name} from {Accepted}/{Total} suggestions",
            tailored.Name, accepted.Count, session.Suggestions.Count);

        return tailored;
    }

    /// <summary>Replaces the summary prose when Claude proposed a rewrite.</summary>
    private static void ApplySummary(List<ResumeSection> sections, TailoringSession session)
    {
        if (string.IsNullOrWhiteSpace(session.TailoredSummary))
        {
            return;
        }

        var summary = sections.FirstOrDefault(s => s.Kind == ResumeSectionKind.Summary);
        if (summary is null)
        {
            sections.Insert(0, new ResumeSection
            {
                Kind = ResumeSectionKind.Summary,
                Heading = "Summary",
                Bullets = [session.TailoredSummary!],
                Order = -1,
            });
            return;
        }

        // Keep contact-detail lines; swap only the prose paragraph.
        var contact = summary.Bullets.Where(IsContactLine).ToList();
        summary.Bullets = [.. contact, session.TailoredSummary!];
    }

    /// <summary>Reorders the skills line to lead with what the job asks for.</summary>
    private static void ApplySkills(List<ResumeSection> sections, TailoringSession session)
    {
        if (string.IsNullOrWhiteSpace(session.TailoredSkillsLine))
        {
            return;
        }

        var skills = sections.FirstOrDefault(s => s.Kind == ResumeSectionKind.Skills);
        if (skills is null)
        {
            var order = sections.Count == 0 ? 0 : sections.Max(s => s.Order) + 1;
            sections.Add(new ResumeSection
            {
                Kind = ResumeSectionKind.Skills,
                Heading = "Technical Skills",
                Bullets = [session.TailoredSkillsLine!],
                Order = order,
            });
            return;
        }

        skills.Bullets = [session.TailoredSkillsLine!];
    }

    /// <summary>Swaps in each accepted bullet, or appends it when it's new.</summary>
    private static void ApplyBulletEdits(List<ResumeSection> sections, List<ResumeSuggestion> accepted)
    {
        foreach (var suggestion in accepted.OrderBy(s => s.Order))
        {
            var text = suggestion.UserEditedText ?? suggestion.SuggestedText;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            // Brand-new bullet: no original to replace.
            if (string.IsNullOrWhiteSpace(suggestion.OriginalText))
            {
                var target = FindSection(sections, suggestion);
                target?.Bullets.Add(text);
                continue;
            }

            var replaced = false;
            foreach (var section in sections)
            {
                var index = section.Bullets.FindIndex(b =>
                    string.Equals(Normalise(b), Normalise(suggestion.OriginalText), StringComparison.OrdinalIgnoreCase));

                if (index >= 0)
                {
                    section.Bullets[index] = text;
                    replaced = true;
                    break;
                }
            }

            // The model paraphrased the original instead of quoting it — append
            // rather than silently dropping an edit you explicitly accepted.
            if (!replaced)
            {
                FindSection(sections, suggestion)?.Bullets.Add(text);
            }
        }
    }

    /// <summary>Best section for a suggestion: exact heading, then kind.</summary>
    private static ResumeSection? FindSection(List<ResumeSection> sections, ResumeSuggestion suggestion)
    {
        if (!string.IsNullOrWhiteSpace(suggestion.SectionHeading))
        {
            var byHeading = sections.FirstOrDefault(s =>
                string.Equals(s.Subject, suggestion.SectionHeading, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.Heading, suggestion.SectionHeading, StringComparison.OrdinalIgnoreCase));

            if (byHeading is not null)
            {
                return byHeading;
            }
        }

        return sections.FirstOrDefault(s => s.Kind == suggestion.Section)
               ?? sections.FirstOrDefault(s => s.Kind == ResumeSectionKind.Experience);
    }

    /// <summary>Renders a resume to docx or pdf.</summary>
    public (byte[] Bytes, string ContentType, string FileName) Export(ResumeDoc resume, string format)
    {
        var renderer = renderers.FirstOrDefault(r =>
                           r.Format.Equals(format, StringComparison.OrdinalIgnoreCase))
                       ?? throw new NotSupportedException($"Unsupported format '{format}'. Try docx or pdf.");

        var safeName = string.Join("_", resume.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return (renderer.Render(resume), renderer.ContentType, $"{safeName}.{renderer.Format}");
    }

    /// <summary>"Gitesh_Resume — Project Lead at Acme".</summary>
    private static string BuildName(TailoringSession session)
    {
        var company = session.Job.Company.Length > 30 ? session.Job.Company[..30] : session.Job.Company;
        return $"{session.ResumeDoc.Name} — {company}";
    }

    /// <summary>Phone/email/address lines we keep verbatim in the summary block.</summary>
    private static bool IsContactLine(string line)
        => line.Contains('@')
           || line.Contains("+91")
           || line.StartsWith("http", StringComparison.OrdinalIgnoreCase)
           || (line.Count(char.IsDigit) >= 8 && line.Length < 60);

    private static string Normalise(string s)
        => string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Trim(' ', '.', '•', '-');

    private static string Flatten(List<ResumeSection> sections)
        => string.Join('\n', sections.OrderBy(s => s.Order).SelectMany(s =>
            new[] { s.Heading, s.Subject, s.DateRange }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .Concat(s.Bullets)));
}
