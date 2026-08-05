using System.Globalization;
using System.Text;
using CampaignManager.Application.Admin.Reports;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CampaignManager.Infrastructure.Reporting;

public sealed class ReportExporter : IReportExporter
{
    static ReportExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static readonly string[] Headers =
        ["Date", "Channel", "Provider", "Sent", "Delivered", "Failed", "Rejected", "Expired", "Avg delivery (s)"];

    public byte[] ToCsv(ReportSummary report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', Headers));
        foreach (var row in report.Rows)
        {
            sb.AppendLine(string.Join(',',
                row.Date.ToString("yyyy-MM-dd"), row.Channel, CsvEscape(row.Provider),
                row.Sent, row.Delivered, row.Failed, row.Rejected, row.Expired,
                row.AvgDeliverySeconds?.ToString("0.0", CultureInfo.InvariantCulture) ?? ""));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public byte[] ToExcel(ReportSummary report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Report");
        for (var i = 0; i < Headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = Headers[i];
            sheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var rowIndex = 2;
        foreach (var row in report.Rows)
        {
            sheet.Cell(rowIndex, 1).Value = row.Date.ToString("yyyy-MM-dd");
            sheet.Cell(rowIndex, 2).Value = row.Channel;
            sheet.Cell(rowIndex, 3).Value = row.Provider;
            sheet.Cell(rowIndex, 4).Value = row.Sent;
            sheet.Cell(rowIndex, 5).Value = row.Delivered;
            sheet.Cell(rowIndex, 6).Value = row.Failed;
            sheet.Cell(rowIndex, 7).Value = row.Rejected;
            sheet.Cell(rowIndex, 8).Value = row.Expired;
            if (row.AvgDeliverySeconds is { } avg) sheet.Cell(rowIndex, 9).Value = Math.Round(avg, 1);
            rowIndex++;
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ToPdf(ReportSummary report, string title, DateOnly fromDate, DateOnly toDate)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.Header().Column(col =>
                {
                    col.Item().Text(title).FontSize(18).Bold();
                    col.Item().Text($"{fromDate:yyyy-MM-dd} – {toDate:yyyy-MM-dd}").FontSize(10).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingTop(15).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Total sent: {report.TotalSent:N0}").SemiBold();
                        row.RelativeItem().Text($"Delivered: {report.TotalDelivered:N0}");
                        row.RelativeItem().Text($"Failed: {report.TotalFailed:N0}");
                        row.RelativeItem().Text($"Success rate: {report.SuccessRate:P1}");
                    });

                    col.Item().PaddingTop(15).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn();
                            columns.RelativeColumn(1.3f);
                            columns.RelativeColumn(0.7f);
                            columns.RelativeColumn(0.7f);
                            columns.RelativeColumn(0.7f);
                        });

                        table.Header(header =>
                        {
                            foreach (var text in new[] { "Date", "Channel", "Provider", "Sent", "Delivered", "Failed" })
                            {
                                header.Cell().Text(text).Bold();
                            }
                        });

                        foreach (var row in report.Rows)
                        {
                            table.Cell().Text(row.Date.ToString("yyyy-MM-dd"));
                            table.Cell().Text(row.Channel);
                            table.Cell().Text(row.Provider);
                            table.Cell().Text(row.Sent.ToString("N0"));
                            table.Cell().Text(row.Delivered.ToString("N0"));
                            table.Cell().Text(row.Failed.ToString("N0"));
                        }
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static string CsvEscape(string value) =>
        value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
