namespace JobApp.Api.Controllers;

using JobApp.Api.Contracts;
using JobApp.Core.Domain;
using JobApp.Infrastructure.Data;
using JobApp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

/// <summary>Search profiles — the fully user-configurable "what am I looking for" settings.</summary>
[ApiController]
[Route("api/profiles")]
public class ProfilesController(JobAppDbContext db, ScoringService scoring) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SearchProfileDto>>> List(CancellationToken ct)
        => await db.SearchProfiles.AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => SearchProfileDto.From(p))
            .ToListAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SearchProfileDto>> Get(int id, CancellationToken ct)
    {
        var profile = await db.SearchProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        return profile is null ? NotFound() : SearchProfileDto.From(profile);
    }

    [HttpPost]
    public async Task<ActionResult<SearchProfileDto>> Create(SearchProfileDto dto, CancellationToken ct)
    {
        var profile = new SearchProfile();
        dto.ApplyTo(profile);
        db.SearchProfiles.Add(profile);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = profile.Id }, SearchProfileDto.From(profile));
    }

    /// <summary>Updates the profile and re-runs the free deterministic scoring so changes show immediately.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<SearchProfileDto>> Update(int id, SearchProfileDto dto, CancellationToken ct)
    {
        var profile = await db.SearchProfiles.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (profile is null)
        {
            return NotFound();
        }

        dto.ApplyTo(profile);
        await db.SaveChangesAsync(ct);

        // Location/freshness/salary weights are the ones people tune repeatedly —
        // recompute them for free instead of waiting for the next AI triage cycle.
        await scoring.RescoreDeterministicAsync(id, ct);

        return SearchProfileDto.From(profile);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var profile = await db.SearchProfiles.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (profile is null)
        {
            return NotFound();
        }

        if (await db.SearchProfiles.CountAsync(ct) <= 1)
        {
            return BadRequest("At least one search profile must exist.");
        }

        db.SearchProfiles.Remove(profile);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
