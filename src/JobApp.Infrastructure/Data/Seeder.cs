namespace JobApp.Infrastructure.Data;

using JobApp.Core.Domain;
using JobApp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// First-run setup: create the database and one starter search profile, then
/// seed it from whatever resume was imported. Everything it writes is editable
/// in the dashboard afterwards — these are defaults, not hardcoded behaviour.
/// </summary>
public class Seeder(JobAppDbContext db, ResumeService resumes, ILogger<Seeder> log)
{
    /// <summary>Runs migrations and seeds if the database is empty.</summary>
    public async Task InitialiseAsync(string? resumePathToImport, CancellationToken ct)
    {
        await db.Database.MigrateAsync(ct);

        var resume = await db.Resumes.FirstOrDefaultAsync(r => r.IsMaster, ct);

        // Import the starter resume only on a genuinely empty database.
        if (resume is null
            && !await db.Resumes.AnyAsync(ct)
            && !string.IsNullOrWhiteSpace(resumePathToImport))
        {
            try
            {
                resume = await resumes.ImportFromPathAsync(resumePathToImport, ct);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Could not import starter resume from {Path}", resumePathToImport);
            }
        }

        if (await db.SearchProfiles.AnyAsync(ct))
        {
            return;
        }

        db.SearchProfiles.Add(BuildStarterProfile(resume));
        await db.SaveChangesAsync(ct);

        log.LogInformation("Seeded starter search profile. Edit it in the dashboard under Settings.");
    }

    /// <summary>
    /// Defaults derived from the resume where possible, so the first poll returns
    /// something sensible before you have tuned anything.
    /// </summary>
    private static SearchProfile BuildStarterProfile(ResumeDoc? resume)
    {
        var years = resume?.YearsExperience;

        var profile = new SearchProfile
        {
            Name = "Default",
            IsActive = true,

            // Both ladders the resume supports; edit these in Settings.
            TargetTitles =
            [
                "Project Lead",
                "Technical Lead",
                "Engineering Manager",
                "Solution Architect",
                "Senior Full Stack Engineer",
            ],

            ExcludeKeywords = ["intern", "internship", "fresher", "trainee", "unpaid", "commission only"],

            MustHaveSkills = [".NET", "C#"],

            NiceToHaveSkills = ResolveNiceToHave(resume),

            // A 16-year lead should not be shown 3-year roles or CTO postings.
            MinYearsExperience = years is int y ? Math.Max(5, y - 6) : 8,
            MaxYearsExperience = years is int y2 ? y2 + 8 : null,

            CountryCodes = ["in"],
            HomeCity = "Bangalore",
            AcceptableCities = ["Bengaluru", "Bangalore"],
            AllowGlobalRemote = true,
            OpenToRelocation = false,
            TrackVisaSponsorship = false,

            // The priority order you asked for: remote first, 2-day hybrid close behind.
            RemoteWeight = 100,
            HybridWeight = 90,
            HybridOverLimitWeight = 45,
            OnsiteWeight = 25,
            MaxOfficeDaysPerWeek = 2,
            ExcludeOnsite = false,

            // No floor by default: most Indian listings hide salary, and a floor
            // would silently bury good roles. Set one in Settings if you want.
            MinSalary = null,
            SalaryCurrency = "INR",
            IncludeUndisclosedSalary = true,

            PostedWithinDays = 7,
            MinOverallScore = 55,
            SkillFitWeight = 55,
            LocationFitWeight = 30,
            FreshnessWeight = 15,
        };

        return profile;
    }

    /// <summary>Prefers skills actually found in the resume, capped to keep prompts lean.</summary>
    private static List<string> ResolveNiceToHave(ResumeDoc? resume)
    {
        var fallback = new List<string>
        {
            ".NET Core", "Angular", "Microservices", "Azure", "AWS", "GraphQL",
            "SQL Server", "Docker", "Kubernetes", "CQRS", "Redis", "TypeScript",
        };

        if (resume is null || resume.ExtractedSkills.Count == 0)
        {
            return fallback;
        }

        // Keep the resume's own vocabulary — that's what the matcher compares against.
        var fromResume = resume.ExtractedSkills
            .Where(s => s.Length is > 1 and < 30)
            .Take(20)
            .ToList();

        return fromResume.Count >= 6 ? fromResume : fallback;
    }
}
