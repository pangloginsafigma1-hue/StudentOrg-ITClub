using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ITClubOrg.Models;

namespace ITClubOrg.Services;

public static class MemberWordExporter
{
    private static readonly string[] ColumnHeaders = ["ID", "Student Name", "Course", "Year Level", "Position", "Status"];

    public static byte[] Export(IEnumerable<Member> members)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            var body = new Body();
            mainPart.Document = new Document(body);

            body.Append(CreateTitle());
            body.Append(CreateSubtitle());
            body.Append(CreateTable(members));
            body.Append(new SectionProperties());

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static Paragraph CreateTitle() =>
        new(
            new ParagraphProperties(new SpacingBetweenLines { After = "100" }),
            new Run(
                new RunProperties(
                    new Bold(),
                    new FontSize { Val = "32" },
                    new Color { Val = "000080" }),
                new Text("List of Organization Members")));

    private static Paragraph CreateSubtitle() =>
        new(
            new ParagraphProperties(new SpacingBetweenLines { After = "260" }),
            new Run(
                new RunProperties(new FontSize { Val = "20" }),
                new Text($"Exported {DateTime.Now:MMMM d, yyyy}")));

    private static Table CreateTable(IEnumerable<Member> members)
    {
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

        table.Append(CreateRow(ColumnHeaders, header: true, shaded: false));

        var index = 0;
        foreach (var member in members)
        {
            table.Append(CreateRow(
                [member.MemberId, member.StudentName, member.Course, member.YearLevel, member.Position, member.Status],
                header: false,
                shaded: index++ % 2 == 1));
        }

        return table;
    }

    private static TableRow CreateRow(IReadOnlyList<string> values, bool header, bool shaded)
    {
        var row = new TableRow();
        foreach (var value in values)
        {
            var runProperties = new RunProperties(new FontSize { Val = "20" });
            if (header)
            {
                runProperties.Append(new Bold(), new Color { Val = "FFFFFF" });
            }

            var paragraph = new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines { Before = "60", After = "60" }),
                new Run(runProperties, new Text(value) { Space = SpaceProcessingModeValues.Preserve }));
            var cellProperties = new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Auto });
            if (header)
            {
                cellProperties.Append(new Shading { Fill = "000080", Val = ShadingPatternValues.Clear });
            }
            else if (shaded)
            {
                cellProperties.Append(new Shading { Fill = "F0F4FF", Val = ShadingPatternValues.Clear });
            }

            row.Append(new TableCell(cellProperties, paragraph));
        }

        return row;
    }
}
