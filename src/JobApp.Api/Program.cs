using System.Text.Json.Serialization;
using JobApp.Api;
using JobApp.Infrastructure;
using JobApp.Infrastructure.Data;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// QuestPDF needs an explicit license type at startup — Community is free for
// this kind of single-user tool. See https://www.questpdf.com/license/.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

builder.Services.AddControllers()
    // Enums (WorkModel, ApplicationStage, ...) as "Remote"/"Applied" over the
    // wire — plain integers would be unreadable in requests from the UI.
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddJobAppInfrastructure(builder.Configuration);

// The Vue dev server runs on a different port; only needed outside Production.
const string DevCorsPolicy = "DevCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy => policy
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 10_000_000);

var app = builder.Build();

app.UseJobAppExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(DevCorsPolicy);
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

// Serve the built Vue app for any route Swagger/API didn't claim, so client-side
// routing (e.g. /pipeline, /settings) survives a hard refresh.
app.MapFallbackToFile("index.html");

// Creates the SQLite file, applies migrations, and imports your resume on first run.
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<Seeder>();
    var startupResumePath = builder.Configuration["Seed:ResumePath"];
    await seeder.InitialiseAsync(startupResumePath, CancellationToken.None);
}

app.Run();
