namespace JobApp.Api.Controllers;

using JobApp.Api.Contracts;
using JobApp.Infrastructure.Data;
using JobApp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

/// <summary>Upload, list, and manage the resumes/cover letters the tailoring flow works from.</summary>
[ApiController]
[Route("api/resumes")]
public class ResumesController(ResumeService resumes, JobAppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ResumeSummaryDto>>> List(CancellationToken ct)
        => await db.Resumes.AsNoTracking()
            .OrderByDescending(r => r.IsMaster)
            .ThenByDescending(r => r.UpdatedAt)
            .Select(r => ResumeSummaryDto.From(r))
            .ToListAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ResumeDetailDto>> Get(int id, CancellationToken ct)
    {
        var resume = await db.Resumes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return resume is null ? NotFound() : ResumeDetailDto.From(resume);
    }

    /// <summary>Accepts a .docx, .txt or .md file upload.</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<ResumeSummaryDto>> Upload(
        IFormFile file, [FromQuery] bool isCoverLetter, CancellationToken ct)
    {
        if (file.Length == 0)
        {
            return BadRequest(new { message = "No file received." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var resume = await resumes.ImportAsync(stream, file.FileName, isCoverLetter, ct);
            return CreatedAtAction(nameof(Get), new { id = resume.Id }, ResumeSummaryDto.From(resume));
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/set-master")]
    public async Task<ActionResult<ResumeSummaryDto>> SetMaster(int id, CancellationToken ct)
    {
        try
        {
            var resume = await resumes.SetMasterAsync(id, ct);
            return ResumeSummaryDto.From(resume);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            await resumes.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
