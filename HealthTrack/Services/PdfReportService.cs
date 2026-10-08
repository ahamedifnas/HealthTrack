using System.Text.RegularExpressions;
using HealthTrack.Models.Reports;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace HealthTrack.Services;

public interface IPdfReportService
{
    byte[] Generate(ReportViewModel report);
    string FileName(ReportViewModel report);
}

// Uses installed fonts, never remote resources or user-supplied HTML/URLs.
public sealed class ReportFontResolver(IConfiguration configuration) : IFontResolver
{
    private readonly string directory = configuration["Pdf:FontDirectory"] ?? Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
    public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) => new(bold ? "arialbd.ttf" : "arial.ttf");
    public byte[] GetFont(string faceName) => File.ReadAllBytes(Path.Combine(directory, faceName));
}

public sealed class PdfReportService : IPdfReportService
{
    public string FileName(ReportViewModel report)
    {
        var title = Safe(report.Title);
        var name = report.ClientName is null ? "" : "_" + Safe(report.ClientName);
        return $"HealthTrack_{title}{name}_{report.GeneratedAt:yyyy-MM-dd}.pdf";
    }

    private static string Safe(string text)
    {
        var result = Regex.Replace(text, "[^a-zA-Z0-9_-]+", "_").Trim('_');
        return string.IsNullOrEmpty(result) ? "Report" : result[..Math.Min(result.Length, 70)];
    }

    public byte[] Generate(ReportViewModel report)
    {
        var document = new Document();
        document.Info.Title = report.Title;
        document.Info.Author = "HealthTrack";
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = "Arial";
        normal.Font.Size = 9;
        normal.ParagraphFormat.SpaceAfter = 5;
        var heading = document.Styles[StyleNames.Heading1]!;
        heading.Font.Size = 17;
        heading.Font.Color = Color.FromRgb(22, 91, 73);
        heading.ParagraphFormat.SpaceBefore = 12;
        heading.ParagraphFormat.SpaceAfter = 8;
        var section = document.AddSection();
        var wide = report.Tables.Any(t => t.Columns.Length > 6);
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = wide ? Orientation.Landscape : Orientation.Portrait;
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.8);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.8);
        section.PageSetup.LeftMargin = section.PageSetup.RightMargin = Unit.FromCentimeter(1.6);
        section.Headers.Primary.AddParagraph("HEALTHTRACK  /  REPORTS").Format.Font.Color = Colors.Gray;
        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Font.Size = 8;
        footer.Format.Alignment = ParagraphAlignment.Right;
        footer.AddText("HealthTrack | Page "); footer.AddPageField(); footer.AddText(" of "); footer.AddNumPagesField();
        section.AddParagraph("HealthTrack", StyleNames.Heading1);
        section.AddParagraph(report.Title, StyleNames.Heading1);
        if (report.ClientName is not null) section.AddParagraph("Client / user: " + report.ClientName);
        section.AddParagraph($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC");
        section.AddParagraph(report.Scope);
        if (report.Filter is not null) section.AddParagraph(report.Filter.Description);
        if (report.Metrics.Count != 0)
            AddTable("Summary statistics", ["Metric", "Value"], report.Metrics.Select(m => new[] { m.Label, m.Value }).ToList());
        foreach (var table in report.Tables) AddTable(table.Title, table.Columns, table.Rows);
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var output = new MemoryStream();
        renderer.PdfDocument.Save(output, false);
        return output.ToArray();

        void AddTable(string title, string[] columns, IReadOnlyList<string[]> rows)
        {
            section.AddParagraph(title, StyleNames.Heading1);
            if (rows.Count == 0) { section.AddParagraph("No records match this report."); return; }
            var table = section.AddTable();
            table.Borders.Width = 0.35;
            table.Borders.Color = Color.FromRgb(207, 219, 214);
            table.Format.Font.Size = columns.Length > 6 ? 8 : 9;
            table.Format.SpaceAfter = 2;
            table.TopPadding = table.BottomPadding = 4;
            table.Rows.LeftIndent = 0;
            var width = Unit.FromCentimeter((wide ? 29.7 : 21) - 3.2).Point / columns.Length;
            foreach (var unused in columns) table.AddColumn(Unit.FromPoint(width));
            var header = table.AddRow();
            header.HeadingFormat = true;
            header.Shading.Color = Color.FromRgb(22, 91, 73);
            header.Format.Font.Color = Colors.White;
            header.Format.Font.Bold = true;
            for (var i = 0; i < columns.Length; i++) header.Cells[i].AddParagraph(columns[i]);
            var rowNumber = 0;
            foreach (var values in rows)
            {
                // Bounded continuation rows let even maximum-length notes span pages without clipping.
                var chunks = values.Select(v => Chunk(v ?? "-")).ToArray();
                for (var part = 0; part < chunks.Max(x => x.Length); part++)
                {
                    var row = table.AddRow();
                    if (rowNumber % 2 == 0) row.Shading.Color = Color.FromRgb(242, 247, 244);
                    for (var i = 0; i < columns.Length; i++)
                    {
                        var text = part < chunks[i].Length ? chunks[i][part] : (i == 0 ? "(continued)" : "");
                        // Break long unspaced input, including emails, without discarding content.
                        row.Cells[i].AddParagraph(Regex.Replace(text, @"(\S{10})(?=\S)", "$1\u200B"));
                    }
                }
                rowNumber++;
            }
            section.AddParagraph();
        }
    }

    private static string[] Chunk(string value)
    {
        var text = Regex.Replace(value, @"[\r\n\t]+", " ");
        if (text.Length == 0) return [""];
        return Enumerable.Range(0, (text.Length + 179) / 180).Select(i => text.Substring(i * 180, Math.Min(180, text.Length - i * 180))).ToArray();
    }
}
