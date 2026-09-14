namespace JobApp.Infrastructure.Services;

using JobApp.Core.Abstractions;
using JobApp.Core.Domain;
using JobApp.Core.Scoring;
using JobApp.Infrastructure.Ai;
using JobApp.Infrastructure.Data;
using JobApp.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>Outcome of a triage pass.</summary>
public record TriageReport(int Scored, int Failed, int Skipped, decimal CostUsd, List<string> Warnings);

/// <summary>
/// Runs Claude over unscored jobs. Cheap model by default; the expensive model
/// only on explicit request for a single job.
/// </summary>
public class ScoringService(
    JobAppDbContext db,
    IJobAnalyst analyst,
    SpendGuard spend,
    IOptions<ClaudeOptions> claudeOptions,
    ILogger<ScoringService> log)
{
    private readonly ClaudeOptions opts = claudeOptions.Value;

    /// <summary>Scores every unscored, unarchived job for each active profile.</summary>
    public async Task<TriageReport> TriagePendingAsync(CancellationToken ct)
    {
        var warnings = new List<string>();

        var resume = await MasterResumeAsync(ct);
        if (resume is null)
        {
            return new TriageReport(0, 0, 0, 0m, ["No master resume uploaded — cannot score fit."]);
        }

        var profiles = await db.SearchProfiles.Where(p => p.IsActive).ToListAsync(ct);
        if (profiles.Count == 0)
        {
            return new TriageReport(0, 0, 0, 0m, ["No active search profile configured."]);
        }

        var scored = 0;
        var failed = 0;
        var skipped = 0;
        var cost = 0m;
        var budget = opts.MaxTriagePerCycle;

        foreach (var profile in profiles)
        {
            if (budget <= 0)
            {
                break;
            }

            // Newest first: if the budget runs out, it runs out on old jobs.
            var pending = await db.Jobs
                .Where(j => !j.IsArchived && !j.Scores.Any(s => s.SearchProfileId == profile.Id))
                .OrderByDescending(j => j.PostedAt ?? j.DiscoveredAt)
                .Take(budget)
                .ToListAsync(ct);

            foreach (var job in pending)
            {
                ct.ThrowIfCancellationRequested();

                if (!await spend.CanSpendAsync(ct))
                {
                    warnings.Add("Monthly Claude budget reached — remaining jobs left unscored.");
                    skipped += pending.Count - scored;
                    return new TriageReport(scored, failed, skipped, cost, warnings);
                }

                try
                {
                    var response = await analyst.TriageAsync(job, profile, resume.PlainText, ct);
                    await ApplyAsync(job, profile, response.Value, deep: false, opts.TriageModel, ct);
                    await spend.RecordAsync(AiPurpose.Triage, response.Usage, job.Id, ct);

                    cost += response.Usage.CostUsd;
                    scored++;
                    budget--;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Triage failed for job {JobId} ({Title})", job.Id, job.Title);
                    await spend.RecordFailureAsync(AiPurpose.Triage, opts.TriageModel, ex.Message, job.Id, ct);
                    failed++;
                    budget--;
                }
            }
        }

        return new TriageReport(scored, failed, skipped, cost, warnings);
    }

    /// <summary>Re-reads one job with the expensive model, on user request.</summary>
    public async Task<JobScore> DeepAnalyseAsync(int jobId, int profileId, CancellationToken ct)
    {
        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, ct)
                  ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        var profile = await db.SearchProfiles.FirstOrDefaultAsync(p => p.Id == profileId, ct)
                      ?? throw new KeyNotFoundException($"Search profile {profileId} not found.");

        var resume = await MasterResumeAsync(ct)
                     ?? throw new InvalidOperationException("Upload a master resume first.");

        if (!await spend.CanSpendAsync(ct))
        {
            throw new InvalidOperationException("Monthly Claude budget reached. Raise it in settings to continue.");
        }

        var response = await analyst.DeepFitAsync(job, profile, resume.PlainText, ct);
        var score = await ApplyAsync(job, profile, response.Value, deep: true, opts.TailoringModel, ct);
        await spend.RecordAsync(AiPurpose.DeepFit, response.Usage, job.Id, ct);

        return score;
    }

    /// <summary>Re-scores every job for a profile after you change its settings.</summary>
    public async Task<int> RescoreDeterministicAsync(int profileId, CancellationToken ct)
    {
        var profile = await db.SearchProfiles.FirstOrDefaultAsync(p => p.Id == profileId, ct)
                      ?? throw new KeyNotFoundException($"Search profile {profileId} not found.");

        var scores = await db.JobScores
            .Include(s => s.Job)
            .Where(s => s.SearchProfileId == profileId)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;

        // Location, freshness and salary are free to recompute — no AI needed.
        foreach (var score in scores)
        {
            score.LocationScore = MatchScorer.LocationScore(score.Job, profile);
            score.FreshnessScore = MatchScorer.FreshnessScore(score.Job, profile, now);
            score.SalaryScore = MatchScorer.SalaryScore(score.Job, profile);
            score.OverallScore = MatchScorer.Blend(score, profile, score.Job);
        }

        await db.SaveChangesAsync(ct);
        log.LogInformation("Re-scored {Count} jobs for profile {Profile}", scores.Count, profile.Name);
        return scores.Count;
    }

    /// <summary>Writes the AI verdict onto the job and upserts its score row.</summary>
    private async Task<JobScore> ApplyAsync(
        Job job, SearchProfile profile, Core.Contracts.TriageResult triage, bool deep, string model, CancellationToken ct)
    {
        // Never let the AI overwrite a classification you corrected by hand.
        if (job.WorkModelOrigin != WorkModelOrigin.Manual
            && triage.WorkModelConfidence >= job.WorkModelConfidence)
        {
            job.WorkModel = triage.WorkModel;
            job.OfficeDaysPerWeek = triage.OfficeDaysPerWeek;
            job.WorkModelConfidence = triage.WorkModelConfidence;
            job.WorkModelEvidence = triage.WorkModelEvidence;
            job.WorkModelOrigin = WorkModelOrigin.Ai;
        }

        job.RequiredYearsExperience ??= triage.RequiredYearsExperience;
        job.MentionsVisaSponsorship ??= triage.MentionsVisaSponsorship;

        var fresh = MatchScorer.Score(job, profile, triage, deep, model, DateTimeOffset.UtcNow);

        var existing = await db.JobScores
            .FirstOrDefaultAsync(s => s.JobId == job.Id && s.SearchProfileId == profile.Id, ct);

        if (existing is null)
        {
            db.JobScores.Add(fresh);
            await db.SaveChangesAsync(ct);
            return fresh;
        }

        // Don't let a cheap re-triage clobber a deep analysis you paid for.
        if (existing.IsDeepAnalysis && !deep)
        {
            return existing;
        }

        existing.OverallScore = fresh.OverallScore;
        existing.SkillScore = fresh.SkillScore;
        existing.SeniorityScore = fresh.SeniorityScore;
        existing.LocationScore = fresh.LocationScore;
        existing.FreshnessScore = fresh.FreshnessScore;
        existing.SalaryScore = fresh.SalaryScore;
        existing.MatchedSkills = fresh.MatchedSkills;
        existing.MissingMustHaves = fresh.MissingMustHaves;
        existing.MissingResumeKeywords = fresh.MissingResumeKeywords;
        existing.RedFlags = fresh.RedFlags;
        existing.Rationale = fresh.Rationale;
        existing.IsDeepAnalysis = deep;
        existing.ModelUsed = model;
        existing.ScoredAt = fresh.ScoredAt;

        await db.SaveChangesAsync(ct);
        return existing;
    }

    private Task<ResumeDoc?> MasterResumeAsync(CancellationToken ct)
        => db.Resumes
            .Where(r => !r.IsCoverLetter)
            .OrderByDescending(r => r.IsMaster)
            .ThenByDescending(r => r.UpdatedAt)
            .FirstOrDefaultAsync(ct);
}
