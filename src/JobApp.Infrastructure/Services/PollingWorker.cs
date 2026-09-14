namespace JobApp.Infrastructure.Services;

using JobApp.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Hourly (configurable) background loop: fetch new jobs, then triage the ones
/// that need it. Runs in its own DI scope per cycle since the DbContext isn't
/// thread-safe across the app's request scopes.
/// </summary>
public class PollingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<IngestionOptions> options,
    ILogger<PollingWorker> log) : BackgroundService
{
    private readonly IngestionOptions opts = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!opts.Enabled)
        {
            log.LogInformation("Background ingestion disabled via config.");
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, opts.StartupDelaySeconds)), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(5, opts.PollIntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken);

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>One fetch-then-triage pass, isolated so a bad cycle doesn't kill the loop.</summary>
    private async Task RunCycleAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var ingestion = scope.ServiceProvider.GetRequiredService<IngestionService>();
        var scoring = scope.ServiceProvider.GetRequiredService<ScoringService>();

        try
        {
            var report = await ingestion.IngestAllAsync(ct);
            log.LogInformation(
                "Ingestion cycle: {Fetched} fetched, {Inserted} new, {Dup} duplicates, {Exc} excluded, {Old} stale. {Warnings}",
                report.Fetched, report.Inserted, report.Duplicates, report.Excluded, report.TooOld,
                report.Warnings.Count > 0 ? string.Join("; ", report.Warnings) : "no warnings");

            var archived = await ingestion.ArchiveStaleAsync(ct);
            if (archived > 0)
            {
                log.LogInformation("Archived {Count} stale untouched jobs.", archived);
            }

            var triage = await scoring.TriagePendingAsync(ct);
            log.LogInformation(
                "Triage cycle: {Scored} scored, {Failed} failed, {Skipped} skipped, ${Cost:F4} spent. {Warnings}",
                triage.Scored, triage.Failed, triage.Skipped, triage.CostUsd,
                triage.Warnings.Count > 0 ? string.Join("; ", triage.Warnings) : "no warnings");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A bad cycle should not take the whole app down — try again next tick.
            log.LogError(ex, "Polling cycle failed");
        }
    }
}
