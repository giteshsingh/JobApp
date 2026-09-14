namespace JobApp.Infrastructure;

using JobApp.Core.Abstractions;
using JobApp.Infrastructure.Ai;
using JobApp.Infrastructure.Data;
using JobApp.Infrastructure.Documents;
using JobApp.Infrastructure.Options;
using JobApp.Infrastructure.Services;
using JobApp.Infrastructure.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Wires up every piece of the Infrastructure layer against the host's DI container.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJobAppInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<ClaudeOptions>(config.GetSection(ClaudeOptions.Section));
        services.Configure<ClaudePricing>(config.GetSection(ClaudePricing.Section));
        services.Configure<JobSourceOptions>(config.GetSection(JobSourceOptions.Section));
        services.Configure<IngestionOptions>(config.GetSection(IngestionOptions.Section));

        var dbPath = config.GetConnectionString("JobAppDb") ?? "Data Source=jobapp.db";
        services.AddDbContext<JobAppDbContext>(o => o.UseSqlite(dbPath));

        // Named clients so each source can carry its own headers/timeout later
        // without touching the others.
        services.AddHttpClient(nameof(AdzunaJobSource), c => c.Timeout = TimeSpan.FromSeconds(20));
        services.AddHttpClient(nameof(JSearchJobSource), c => c.Timeout = TimeSpan.FromSeconds(20));
        services.AddHttpClient(nameof(RemotiveJobSource), c => c.Timeout = TimeSpan.FromSeconds(20));
        services.AddHttpClient(nameof(ArbeitnowJobSource), c => c.Timeout = TimeSpan.FromSeconds(20));
        services.AddHttpClient(nameof(JobPageFetcher), c =>
        {
            c.Timeout = TimeSpan.FromSeconds(15);
            // A generic browser UA — bare HttpClient UAs are blocked by many sites outright.
            c.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124 Safari/537.36");
        });

        services.AddScoped<IJobSource, AdzunaJobSource>();
        services.AddScoped<IJobSource, JSearchJobSource>();
        services.AddScoped<IJobSource, RemotiveJobSource>();
        services.AddScoped<IJobSource, ArbeitnowJobSource>();

        services.AddScoped<IJobPageFetcher, JobPageFetcher>();
        services.AddScoped<IJobAnalyst, ClaudeJobAnalyst>();

        services.AddScoped<IResumeImporter, DocxResumeImporter>();
        services.AddScoped<IResumeImporter, PlainTextResumeImporter>();
        services.AddScoped<IResumeRenderer, DocxResumeRenderer>();
        services.AddScoped<IResumeRenderer, PdfResumeRenderer>();

        services.AddScoped<ISpendGuard, SpendGuard>();
        services.AddScoped<SpendGuard>();

        services.AddScoped<IngestionService>();
        services.AddScoped<ScoringService>();
        services.AddScoped<TailoringService>();
        services.AddScoped<ManualImportService>();
        services.AddScoped<ResumeService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<PipelineService>();
        services.AddScoped<Seeder>();

        services.AddHostedService<PollingWorker>();

        return services;
    }
}
