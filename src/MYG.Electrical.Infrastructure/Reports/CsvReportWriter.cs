using System.Text;
using MYG.Electrical.Core.Reports;

namespace MYG.Electrical.Infrastructure.Reports;

/// <summary>
/// Raporu CSV olarak yazar: UTF-8 BOM (Excel Türkçe karakterleri doğru açsın), ';' ayırıcı (Türkçe bölge ayarında
/// Excel'in varsayılanı), gerektiğinde tırnaklama. Formül enjeksiyonuna karşı =, +, @ ile başlayan hücreler
/// tek tırnakla korunur. Excel için <see cref="XlsxReportWriter"/> önerilir: CSV'de "-K1" gibi etiketleri Excel
/// formül sanabilir; xlsx'te bütün hücreler metindir.
/// </summary>
public static class CsvReportWriter
{
    public const char Separator = ';';

    public static void Write(ReportTable table, Stream output)
    {
        using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);
        writer.WriteLine(string.Join(Separator, table.Headers.Select(Escape)));
        foreach (var row in table.Rows)
        {
            writer.WriteLine(string.Join(Separator, row.Select(Escape)));
        }
    }

    public static void Write(ReportTable table, string path)
    {
        using var stream = File.Create(path);
        Write(table, stream);
    }

    public static string Escape(string? value)
    {
        var v = value ?? string.Empty;
        if (v.Length > 0 && v[0] is '=' or '+' or '@')
        {
            v = "'" + v;
        }
        return v.IndexOfAny([Separator, '"', '\n', '\r']) >= 0 ? "\"" + v.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : v;
    }
}
