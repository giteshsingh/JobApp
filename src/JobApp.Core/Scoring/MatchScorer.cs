namespace JobApp.Core.Scoring;

using JobApp.Core.Contracts;
using JobApp.Core.Domain;

/// <summary>
/// Blends Claude's skill/seniority judgement with deterministic location,
/// freshness and salary maths. Deterministic parts live here (not in a prompt)
/// so the numbers are stable, free, and explainable.
/// </summary>
public static class MatchScorer
{
    /// <summary>Builds the score row for a job/profile pair.</summary>
    public static JobScore Score(Job job, SearchProfile profile, TriageResult triage, bool deep, string model, DateTimeOffset now)
    {
        var location = LocationScore(job, profile);
        var freshness = FreshnessScore(job, profile, now);
        var salary = SalaryScore(job, profile);

        var score = new JobScore
        {
            JobId = job.Id,
            SearchProfileId = profile.Id,
            SkillScore = Clamp(triage.SkillScore),
            SeniorityScore = Clamp(triage.SeniorityScore),
            LocationScore = location,
            FreshnessScore = freshness,
            SalaryScore = salary,
            MatchedSkills = [.. triage.MatchedSkills],
            MissingMustHaves = [.. triage.MissingMustHaves],
            MissingResumeKeywords = [.. triage.MissingResumeKeywords],
            RedFlags = [.. triage.RedFlags],
            Rationale = triage.Rationale,
            IsDeepAnalysis = deep,
            ModelUsed = model,
            ScoredAt = now,
        };

        score.OverallScore = Blend(score, profile, job);
        return score;
    }

    /// <summary>Weighted blend, then hard penalties. Always lands in 0-100.</summary>
    public static int Blend(JobScore s, SearchProfile profile, Job job)
    {
        // Skill and seniority share the skill-fit weight, skill dominating.
        var skillFit = (s.SkillScore * 0.7) + (s.SeniorityScore * 0.3);

        var wSkill = Math.Max(0, profile.SkillFitWeight);
        var wLoc = Math.Max(0, profile.LocationFitWeight);
        var wFresh = Math.Max(0, profile.FreshnessWeight);
        var total = wSkill + wLoc + wFresh;
        if (total == 0)
        {
            return Clamp((int)Math.Round(skillFit));
        }

        var blended = ((skillFit * wSkill) + (s.LocationScore * wLoc) + (s.FreshnessScore * wFresh)) / total;

        // Salary nudges rather than dominates — most listings hide it.
        blended = (blended * 0.9) + (s.SalaryScore * 0.1);

        // Each unmet must-have skill is a real disqualifier.
        blended -= s.MissingMustHaves.Count * 12;

        // Red flags are softer signals (contract-to-hire, unpaid, vague scope).
        blended -= s.RedFlags.Count * 3;

        // An unclassified work model shouldn't outrank a confirmed match.
        if (job.WorkModel == WorkModel.Unknown)
        {
            blended -= 5;
        }

        return Clamp((int)Math.Round(blended));
    }

    /// <summary>Work-model preference crossed with commute reality.</summary>
    public static int LocationScore(Job job, SearchProfile profile)
    {
        var modelScore = job.WorkModel switch
        {
            WorkModel.Remote => profile.RemoteWeight,
            WorkModel.Hybrid => HybridScore(job, profile),
            WorkModel.Onsite => profile.OnsiteWeight,
            // Unknown sits between hybrid-over-limit and onsite rather than guessing.
            _ => (profile.HybridOverLimitWeight + profile.OnsiteWeight) / 2,
        };

        // Fully remote roles have no commute to discount.
        if (job.WorkModel == WorkModel.Remote)
        {
            return Clamp(modelScore);
        }

        return Clamp((int)Math.Round(modelScore * CityFactor(job, profile)));
    }

