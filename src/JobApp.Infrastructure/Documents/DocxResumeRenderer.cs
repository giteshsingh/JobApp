namespace JobApp.Infrastructure.Documents;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using JobApp.Core.Abstractions;
using JobApp.Core.Domain;

/// <summary>
/// Writes a resume out as a clean, ATS-friendly .docx: single column, no text
/// boxes, no tables, real heading styles. Fancy layouts are what break ATS
/// parsers, so this deliberately stays plain.
/// </summary>
public class DocxResumeRenderer : IResumeRenderer
{
    public string Format => "docx";

    public string ContentType => "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public byte[] Render(ResumeDoc resume)
    {
        using var stream = new MemoryStream();

        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document();
            var body = main.Document.AppendChild(new Body());

            body.AppendChild(Heading(resume.Name, size: 32, spaceAfter: 60));

            foreach (var section in resume.Sections.OrderBy(s => s.Order))
            {
                if (section.Bullets.Count == 0 && string.IsNullOrWhiteSpace(section.Subject))
                {
                    continue;
                }

                body.AppendChild(Heading(section.Heading.ToUpperInvariant(), size: 24, spaceBefore: 220, spaceAfter: 60));

                if (!string.IsNullOrWhiteSpace(section.Subject))
                {
                    var subject = string.IsNullOrWhiteSpace(section.DateRange)
                        ? section.Subject
                        : $"{section.Subject}  —  {section.DateRange}";
                    body.AppendChild(Paragraph(subject!, bold: true, spaceAfter: 40));
                }
                else if (!string.IsNullOrWhiteSpace(section.DateRange))
                {
                    body.AppendChild(Paragraph(section.DateRange!, italic: true, spaceAfter: 40));
                }

                // The summary reads as prose; everything else as bullets.
                var asProse = section.Kind is ResumeSectionKind.Summary or ResumeSectionKind.Skills;

                foreach (var bullet in section.Bullets)
                {
                    body.AppendChild(asProse
                        ? Paragraph(bullet, spaceAfter: 60)
                        : Paragraph($"•  {bullet}", indentTwips: 260, spaceAfter: 40));
                }
            }

            // A4 with sane margins; ATS tools cope badly with unusual page setups.
            body.AppendChild(new SectionProperties(
                new PageSize { Width = 11906U, Height = 16838U },
                new PageMargin { Top = 720, Bottom = 720, Left = 900, Right = 900, Header = 0U, Footer = 0U, Gutter = 0U }));

            main.Document.Save();
        }

        return stream.ToArray();
    }

    /// <summary>Bold run at a given half-point size, with a bottom rule.</summary>
    private static Paragraph Heading(string text, int size, int spaceBefore = 0, int spaceAfter = 0)
    {
        var props = new ParagraphProperties(
            new SpacingBetweenLines { Before = spaceBefore.ToString(), After = spaceAfter.ToString() },
            new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 4U, Space = 1U }));

        var run = new Run(
            new RunProperties(
                new Bold(),
                new FontSize { Val = size.ToString() },
                new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" }),
            new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        return new Paragraph(props, run);
    }

    private static Paragraph Paragraph(
        string text, bool bold = false, bool italic = false, int indentTwips = 0, int spaceAfter = 0)
    {
        var props = new ParagraphProperties(
            new SpacingBetweenLines { After = spaceAfter.ToString(), Line = "240", LineRule = LineSpacingRuleValues.Auto });

        if (indentTwips > 0)
        {
            props.AppendChild(new Indentation { Left = indentTwips.ToString(), Hanging = "180" });
        }

        var runProps = new RunProperties(
            new FontSize { Val = "21" },
            new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" });

        if (bold)
        {
            runProps.AppendChild(new Bold());
        }

        if (italic)
        {
            runProps.AppendChild(new Italic());
        }

        return new Paragraph(props, new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }
}
