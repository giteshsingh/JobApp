namespace JobApp.Infrastructure.Documents;

using JobApp.Core.Abstractions;
using JobApp.Core.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

/// <summary>
/// Print-ready PDF of the same content the .docx renderer emits. Kept text-based
/// (no images, no columns) so PDF text extraction still works for ATS uploads.
/// </summary>
public class PdfResumeRenderer : IResumeRenderer
{
    public string Format => "pdf";

    public string ContentType => "application/pdf";

    public byte[] Render(ResumeDoc resume)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily(Fonts.Calibri).FontColor(Colors.Black));

                page.Header().PaddingBottom(6).Column(col =>
                {
                    col.Item().Text(resume.Name).FontSize(17).SemiBold();
                });

                page.Content().Column(col =>
                {
                    col.Spacing(8);

                    foreach (var section in resume.Sections.OrderBy(s => s.Order))
                    {
                        if (section.Bullets.Count == 0 && string.IsNullOrWhiteSpace(section.Subject))
                        {
                            continue;
                        }

                        col.Item().Column(sectionCol =>
                        {
                            sectionCol.Spacing(3);

                            sectionCol.Item()
                                .BorderBottom(0.75f)
                                .BorderColor(Colors.Grey.Medium)
                                .PaddingBottom(2)
                                .Text(section.Heading.ToUpperInvariant())
                                .FontSize(11).SemiBold().LetterSpacing(0.04f);

                            if (!string.IsNullOrWhiteSpace(section.Subject))
                            {
                                var subject = string.IsNullOrWhiteSpace(section.DateRange)
                                    ? section.Subject!
                                    : $"{section.Subject}  —  {section.DateRange}";
                                sectionCol.Item().Text(subject).SemiBold();
                            }
                            else if (!string.IsNullOrWhiteSpace(section.DateRange))
                            {
                                sectionCol.Item().Text(section.DateRange!).Italic().FontColor(Colors.Grey.Darken2);
                            }

                            var asProse = section.Kind is ResumeSectionKind.Summary or ResumeSectionKind.Skills;

                            foreach (var bullet in section.Bullets)
                            {
                                if (asProse)
                                {
                                    sectionCol.Item().Text(bullet).LineHeight(1.25f);
                                }
                                else
                                {
                                    // Hanging indent so wrapped lines align under the text.
                                    sectionCol.Item().Row(row =>
                                    {
                                        row.ConstantItem(12).Text("•");
                                        row.RelativeItem().Text(bullet).LineHeight(1.25f);
                                    });
                                }
                            }
                        });
                    }
                });

                page.Footer().AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Medium));
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }
}
