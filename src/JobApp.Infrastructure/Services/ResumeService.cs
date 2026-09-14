namespace JobApp.Infrastructure.Services;

using JobApp.Core.Abstractions;
using JobApp.Core.Domain;
using JobApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>Import, list and promote resume documents.</summary>
public class ResumeService(
    JobAppDbContext db,
    IEnumerable<IResumeImporter> importers,
    ILogger<ResumeService> log)
{
    /// <summary>Imports an uploaded file. The first resume you add becomes the master.</summary>
    public async Task<ResumeDoc> ImportAsync(Stream content, string fileName, bool isCoverLetter, CancellationToken ct)
    {
        var importer = importers.FirstOrDefault(i => i.CanImport(fileName))
                       ?? throw new NotSupportedException(
                           $"Cannot read '{Path.GetExtension(fileName)}'. Supported: .docx, .txt, .md");

        var resume = await importer.ImportAsync(content, fileName, ct);
        resume.IsCoverLetter = isCoverLetter;

        if (string.IsNullOrWhiteSpace(resume.PlainText))
        {
            throw new InvalidOperationException("No text could be read from that file.");
        }

        var hasMaster = await db.Resumes.AnyAsync(r => r.IsMaster && !r.IsCoverLetter, ct);
        resume.IsMaster = !isCoverLetter && !hasMaster;

        db.Resumes.Add(resume);
        await db.SaveChangesAsync(ct);

        log.LogInformation(
            "Imported resume '{Name}': {Sections} sections, {Skills} skills, master={Master}",
            resume.Name, resume.Sections.Count, resume.ExtractedSkills.Count, resume.IsMaster);

        return resume;
    }

    /// <summary>Imports from a path on this machine — used by the first-run seeder.</summary>
    public async Task<ResumeDoc?> ImportFromPathAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            log.LogWarning("Resume not found at {Path}", path);
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await ImportAsync(stream, Path.GetFileName(path), isCoverLetter: false, ct);
    }

    /// <summary>Makes one resume the baseline for scoring and tailoring.</summary>
    public async Task<ResumeDoc> SetMasterAsync(int id, CancellationToken ct)
    {
        var resume = await db.Resumes.FirstOrDefaultAsync(r => r.Id == id, ct)
                     ?? throw new KeyNotFoundException($"Resume {id} not found.");

        if (resume.IsCoverLetter)
        {
            throw new InvalidOperationException("A cover letter cannot be the master resume.");
        }

        await db.Resumes
            .Where(r => r.IsMaster && r.Id != id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsMaster, false), ct);

        resume.IsMaster = true;
        resume.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return resume;
    }

    /// <summary>Deletes a resume, refusing to leave you with no master.</summary>
    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var resume = await db.Resumes.FirstOrDefaultAsync(r => r.Id == id, ct)
                     ?? throw new KeyNotFoundException($"Resume {id} not found.");

        if (resume.IsMaster)
        {
            var replacement = await db.Resumes
                .Where(r => r.Id != id && !r.IsCoverLetter)
                .OrderByDescending(r => r.UpdatedAt)
                .FirstOrDefaultAsync(ct);

            if (replacement is null)
            {
                throw new InvalidOperationException(
                    "That's your only resume — upload a replacement before deleting it.");
            }

            replacement.IsMaster = true;
        }

        db.Resumes.Remove(resume);
        await db.SaveChangesAsync(ct);
    }
}
