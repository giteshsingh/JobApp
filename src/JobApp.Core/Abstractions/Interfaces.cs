namespace JobApp.Core.Abstractions;

using JobApp.Core.Contracts;
using JobApp.Core.Domain;

/// <summary>A job board or feed the ingestion worker can poll.</summary>
public interface IJobSource
{
    JobSourceKind Kind { get; }

    string DisplayName { get; }

    /// <summary>False when required API keys are missing, so the worker can skip it cleanly.</summary>
    bool IsConfigured { get; }

    /// <summary>Fetch postings matching the profile. Implementations must not throw on HTTP failure.</summary>
    Task<IReadOnlyList<Job>> FetchAsync(SearchProfile profile, CancellationToken ct);
}

/// <summary>Claude-backed analysis. Implementations record their own usage.</summary>
public interface IJobAnalyst
{
    /// <summary>Cheap model: classify work model and score fit.</summary>
    Task<AiResponse<TriageResult>> TriageAsync(Job job, SearchProfile profile, string resumeText, CancellationToken ct);

    /// <summary>Expensive model: deeper fit read for a job you actually opened.</summary>
    Task<AiResponse<TriageResult>> DeepFitAsync(Job job, SearchProfile profile, string resumeText, CancellationToken ct);

    /// <summary>Expensive model: rewrite the resume against one JD.</summary>
    Task<AiResponse<TailoringResult>> TailorResumeAsync(Job job, ResumeDoc resume, bool includeCoverLetter, CancellationToken ct);

    /// <summary>Pull structured fields out of a pasted JD or scraped page.</summary>
    Task<AiResponse<ParsedJobPosting>> ParseJobPostingAsync(string rawText, string? sourceUrl, CancellationToken ct);
}

/// <summary>Reads a .docx/.pdf/.txt resume into structured sections.</summary>
public interface IResumeImporter
{
    bool CanImport(string fileName);

    Task<ResumeDoc> ImportAsync(Stream content, string fileName, CancellationToken ct);
}

/// <summary>Writes a resume back out as a downloadable file.</summary>
public interface IResumeRenderer
{
    /// <summary>"docx" or "pdf".</summary>
    string Format { get; }

    string ContentType { get; }

    byte[] Render(ResumeDoc resume);
}

/// <summary>Fetches the HTML of a pasted job URL.</summary>
public interface IJobPageFetcher
{
    Task<string?> FetchReadableTextAsync(string url, CancellationToken ct);
}

/// <summary>Guards the optional monthly Claude spend ceiling.</summary>
public interface ISpendGuard
{
    Task<bool> CanSpendAsync(CancellationToken ct);

    Task RecordAsync(AiPurpose purpose, AiCallUsage usage, int? jobId, CancellationToken ct);
}
