namespace JobApp.Infrastructure.Ai;

using JobApp.Core.Contracts;

/// <summary>
/// Rates used for the spend dashboard and the monthly cap.
///
/// IMPORTANT: these defaults are estimates, and the Opus figure is deliberately
/// conservative (high) so the budget cap fails safe rather than overspending.
/// Verify against https://platform.claude.com/pricing and override via the
/// "Claude:Pricing" config section — nothing here affects what you are actually
/// billed, only what the dashboard reports and when the cap trips.
/// </summary>
public class ClaudePricing
{
    public const string Section = "Claude:Pricing";

    /// <summary>USD per million tokens, keyed by model id prefix.</summary>
    public Dictionary<string, ModelRate> Rates { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude-opus-5"] = new(15.00m, 75.00m),
        ["claude-sonnet-5"] = new(3.00m, 15.00m),
        ["claude-haiku-4-5"] = new(1.00m, 5.00m),
    };

    /// <summary>Used when a model id matches nothing in <see cref="Rates"/>.</summary>
    public ModelRate Fallback { get; set; } = new(15.00m, 75.00m);

    /// <summary>Cache reads are billed at a fraction of the input rate.</summary>
    public decimal CacheReadMultiplier { get; set; } = 0.1m;

    /// <summary>Writing to the cache costs a premium over plain input.</summary>
    public decimal CacheWriteMultiplier { get; set; } = 1.25m;

    /// <summary>Longest-prefix match, so dated ids like -20251001 still resolve.</summary>
    public ModelRate RateFor(string model)
    {
        ModelRate? best = null;
        var bestLen = -1;
        foreach (var (key, rate) in Rates)
        {
            if (model.StartsWith(key, StringComparison.OrdinalIgnoreCase) && key.Length > bestLen)
            {
                best = rate;
                bestLen = key.Length;
            }
        }

        return best ?? Fallback;
    }

    /// <summary>Prices one call. Cache tokens are billed separately from plain input.</summary>
    public decimal Cost(string model, long input, long output, long cacheRead, long cacheWrite)
    {
        var r = RateFor(model);
        const decimal million = 1_000_000m;

        return ((input * r.InputPerMTok)
                + (output * r.OutputPerMTok)
                + (cacheRead * r.InputPerMTok * CacheReadMultiplier)
                + (cacheWrite * r.InputPerMTok * CacheWriteMultiplier))
               / million;
    }

    /// <summary>Builds the usage record for a completed call.</summary>
    public AiCallUsage Usage(string model, long input, long output, long cacheRead, long cacheWrite)
        => new(model, input, output, cacheRead, cacheWrite, Cost(model, input, output, cacheRead, cacheWrite));

    /// <summary>USD per million input/output tokens.</summary>
    public record ModelRate(decimal InputPerMTok, decimal OutputPerMTok);
}
