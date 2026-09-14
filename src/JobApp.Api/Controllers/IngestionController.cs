namespace JobApp.Api.Controllers;

using JobApp.Api.Contracts;
using JobApp.Infrastructure.Ai;
using JobApp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

/// <summary>Manual controls for the background pipeline, and the Claude spend meter.</summary>
[ApiController]
[Route("api/ingestion")]
public class IngestionController(IngestionService ingestion, ScoringService scoring, SpendGuard spend) : ControllerBase
{
    /// <summary>The "Refresh now" button — runs one fetch cycle immediately.</summary>
    [HttpPost("run")]
    public async Task<ActionResult<IngestionReportDto>> Run(CancellationToken ct)
    {
        var report = await ingestion.IngestAllAsync(ct);
        return new IngestionReportDto(report.Fetched, report.Inserted, report.Duplicates, report.Excluded, report.TooOld, report.BySource, report.Warnings);
    }

    /// <summary>Triages whatever is currently unscored, up to the configured per-cycle budget.</summary>
    [HttpPost("triage")]
    public async Task<ActionResult<TriageReportDto>> Triage(CancellationToken ct)
    {
        var report = await scoring.TriagePendingAsync(ct);
        return new TriageReportDto(report.Scored, report.Failed, report.Skipped, report.CostUsd, report.Warnings);
    }

    /// <summary>Month-to-date Claude spend, for the budget meter in Settings.</summary>
    [HttpGet("spend")]
    public async Task<ActionResult<object>> Spend(CancellationToken ct)
        => new { monthToDateUsd = await spend.MonthToDateAsync(ct) };
}
