using CampaignManager.Application.Admin.Reports;

namespace CampaignManager.Infrastructure.Reporting;

public interface IReportExporter
{
    byte[] ToCsv(ReportSummary report);
    byte[] ToExcel(ReportSummary report);
    byte[] ToPdf(ReportSummary report, string title, DateOnly fromDate, DateOnly toDate);
}
