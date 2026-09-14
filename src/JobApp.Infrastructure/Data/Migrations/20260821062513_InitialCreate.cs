using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApp.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiUsage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Purpose = table.Column<int>(type: "INTEGER", nullable: false),
                    Model = table.Column<string>(type: "TEXT", nullable: false),
                    InputTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    OutputTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    CacheReadTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    CacheWriteTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    CostUsd = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    JobId = table.Column<int>(type: "INTEGER", nullable: true),
                    Succeeded = table.Column<bool>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", nullable: true),
                    At = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", nullable: true),
                    Url = table.Column<string>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    Company = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    LocationRaw = table.Column<string>(type: "TEXT", nullable: true),
                    City = table.Column<string>(type: "TEXT", nullable: true),
                    CountryCode = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    PostedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    DiscoveredAt = table.Column<long>(type: "INTEGER", nullable: false),
                    SalaryMin = table.Column<decimal>(type: "TEXT", nullable: true),
                    SalaryMax = table.Column<decimal>(type: "TEXT", nullable: true),
                    SalaryCurrency = table.Column<string>(type: "TEXT", nullable: true),
                    SalaryIsEstimate = table.Column<bool>(type: "INTEGER", nullable: false),
                    WorkModel = table.Column<int>(type: "INTEGER", nullable: false),
                    OfficeDaysPerWeek = table.Column<int>(type: "INTEGER", nullable: true),
                    WorkModelConfidence = table.Column<double>(type: "REAL", nullable: false),
                    WorkModelOrigin = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkModelEvidence = table.Column<string>(type: "TEXT", nullable: true),
                    MentionsVisaSponsorship = table.Column<bool>(type: "INTEGER", nullable: true),
                    RequiredYearsExperience = table.Column<int>(type: "INTEGER", nullable: true),
                    DedupeKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Resumes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsMaster = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCoverLetter = table.Column<bool>(type: "INTEGER", nullable: false),
                    SourceFilePath = table.Column<string>(type: "TEXT", nullable: true),
                    PlainText = table.Column<string>(type: "TEXT", nullable: false),
                    Sections = table.Column<string>(type: "TEXT", nullable: false),
                    ExtractedSkills = table.Column<string>(type: "TEXT", nullable: false),
                    YearsExperience = table.Column<int>(type: "INTEGER", nullable: true),
                    DerivedFromTailoringId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resumes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SearchProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    TargetTitles = table.Column<string>(type: "TEXT", nullable: false),
                    ExcludeKeywords = table.Column<string>(type: "TEXT", nullable: false),
                    MustHaveSkills = table.Column<string>(type: "TEXT", nullable: false),
                    NiceToHaveSkills = table.Column<string>(type: "TEXT", nullable: false),
                    MinYearsExperience = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxYearsExperience = table.Column<int>(type: "INTEGER", nullable: true),
                    CountryCodes = table.Column<string>(type: "TEXT", nullable: false),
                    HomeCity = table.Column<string>(type: "TEXT", nullable: true),
                    AcceptableCities = table.Column<string>(type: "TEXT", nullable: false),
                    AllowGlobalRemote = table.Column<bool>(type: "INTEGER", nullable: false),
                    OpenToRelocation = table.Column<bool>(type: "INTEGER", nullable: false),
                    TrackVisaSponsorship = table.Column<bool>(type: "INTEGER", nullable: false),
                    RemoteWeight = table.Column<int>(type: "INTEGER", nullable: false),
                    HybridWeight = table.Column<int>(type: "INTEGER", nullable: false),
                    HybridOverLimitWeight = table.Column<int>(type: "INTEGER", nullable: false),
                    OnsiteWeight = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxOfficeDaysPerWeek = table.Column<int>(type: "INTEGER", nullable: false),
                    ExcludeOnsite = table.Column<bool>(type: "INTEGER", nullable: false),
                    MinSalary = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    SalaryCurrency = table.Column<string>(type: "TEXT", nullable: false),
                    IncludeUndisclosedSalary = table.Column<bool>(type: "INTEGER", nullable: false),
                    PostedWithinDays = table.Column<int>(type: "INTEGER", nullable: false),
                    MinOverallScore = table.Column<int>(type: "INTEGER", nullable: false),
                    SkillFitWeight = table.Column<int>(type: "INTEGER", nullable: false),
                    LocationFitWeight = table.Column<int>(type: "INTEGER", nullable: false),
                    FreshnessWeight = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Applications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobId = table.Column<int>(type: "INTEGER", nullable: false),
                    Stage = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    AppliedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ResumeDocId = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    NextAction = table.Column<string>(type: "TEXT", nullable: true),
                    NextActionAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Applications_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Applications_Resumes_ResumeDocId",
                        column: x => x.ResumeDocId,
                        principalTable: "Resumes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Tailorings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobId = table.Column<int>(type: "INTEGER", nullable: false),
                    ResumeDocId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", nullable: true),
                    TailoredSummary = table.Column<string>(type: "TEXT", nullable: true),
                    TailoredSkillsLine = table.Column<string>(type: "TEXT", nullable: true),
                    KeywordsToAdd = table.Column<string>(type: "TEXT", nullable: false),
                    HonestyNotes = table.Column<string>(type: "TEXT", nullable: true),
                    CoverLetterDraft = table.Column<string>(type: "TEXT", nullable: true),
                    ModelUsed = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tailorings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tailorings_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tailorings_Resumes_ResumeDocId",
                        column: x => x.ResumeDocId,
                        principalTable: "Resumes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobId = table.Column<int>(type: "INTEGER", nullable: false),
                    SearchProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    OverallScore = table.Column<int>(type: "INTEGER", nullable: false),
                    SkillScore = table.Column<int>(type: "INTEGER", nullable: false),
                    SeniorityScore = table.Column<int>(type: "INTEGER", nullable: false),
                    LocationScore = table.Column<int>(type: "INTEGER", nullable: false),
                    FreshnessScore = table.Column<int>(type: "INTEGER", nullable: false),
                    SalaryScore = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchedSkills = table.Column<string>(type: "TEXT", nullable: false),
                    MissingMustHaves = table.Column<string>(type: "TEXT", nullable: false),
                    MissingResumeKeywords = table.Column<string>(type: "TEXT", nullable: false),
                    RedFlags = table.Column<string>(type: "TEXT", nullable: false),
                    Rationale = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeepAnalysis = table.Column<bool>(type: "INTEGER", nullable: false),
                    ModelUsed = table.Column<string>(type: "TEXT", nullable: true),
                    ScoredAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobScores_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobScores_SearchProfiles_SearchProfileId",
                        column: x => x.SearchProfileId,
                        principalTable: "SearchProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StageChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    From = table.Column<int>(type: "INTEGER", nullable: false),
                    To = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    At = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StageChanges_Applications_JobApplicationId",
                        column: x => x.JobApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Suggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TailoringSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Section = table.Column<int>(type: "INTEGER", nullable: false),
                    SectionHeading = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalText = table.Column<string>(type: "TEXT", nullable: false),
                    SuggestedText = table.Column<string>(type: "TEXT", nullable: false),
                    Rationale = table.Column<string>(type: "TEXT", nullable: true),
                    KeywordsAdded = table.Column<string>(type: "TEXT", nullable: false),
                    Accepted = table.Column<bool>(type: "INTEGER", nullable: true),
                    UserEditedText = table.Column<string>(type: "TEXT", nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Suggestions_Tailorings_TailoringSessionId",
                        column: x => x.TailoringSessionId,
                        principalTable: "Tailorings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsage_At",
                table: "AiUsage",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_JobId",
                table: "Applications",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_ResumeDocId",
                table: "Applications",
                column: "ResumeDocId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Stage",
                table: "Applications",
                column: "Stage");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_DedupeKey",
                table: "Jobs",
                column: "DedupeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_PostedAt",
                table: "Jobs",
                column: "PostedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Source_ExternalId",
                table: "Jobs",
                columns: new[] { "Source", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_JobScores_JobId_SearchProfileId",
                table: "JobScores",
                columns: new[] { "JobId", "SearchProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobScores_OverallScore",
                table: "JobScores",
                column: "OverallScore");

            migrationBuilder.CreateIndex(
                name: "IX_JobScores_SearchProfileId",
                table: "JobScores",
                column: "SearchProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_StageChanges_JobApplicationId",
                table: "StageChanges",
                column: "JobApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_Suggestions_TailoringSessionId",
                table: "Suggestions",
                column: "TailoringSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Tailorings_JobId",
                table: "Tailorings",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_Tailorings_ResumeDocId",
                table: "Tailorings",
                column: "ResumeDocId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUsage");

            migrationBuilder.DropTable(
                name: "JobScores");

            migrationBuilder.DropTable(
                name: "StageChanges");

            migrationBuilder.DropTable(
                name: "Suggestions");

            migrationBuilder.DropTable(
                name: "SearchProfiles");

            migrationBuilder.DropTable(
                name: "Applications");

            migrationBuilder.DropTable(
                name: "Tailorings");

            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropTable(
                name: "Resumes");
        }
    }
}
