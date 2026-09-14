namespace JobApp.Infrastructure.Ai;

using JobApp.Core.Abstractions;
using JobApp.Core.Contracts;
using JobApp.Core.Domain;
using JobApp.Infrastructure.Data;
using JobApp.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Records every Claude call and enforces the optional monthly USD ceiling, so
/// hourly background polling cannot quietly run up a bill.
/// </summary>
public class SpendGuard(
    JobAppDbContext db,
    IOptions<ClaudeOptions> options,
    ILogger<SpendGuard> log) : ISpendGuard
{
    private readonly ClaudeOptions opts = options.Value;

    /// <summary>False once this calendar month's spend has hit the configured cap.</summary>
    public async Task<bool> CanSpendAsync(CancellationToken ct)
    {
        if (opts.MonthlyBudgetUsd is not decimal cap || cap <= 0)
        {
            return true; // no cap configured
        }

        var spent = await MonthToDateAsync(ct);
        if (spent < cap)
        {
            return true;
        }

        log.LogWarning("Claude monthly budget reached: {Spent:C} of {Cap:C}. Pausing AI calls.", spent, cap);
        return false;
    }

    /// <summary>Total USD spent since the start of the current UTC month.</summary>
    public async Task<decimal> MonthToDateAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

        return await db.AiUsage
            .Where(u => u.At >= monthStart)
            .SumAsync(u => u.CostUsd, ct);
    }

    public async Task RecordAsync(AiPurpose purpose, AiCallUsage usage, int? jobId, CancellationToken ct)
    {
        db.AiUsage.Add(new AiUsageRecord
        {
            Purpose = purpose,
            Model = usage.Model,
            InputTokens = usage.InputTokens,
            OutputTokens = usage.OutputTokens,
            CacheReadTokens = usage.CacheReadTokens,
            CacheWriteTokens = usage.CacheWriteTokens,
            CostUsd = usage.CostUsd,
            JobId = jobId,
            Succeeded = true,
        });

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Logs a failed call so the dashboard can show error rates.</summary>
    public async Task RecordFailureAsync(AiPurpose purpose, string model, string error, int? jobId, CancellationToken ct)
    {
        db.AiUsage.Add(new AiUsageRecord
        {
            Purpose = purpose,
            Model = model,
            CostUsd = 0m,
            JobId = jobId,
            Succeeded = false,
            Error = error.Length > 1000 ? error[..1000] : error,
        });

        await db.SaveChangesAsync(ct);
    }
}
