using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using MYG.Electrical.Core.Reports;

namespace MYG.Electrical.Infrastructure.Reports;

/// <summary>
/// Raporları Excel çalışma kitabı olarak yazar (her rapor ayrı sayfa). Bütün hücreler metindir: "-K1" gibi
/// etiketler formül, "1.5" gibi değerler tarih/sayı olarak yorumlanmaz. Başlık satırı kalın ve dondurulmuş,
/// otomatik süzgeç açık.
/// </summary>
public static class XlsxReportWriter
{
    public static void Write(IReadOnlyList<ReportTable> tables, Stream output)
    {
        using var doc = SpreadsheetDocument.Create(output, SpreadsheetDocumentType.Workbook);
        var wbPart = doc.AddWorkbookPart();
        wbPart.Workbook = new Workbook();
        AddStyles(wbPart);
        var sheets = wbPart.Workbook.AppendChild(new Sheets());
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        uint id = 1;
        foreach (var table in tables)
        {
            var wsPart = wbPart.AddNewPart<WorksheetPart>();
            wsPart.Worksheet = BuildSheet(table);
            sheets.Append(new Sheet { Id = wbPart.GetIdOfPart(wsPart), SheetId = id++, Name = SheetName(table.Title, names) });
        }
        wbPart.Workbook.Save();
    }

    public static void Write(IReadOnlyList<ReportTable> tables, string path)
    {
        using var stream = File.Create(path);
        Write(tables, stream);
    }

    private static Worksheet BuildSheet(ReportTable table)
    {
        var ws = new Worksheet();
        // Başlık satırını dondur
        ws.Append(new SheetViews(new SheetView(new Pane
        {
            VerticalSplit = 1, TopLeftCell = "A2", ActivePane = PaneValues.BottomLeft, State = PaneStateValues.Frozen,
        }) { WorkbookViewId = 0 }));

        var columns = new Columns();
        for (var c = 0; c < table.Headers.Count; c++)
        {
            var longest = table.Rows.Select(r => r.ElementAtOrDefault(c)?.Length ?? 0).Append(table.Headers[c].Length).Max();
            columns.Append(new Column { Min = (uint)c + 1, Max = (uint)c + 1, Width = Math.Clamp(longest + 2, 6, 60), CustomWidth = true });
        }
        ws.Append(columns);

        var data = new SheetData();
        data.Append(Row(1, table.Headers, styleIndex: 1));
        uint r = 2;
        foreach (var row in table.Rows)
        {
            data.Append(Row(r++, row, styleIndex: 0));
        }
        ws.Append(data);
        if (table.Headers.Count > 0)
        {
            ws.Append(new AutoFilter { Reference = $"A1:{ColumnName(table.Headers.Count)}{Math.Max(1, r - 1)}" });
        }
        return ws;
    }

    private static Row Row(uint index, IReadOnlyList<string> values, uint styleIndex)
    {
        var row = new Row { RowIndex = index };
        for (var c = 0; c < values.Count; c++)
        {
            row.Append(new Cell
            {
                CellReference = $"{ColumnName(c + 1)}{index}",
                DataType = CellValues.InlineString,
                StyleIndex = styleIndex,
                InlineString = new InlineString(new Text(Clean(values[c])) { Space = SpaceProcessingModeValues.Preserve }),
            });
        }
        return row;
    }

    /// <summary>XML'de geçersiz kontrol karakterlerini atar (dosya bozulmasın).</summary>
    private static string Clean(string? s) =>
        string.IsNullOrEmpty(s) ? string.Empty : new string(s.Where(ch => ch is '\t' or '\n' or '\r' || ch >= ' ').ToArray());

    private static string ColumnName(int n)
    {
        var name = string.Empty;
        while (n > 0)
        {
            var m = (n - 1) % 26;
            name = (char)('A' + m) + name;
            n = (n - 1) / 26;
        }
        return name;
    }

    /// <summary>Excel sayfa adı: en çok 31 karakter, : \ / ? * [ ] yerine '-', tekil.</summary>
    private static string SheetName(string title, HashSet<string> used)
    {
        var clean = new string(title.Select(ch => ch is ':' or '\\' or '/' or '?' or '*' or '[' or ']' ? '-' : ch).ToArray()).Trim();
        if (clean.Length == 0)
        {
            clean = "Rapor";
        }
        clean = clean.Length > 31 ? clean[..31] : clean;
        var name = clean;
        for (var i = 2; !used.Add(name); i++)
        {
            var suffix = $" {i}";
            name = (clean.Length + suffix.Length > 31 ? clean[..(31 - suffix.Length)] : clean) + suffix;
        }
        return name;
    }

    private static void AddStyles(WorkbookPart wb)
    {
        var styles = wb.AddNewPart<WorkbookStylesPart>();
        styles.Stylesheet = new Stylesheet(
            new Fonts(new Font(), new Font(new Bold())),
            new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill { PatternType = PatternValues.Gray125 })),
            new Borders(new Border()),
            new CellFormats(
                new CellFormat { FontId = 0, FillId = 0, BorderId = 0 },
                new CellFormat { FontId = 1, FillId = 0, BorderId = 0, ApplyFont = true }));
        styles.Stylesheet.Save();
    }
}
