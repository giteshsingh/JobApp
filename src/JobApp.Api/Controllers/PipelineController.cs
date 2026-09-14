namespace JobApp.Api.Controllers;

using JobApp.Api.Contracts;
using JobApp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

/// <summary>Kanban: Saved → Applied → Screen → Interview → Offer/Rejected.</summary>
[ApiController]
[Route("api/pipeline")]
public class PipelineController(PipelineService pipeline) : ControllerBase
{
    [HttpGet("board")]
    public Task<Dictionary<string, List<PipelineCard>>> Board([FromQuery] int? profileId, CancellationToken ct)
        => pipeline.BoardAsync(profileId, ct);

    [HttpPost("jobs/{jobId:int}/track")]
    public async Task<ActionResult<PipelineCard>> Track(int jobId, TrackJobRequest req, CancellationToken ct)
    {
        try
        {
            await pipeline.TrackAsync(jobId, req.Stage, ct);
            var board = await pipeline.BoardAsync(null, ct);
            var card = board.Values.SelectMany(x => x).FirstOrDefault(c => c.JobId == jobId);
            return card is null ? NotFound() : card;
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("{applicationId:int}/move")]
    public async Task<ActionResult> Move(int applicationId, MoveCardRequest req, CancellationToken ct)
    {
        try
        {
            await pipeline.MoveAsync(applicationId, req.Stage, req.SortOrder, req.Note, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("{applicationId:int}")]
    public async Task<ActionResult> Update(int applicationId, UpdateCardRequest req, CancellationToken ct)
    {
        try
        {
            await pipeline.UpdateAsync(applicationId, req.Notes, req.NextAction, req.NextActionAt, req.ResumeDocId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("{applicationId:int}")]
    public async Task<ActionResult> Remove(int applicationId, CancellationToken ct)
    {
        try
        {
            await pipeline.RemoveAsync(applicationId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
