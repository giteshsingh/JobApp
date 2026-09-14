namespace JobApp.Infrastructure.Documents;

using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using JobApp.Core.Abstractions;
using JobApp.Core.Domain;

/// <summary>Reads a .docx resume into structured sections.</summary>
public class DocxResumeImporter : IResumeImporter
{
    public bool CanImport(string fileName)
        => fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase);

    public Task<ResumeDoc> ImportAsync(Stream content, string fileName, CancellationToken ct)
    {
        // OpenXml needs a seekable, writable-position stream.
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        buffer.Position = 0;

        var lines = ReadLines(buffer);
        return Task.FromResult(BuildResume(lines, fileName));
    }

    /// <summary>One string per paragraph, tables flattened row by row.</summary>
    private static List<string> ReadLines(Stream stream)
    {
        var lines = new List<string>();

        using var doc = WordprocessingDocument.Open(stream, isEditable: false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return lines;
        }

        foreach (var element in body.ChildElements)
        {
            switch (element)
            {
                case Paragraph p:
                    lines.Add(ParagraphText(p));
                    break;

                case Table table:
                    // Resumes often lay out dates in a two-column table; keep the
                    // cells on one line so the date stays attached to its entry.
                    foreach (var row in table.Elements<TableRow>())
                    {
                        var cells = row.Elements<TableCell>()
                            .SelectMany(c => c.Elements<Paragraph>().Select(ParagraphText))
                            .Where(s => !string.IsNullOrWhiteSpace(s));

                        var joined = string.Join(" | ", cells);
                        if (!string.IsNullOrWhiteSpace(joined))
                        {
                            lines.Add(joined);
                        }
                    }

                    break;
            }
        }

        return lines;
    }

    /// <summary>Concatenates runs, honouring explicit line and tab breaks.</summary>
    private static string ParagraphText(Paragraph p)
    {
        var sb = new StringBuilder();

        foreach (var run in p.Descendants<Run>())
        {
            foreach (var child in run.ChildElements)
            {
                switch (child)
                {
                    case Text t:
                        sb.Append(t.Text);
                        break;
                    case Break:
                        sb.Append('\n');
                        break;
                    case TabChar:
                        sb.Append(' ');
                        break;
                }
            }
        }

        return sb.ToString().Trim();
    }

    /// <summary>Shared by every importer: structure, skills, years, plain text.</summary>
    internal static ResumeDoc BuildResume(List<string> lines, string fileName)
    {
        // A run of runs can contain embedded newlines; flatten before parsing.
        var flat = lines
            .SelectMany(l => l.Split('\n'))
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        var sections = ResumeStructureParser.Parse(flat);
        var plainText = string.Join('\n', flat);

        return new ResumeDoc
        {
            Name = Path.GetFileNameWithoutExtension(fileName),
            SourceFilePath = fileName,
            PlainText = plainText,
            Sections = sections,
            ExtractedSkills = ResumeStructureParser.ExtractSkills(sections),
            YearsExperience = ResumeStructureParser.ExtractYearsExperience(plainText),
        };
    }
}

/// <summary>Reads a .txt or .md resume. Same structure pass, no unpacking.</summary>
public class PlainTextResumeImporter : IResumeImporter
{
    public bool CanImport(string fileName)
        => fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
           || fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    public async Task<ResumeDoc> ImportAsync(Stream content, string fileName, CancellationToken ct)
    {
        using var reader = new StreamReader(content);
        var text = await reader.ReadToEndAsync(ct);

        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        return DocxResumeImporter.BuildResume(lines, fileName);
    }
}