    private static int HybridScore(Job job, SearchProfile profile)
    {
        // JD stated the days: reward inside your tolerance, discount outside it.
        if (job.OfficeDaysPerWeek is int days)
        {
            return days <= profile.MaxOfficeDaysPerWeek
                ? profile.HybridWeight
                : profile.HybridOverLimitWeight;
        }

        // Days unstated. Interpolate by how sure Claude was it's hybrid at all.
        var confidence = Math.Clamp(job.WorkModelConfidence, 0d, 1d);
        return (int)Math.Round(profile.HybridOverLimitWeight
            + ((profile.HybridWeight - profile.HybridOverLimitWeight) * confidence));
    }

    /// <summary>Multiplier for how workable the job's city is for you.</summary>
    private static double CityFactor(Job job, SearchProfile profile)
    {
        var haystack = $"{job.City} {job.LocationRaw}";
        if (string.IsNullOrWhiteSpace(haystack.Trim()))
        {
            return 0.6; // unknown location, neither reward nor bury
        }

        if (Mentions(haystack, profile.HomeCity))
        {
            return 1.0;
        }

        if (profile.AcceptableCities.Any(c => Mentions(haystack, c)))
        {
            return 0.85;
        }

        // Somewhere you'd have to move to.
        return profile.OpenToRelocation ? 0.5 : 0.15;
    }

    /// <summary>Linear decay across the profile's freshness window.</summary>
    public static int FreshnessScore(Job job, SearchProfile profile, DateTimeOffset now)
    {
        var window = Math.Max(1, profile.PostedWithinDays);

        // No posted date means we only know when we found it — discount for that.
        var posted = job.PostedAt;
        var penalty = posted is null ? 15 : 0;
        posted ??= job.DiscoveredAt;

        var ageDays = (now - posted.Value).TotalDays;
        if (ageDays <= 0)
        {
            return Clamp(100 - penalty);
        }

        if (ageDays >= window)
        {
            // Older than the window: floor it rather than zero it, so a great
            // 10-day-old role still surfaces below the fresh ones.
            var overflow = Math.Min(1d, (ageDays - window) / window);
            return Clamp((int)Math.Round(35 * (1 - overflow)) - penalty);
        }

        return Clamp((int)Math.Round(100 - (65 * (ageDays / window))) - penalty);
    }

    /// <summary>Salary vs your floor. Neutral when the listing hides it.</summary>
    public static int SalaryScore(Job job, SearchProfile profile)
    {
        if (profile.MinSalary is not decimal floor || floor <= 0)
        {
            return 100; // no filter configured
        }

        var offered = job.SalaryMax ?? job.SalaryMin;
        if (offered is null || offered <= 0)
        {
            return profile.IncludeUndisclosedSalary ? 50 : 0;
        }

        // Currency mismatch is not something we can honestly compare.
        if (!string.IsNullOrWhiteSpace(job.SalaryCurrency)
            && !job.SalaryCurrency.Equals(profile.SalaryCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return 60;
        }

        var ratio = (double)(offered.Value / floor);
        var raw = job.SalaryIsEstimate ? ratio * 0.9 : ratio;
        return Clamp((int)Math.Round(Math.Min(1.3, raw) / 1.3 * 100));
    }

    /// <summary>True when a profile exclusion keyword appears in the title or JD.</summary>
    public static bool IsExcluded(Job job, SearchProfile profile, out string? matched)
    {
        foreach (var kw in profile.ExcludeKeywords)
        {
            if (Mentions(job.Title, kw) || Mentions(job.Description, kw))
            {
                matched = kw;
                return true;
            }
        }

        matched = null;
        return false;
    }

    private static bool Mentions(string? haystack, string? needle)
        => !string.IsNullOrWhiteSpace(haystack)
           && !string.IsNullOrWhiteSpace(needle)
           && haystack.Contains(needle.Trim(), StringComparison.OrdinalIgnoreCase);

    private static int Clamp(int v) => Math.Clamp(v, 0, 100);
}
