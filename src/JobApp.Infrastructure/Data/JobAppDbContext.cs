namespace JobApp.Infrastructure.Data;

using System.Text.Json;
using JobApp.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>SQLite store for the whole dashboard.</summary>
public class JobAppDbContext(DbContextOptions<JobAppDbContext> options) : DbContext(options)
{
    public DbSet<Job> Jobs => Set<Job>();

    public DbSet<JobScore> JobScores => Set<JobScore>();

    public DbSet<SearchProfile> SearchProfiles => Set<SearchProfile>();

    public DbSet<ResumeDoc> Resumes => Set<ResumeDoc>();

    public DbSet<TailoringSession> Tailorings => Set<TailoringSession>();

    public DbSet<ResumeSuggestion> Suggestions => Set<ResumeSuggestion>();

    public DbSet<JobApplication> Applications => Set<JobApplication>();

    public DbSet<StageChange> StageChanges => Set<StageChange>();

    public DbSet<AiUsageRecord> AiUsage => Set<AiUsageRecord>();

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <summary>List&lt;string&gt; stored as a JSON text column — plenty for a single-user tool.</summary>
    private static readonly ValueConverter<List<string>, string> StringListConverter = new(
        v => JsonSerializer.Serialize(v, JsonOpts),
        v => string.IsNullOrWhiteSpace(v) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(v, JsonOpts) ?? new List<string>());

    private static readonly ValueComparer<List<string>> StringListComparer = new(
        (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
        v => v == null ? 0 : v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
        v => v == null ? new List<string>() : new List<string>(v));

    /// <summary>
    /// SQLite has no native DateTimeOffset type and EF's default TEXT mapping
    /// cannot be used in ORDER BY / ExecuteUpdate — "SQLite does not support
    /// expressions of type 'DateTimeOffset' in ORDER BY clauses". Storing as
    /// UTC ticks makes every date column a plain sortable integer instead.
    /// </summary>
    private static readonly ValueConverter<DateTimeOffset, long> DateTimeOffsetConverter = new(
        v => v.UtcTicks,
        v => new DateTimeOffset(v, TimeSpan.Zero));

    private static readonly ValueConverter<DateTimeOffset?, long?> NullableDateTimeOffsetConverter = new(
        v => v.HasValue ? v.Value.UtcTicks : null,
        v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : null);

    private static readonly ValueConverter<List<ResumeSection>, string> SectionsConverter = new(
        v => JsonSerializer.Serialize(v, JsonOpts),
        v => string.IsNullOrWhiteSpace(v) ? new List<ResumeSection>() : JsonSerializer.Deserialize<List<ResumeSection>>(v, JsonOpts) ?? new List<ResumeSection>());

    private static readonly ValueComparer<List<ResumeSection>> SectionsComparer = new(
        (a, b) => JsonSerializer.Serialize(a, JsonOpts) == JsonSerializer.Serialize(b, JsonOpts),
        v => JsonSerializer.Serialize(v, JsonOpts).GetHashCode(),
        v => JsonSerializer.Deserialize<List<ResumeSection>>(JsonSerializer.Serialize(v, JsonOpts), JsonOpts) ?? new List<ResumeSection>());

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ---- Job ----------------------------------------------------------
        b.Entity<Job>(e =>
        {
            e.Property(x => x.Title).IsRequired().HasMaxLength(400);
            e.Property(x => x.Company).IsRequired().HasMaxLength(300);
            e.Property(x => x.DedupeKey).IsRequired().HasMaxLength(64);

            // One card per real-world role, however many boards list it.
            e.HasIndex(x => x.DedupeKey).IsUnique();
            e.HasIndex(x => x.PostedAt);
            e.HasIndex(x => new { x.Source, x.ExternalId });

            e.HasMany(x => x.Scores)
                .WithOne(x => x.Job)
                .HasForeignKey(x => x.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Application)
                .WithOne(x => x.Job)
                .HasForeignKey<JobApplication>(x => x.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Tailorings)
                .WithOne(x => x.Job)
                .HasForeignKey(x => x.JobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- JobScore -----------------------------------------------------
        b.Entity<JobScore>(e =>
        {
            // Re-scoring a job for a profile replaces the row rather than piling up.
            e.HasIndex(x => new { x.JobId, x.SearchProfileId }).IsUnique();
            e.HasIndex(x => x.OverallScore);

            JsonList(e.Property(x => x.MatchedSkills));
            JsonList(e.Property(x => x.MissingMustHaves));
            JsonList(e.Property(x => x.MissingResumeKeywords));
            JsonList(e.Property(x => x.RedFlags));
        });

        // ---- SearchProfile ------------------------------------------------
        b.Entity<SearchProfile>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(120);

            JsonList(e.Property(x => x.TargetTitles));
            JsonList(e.Property(x => x.ExcludeKeywords));
            JsonList(e.Property(x => x.MustHaveSkills));
            JsonList(e.Property(x => x.NiceToHaveSkills));
            JsonList(e.Property(x => x.CountryCodes));
            JsonList(e.Property(x => x.AcceptableCities));

            e.Property(x => x.MinSalary).HasPrecision(18, 2);
        });

        // ---- ResumeDoc ----------------------------------------------------
        b.Entity<ResumeDoc>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);

            e.Property(x => x.Sections)
                .HasConversion(SectionsConverter)
                .Metadata.SetValueComparer(SectionsComparer);

            JsonList(e.Property(x => x.ExtractedSkills));
        });

        // ---- Tailoring ----------------------------------------------------
        b.Entity<TailoringSession>(e =>
        {
            JsonList(e.Property(x => x.KeywordsToAdd));

            e.HasMany(x => x.Suggestions)
                .WithOne(x => x.TailoringSession)
                .HasForeignKey(x => x.TailoringSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.ResumeDoc)
                .WithMany()
                .HasForeignKey(x => x.ResumeDocId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ResumeSuggestion>(e => JsonList(e.Property(x => x.KeywordsAdded)));

        // ---- Applications -------------------------------------------------
        b.Entity<JobApplication>(e =>
        {
            e.HasIndex(x => x.Stage);

            e.HasMany(x => x.History)
                .WithOne(x => x.JobApplication)
                .HasForeignKey(x => x.JobApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Keep the card if the resume version it references is deleted.
            e.HasOne(x => x.ResumeDoc)
                .WithMany()
                .HasForeignKey(x => x.ResumeDocId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ---- AI usage -----------------------------------------------------
        b.Entity<AiUsageRecord>(e =>
        {
            e.HasIndex(x => x.At);
            e.Property(x => x.CostUsd).HasPrecision(18, 6);
        });

        // Apply the DateTimeOffset -> ticks conversion to every such property on
        // every entity, so no new date column can slip through unconverted.
        foreach (var entityType in b.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(DateTimeOffsetConverter);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(NullableDateTimeOffsetConverter);
                }
            }
        }

        base.OnModelCreating(b);
    }

    /// <summary>Applies the JSON converter and comparer to a string-list property.</summary>
    private static void JsonList(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<List<string>> prop)
    {
        prop.HasConversion(StringListConverter).Metadata.SetValueComparer(StringListComparer);
    }
}
