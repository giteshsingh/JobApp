namespace JobApp.Api.Controllers;

using JobApp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

/// <summary>The overview panel: counts, spend, pipeline snapshot, top matches, sparkline.</summary>
[ApiController]
[Route("api/dashboard")]
public class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet("summary")]
    public Task<DashboardSummary> Summary([FromQuery] int? profileId, CancellationToken ct)
        => dashboard.SummaryAsync(profileId, ct);
}
