using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ITClubOrg.Models;

namespace ITClubOrg.Services;

public static class ReportWordExporter
{
    public static byte[] Export(string title, ReportTable report)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            var body = new Body();
            mainPart.Document = new Document(body);
            body.Append(CreateParagraph(title, true));
            body.Append(CreateParagraph($"Generated {DateTime.Now:MMMM d, yyyy h:mm tt}", false));

            var table = new Table(
                new TableProperties(
                    new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 6, Color = "DDE2EE" },
                        new BottomBorder { Val = BorderValues.Single, Size = 6, Color = "DDE2EE" },
                        new LeftBorder { Val = BorderValues.Single, Size = 6, Color = "DDE2EE" },
                        new RightBorder { Val = BorderValues.Single, Size = 6, Color = "DDE2EE" },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "DDE2EE" },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "DDE2EE" })));
            table.Append(CreateRow(report.Headers, header: true));
            foreach (var row in report.Rows)
            {
                table.Append(CreateRow(row, header: false));
            }

            body.Append(table);
            body.Append(new SectionProperties());
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static Paragraph CreateParagraph(string text, bool title)
    {
        var properties = new RunProperties(new FontSize { Val = title ? "32" : "20" });
        if (title)
        {
            properties.Append(new Bold(), new Color { Val = "000080" });
        }

        return new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { After = title ? "100" : "260" }),
            new Run(properties, new Text(text)));
    }

    private static TableRow CreateRow(IReadOnlyList<string> values, bool header)
    {
        var row = new TableRow();
        foreach (var value in values)
        {
            var runProperties = new RunProperties(new FontSize { Val = "18" });
            var cellProperties = new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Auto });
            if (header)
            {
                runProperties.Append(new Bold(), new Color { Val = "FFFFFF" });
                cellProperties.Append(new Shading { Fill = "000080", Val = ShadingPatternValues.Clear });
            }

            var paragraph = new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines { Before = "60", After = "60" }),
                new Run(runProperties, new Text(value) { Space = SpaceProcessingModeValues.Preserve }));
            row.Append(new TableCell(cellProperties, paragraph));
        }

        return row;
    }
}
