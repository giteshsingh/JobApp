namespace JobApp.Api.Controllers;

using JobApp.Api.Contracts;
using JobApp.Infrastructure.Data;
using JobApp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

/// <summary>"Customise my resume for this job": run tailoring, review the diff, export the result.</summary>
[ApiController]
[Route("api/tailoring")]
public class TailoringController(TailoringService tailoring, JobAppDbContext db) : ControllerBase
{
    /// <summary>Kicks off a tailoring run against one job. Costs one Sonnet call.</summary>
    [HttpPost]
    public async Task<ActionResult<TailoringSessionDto>> Create(CreateTailoringRequest req, CancellationToken ct)
    {
        try
        {
            var session = await tailoring.CreateAsync(req.JobId, req.ResumeId, req.IncludeCoverLetter, ct);
            return CreatedAtAction(nameof(Get), new { id = session.Id }, TailoringSessionDto.From(session));
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

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TailoringSessionDto>> Get(int id, CancellationToken ct)
    {
        var session = await db.Tailorings.AsNoTracking()
            .Include(s => s.Suggestions)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        return session is null ? NotFound() : TailoringSessionDto.From(session);
    }

    [HttpGet("by-job/{jobId:int}")]
    public async Task<ActionResult<List<TailoringSessionDto>>> ByJob(int jobId, CancellationToken ct)
        => await db.Tailorings.AsNoTracking()
            .Include(s => s.Suggestions)
            .Where(s => s.JobId == jobId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => TailoringSessionDto.From(s))
            .ToListAsync(ct);

    /// <summary>Accept, reject, or hand-edit one suggested bullet.</summary>
    [HttpPut("suggestions/{suggestionId:int}")]
    public async Task<ActionResult<SuggestionDto>> Decide(int suggestionId, DecideSuggestionRequest req, CancellationToken ct)
    {
        try
        {
            var suggestion = await tailoring.DecideAsync(suggestionId, req.Accepted, req.EditedText, ct);
            return SuggestionDto.From(suggestion);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Bakes the accepted suggestions into a new resume version.</summary>
    [HttpPost("{id:int}/build")]
    public async Task<ActionResult<ResumeSummaryDto>> Build(int id, CancellationToken ct)
    {
        try
        {
            var resume = await tailoring.BuildTailoredResumeAsync(id, ct);
            return ResumeSummaryDto.From(resume);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Downloads a resume as docx or pdf.</summary>
    [HttpGet("resumes/{resumeId:int}/export")]
    public async Task<IActionResult> Export(int resumeId, [FromQuery] string format, CancellationToken ct)
    {
        var resume = await db.Resumes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == resumeId, ct);
        if (resume is null)
        {
            return NotFound();
        }

        try
        {
            var (bytes, contentType, fileName) = tailoring.Export(resume, format);
            return File(bytes, contentType, fileName);
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
