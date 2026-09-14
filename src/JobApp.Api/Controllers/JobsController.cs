namespace JobApp.Api.Controllers;

using JobApp.Api.Contracts;
using JobApp.Core.Domain;
using JobApp.Infrastructure.Data;
using JobApp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

/// <summary>The holistic job feed: list, detail, manual import, and deep re-analysis.</summary>
[ApiController]
[Route("api/jobs")]
public class JobsController(
    DashboardService dashboard,
    ManualImportService manualImport,
    ScoringService scoring,
    PipelineService pipeline,
    JobAppDbContext db) : ControllerBase
{
    /// <summary>Paged, filtered job list. Every filter maps to a dashboard control.</summary>
    [HttpGet]
    public async Task<ActionResult<JobListResult>> List(
        [FromQuery] int? profileId,
        [FromQuery] int? postedWithinDays,
        [FromQuery] string? workModels,
        [FromQuery] int? minScore,
        [FromQuery] string? search,
        [FromQuery] bool withinOfficeDayTolerance,
        [FromQuery] bool needsWorkModelReview,
        [FromQuery] bool includeArchived,
        [FromQuery] bool hideTracked,
        [FromQuery] string sort = "score",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var models = (workModels ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Enum.TryParse<WorkModel>(s, ignoreCase: true, out var m) ? m : (WorkModel?)null)
            .Where(m => m is not null)
            .Select(m => m!.Value)
            .ToList();

        var query = new JobQuery
        {
            ProfileId = profileId,
            PostedWithinDays = postedWithinDays,
            WorkModels = models,
            MinScore = minScore,
            Search = search,
            WithinOfficeDayTolerance = withinOfficeDayTolerance,
            NeedsWorkModelReview = needsWorkModelReview,
            IncludeArchived = includeArchived,
            HideTracked = hideTracked,
            Sort = sort,
            Page = page,
            PageSize = pageSize,
        };

        return await dashboard.QueryAsync(query, ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<JobListItem>> Get(int id, [FromQuery] int? profileId, CancellationToken ct)
    {
        var item = await dashboard.GetAsync(id, profileId, ct);
        return item is null ? NotFound() : item;
    }

    /// <summary>Full description, loaded on demand when you expand a card.</summary>
    [HttpGet("{id:int}/description")]
    public async Task<ActionResult<string>> Description(int id, CancellationToken ct)
    {
        var description = await dashboard.GetDescriptionAsync(id, ct);
        return description is null ? NotFound() : description;
    }

    /// <summary>Paste a LinkedIn/Naukri/board URL. Falls back to a friendly message if the page can't be read.</summary>
    [HttpPost("import/url")]
    public async Task<ActionResult<Job>> ImportUrl(ImportUrlRequest req, CancellationToken ct)
    {
        var outcome = await manualImport.ImportFromUrlAsync(req.Url, ct);
        if (outcome.Job is null)
        {
            return UnprocessableEntity(new { message = outcome.Message });
        }

        return Ok(new { job = outcome.Job, alreadyExisted = outcome.AlreadyExisted, message = outcome.Message });
    }

    /// <summary>Paste the job description text directly — the reliable path for sites that block fetching.</summary>
    [HttpPost("import/text")]
    public async Task<ActionResult> ImportText(ImportTextRequest req, CancellationToken ct)
    {
        var outcome = await manualImport.ImportFromTextAsync(req.Text, req.Url, ct);
        if (outcome.Job is null)
        {
            return UnprocessableEntity(new { message = outcome.Message });
        }

        return Ok(new { job = outcome.Job, alreadyExisted = outcome.AlreadyExisted, message = outcome.Message });
    }

    /// <summary>Runs the expensive model on one job you opened deliberately.</summary>
    [HttpPost("{id:int}/deep-analyse")]
    public async Task<ActionResult<JobScore>> DeepAnalyse(int id, DeepAnalyseRequest req, CancellationToken ct)
    {
        try
        {
            return await scoring.DeepAnalyseAsync(id, req.ProfileId, ct);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
    }

    /// <summary>Corrects the AI's remote/hybrid/onsite call by hand — locks it against future overwrite.</summary>
    [HttpPut("{id:int}/work-model")]
    public async Task<ActionResult> SetWorkModel(int id, SetWorkModelRequest req, CancellationToken ct)
    {
        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == id, ct);
        if (job is null)
        {
            return NotFound();
        }

        job.WorkModel = req.WorkModel;
        job.OfficeDaysPerWeek = req.OfficeDaysPerWeek;
        job.WorkModelConfidence = 1.0;
        job.WorkModelOrigin = WorkModelOrigin.Manual;
        job.WorkModelEvidence = "Corrected manually.";

        await db.SaveChangesAsync(ct);

        // Location score depends on work model, so refresh every profile's score for this job.
        foreach (var score in await db.JobScores.Where(s => s.JobId == id).ToListAsync(ct))
        {
            await scoring.RescoreDeterministicAsync(score.SearchProfileId, ct);
        }

        return NoContent();
    }

    /// <summary>Removes a job from view and remembers you don't want it back.</summary>
    [HttpPost("{id:int}/dismiss")]
    public async Task<ActionResult> Dismiss(int id, CancellationToken ct)
    {
        try
        {
            await pipeline.DismissJobAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
