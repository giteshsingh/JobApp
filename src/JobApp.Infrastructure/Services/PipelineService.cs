namespace JobApp.Infrastructure.Services;

using JobApp.Core.Domain;
using JobApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

/// <summary>A kanban card as the board renders it.</summary>
public record PipelineCard(
    int Id,
    int JobId,
    string Title,
    string Company,
    string? Location,
    string? Url,
    string WorkModel,
    int? OfficeDaysPerWeek,
    int? OverallScore,
    string Stage,
    int SortOrder,
    DateTimeOffset? AppliedAt,
    int? DaysInStage,
    int? ResumeDocId,
    string? ResumeName,
    string? Notes,
    string? NextAction,
    DateTimeOffset? NextActionAt);

/// <summary>Saved → Applied → Screen → Interview → Offer/Rejected tracking.</summary>
public class PipelineService(JobAppDbContext db)
{
    /// <summary>All cards grouped by stage, ordered as the board shows them.</summary>
    public async Task<Dictionary<string, List<PipelineCard>>> BoardAsync(int? profileId, CancellationToken ct)
    {
        var rows = await db.Applications.AsNoTracking()
            .Include(a => a.Job)
            .Include(a => a.ResumeDoc)
            .Select(a => new
            {
                App = a,
                Score = profileId == null
                    ? null
                    : a.Job.Scores.FirstOrDefault(s => s.SearchProfileId == profileId),
                LastChange = a.History.OrderByDescending(h => h.At).Select(h => (DateTimeOffset?)h.At).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var board = Enum.GetValues<ApplicationStage>()
            .ToDictionary(s => s.ToString(), _ => new List<PipelineCard>());

        foreach (var row in rows)
        {
            var a = row.App;
            var since = row.LastChange ?? a.CreatedAt;

            board[a.Stage.ToString()].Add(new PipelineCard(
                a.Id,
                a.JobId,
                a.Job.Title,
                a.Job.Company,
                a.Job.LocationRaw,
                a.Job.Url,
                a.Job.WorkModel.ToString(),
                a.Job.OfficeDaysPerWeek,
                row.Score?.OverallScore,
                a.Stage.ToString(),
                a.SortOrder,
                a.AppliedAt,
                (int)Math.Floor((DateTimeOffset.UtcNow - since).TotalDays),
                a.ResumeDocId,
                a.ResumeDoc?.Name,
                a.Notes,
                a.NextAction,
                a.NextActionAt));
        }

        foreach (var column in board.Values)
        {
            column.Sort((x, y) => x.SortOrder != y.SortOrder
                ? x.SortOrder.CompareTo(y.SortOrder)
                : (y.OverallScore ?? 0).CompareTo(x.OverallScore ?? 0));
        }

        return board;
    }

    /// <summary>Adds a job to the board, or returns the card it already has.</summary>
    public async Task<JobApplication> TrackAsync(int jobId, ApplicationStage stage, CancellationToken ct)
    {
        var existing = await db.Applications.FirstOrDefaultAsync(a => a.JobId == jobId, ct);
        if (existing is not null)
        {
            return existing;
        }

        if (!await db.Jobs.AnyAsync(j => j.Id == jobId, ct))
        {
            throw new KeyNotFoundException($"Job {jobId} not found.");
        }

        // New cards go to the top of their column.
        var minOrder = await db.Applications
            .Where(a => a.Stage == stage)
            .Select(a => (int?)a.SortOrder)
            .MinAsync(ct) ?? 0;

        var app = new JobApplication
        {
            JobId = jobId,
            Stage = stage,
            SortOrder = minOrder - 1,
            AppliedAt = stage >= ApplicationStage.Applied ? DateTimeOffset.UtcNow : null,
        };

        app.History.Add(new StageChange { From = stage, To = stage, Note = "Added to pipeline" });

        db.Applications.Add(app);
        await db.SaveChangesAsync(ct);
        return app;
    }

    /// <summary>Moves a card, recording the transition.</summary>
    public async Task<JobApplication> MoveAsync(int applicationId, ApplicationStage to, int? sortOrder, string? note, CancellationToken ct)
    {
        var app = await db.Applications.FirstOrDefaultAsync(a => a.Id == applicationId, ct)
                  ?? throw new KeyNotFoundException($"Application {applicationId} not found.");

        if (app.Stage != to)
        {
            app.History.Add(new StageChange { From = app.Stage, To = to, Note = note });
            app.Stage = to;

            // Stamp the apply date the first time it reaches Applied or beyond.
            if (to >= ApplicationStage.Applied && to != ApplicationStage.Dismissed && app.AppliedAt is null)
            {
                app.AppliedAt = DateTimeOffset.UtcNow;
            }
        }

        if (sortOrder is int order)
        {
            app.SortOrder = order;
        }

        app.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return app;
    }

    /// <summary>Updates the editable fields on a card. Nulls are left untouched.</summary>
    public async Task<JobApplication> UpdateAsync(
        int applicationId,
        string? notes,
        string? nextAction,
        DateTimeOffset? nextActionAt,
        int? resumeDocId,
        CancellationToken ct)
    {
        var app = await db.Applications.FirstOrDefaultAsync(a => a.Id == applicationId, ct)
                  ?? throw new KeyNotFoundException($"Application {applicationId} not found.");

        if (notes is not null)
        {
            app.Notes = notes;
        }

        if (nextAction is not null)
        {
            app.NextAction = nextAction;
        }

        if (nextActionAt is not null)
        {
            app.NextActionAt = nextActionAt;
        }

        if (resumeDocId is int id)
        {
            if (!await db.Resumes.AnyAsync(r => r.Id == id, ct))
            {
                throw new KeyNotFoundException($"Resume {id} not found.");
            }

            app.ResumeDocId = id;
        }

        app.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return app;
    }

    /// <summary>Removes a card from the board. The job itself stays.</summary>
    public async Task RemoveAsync(int applicationId, CancellationToken ct)
    {
        var app = await db.Applications.FirstOrDefaultAsync(a => a.Id == applicationId, ct)
                  ?? throw new KeyNotFoundException($"Application {applicationId} not found.");

        db.Applications.Remove(app);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Dismissing archives the job too, so it stops coming back in the feed —
    /// the thing that makes a job dashboard usable after a few weeks.
    /// </summary>
    public async Task DismissJobAsync(int jobId, CancellationToken ct)
    {
        var job = await db.Jobs.Include(j => j.Application)
                      .FirstOrDefaultAsync(j => j.Id == jobId, ct)
                  ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        job.IsArchived = true;

        if (job.Application is null)
        {
            db.Applications.Add(new JobApplication { JobId = jobId, Stage = ApplicationStage.Dismissed });
        }
        else if (job.Application.Stage != ApplicationStage.Dismissed)
        {
            job.Application.History.Add(new StageChange
            {
                From = job.Application.Stage,
                To = ApplicationStage.Dismissed,
                Note = "Dismissed from feed",
            });
            job.Application.Stage = ApplicationStage.Dismissed;
        }

        await db.SaveChangesAsync(ct);
    }
}
