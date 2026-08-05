using CampaignManager.Application.Admin.Reports;
using CampaignManager.Infrastructure.Reporting;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampaignManager.AdminUI.Controllers;

[Authorize]
public sealed class ReportsController : Controller
{
    private readonly ISender _sender;
    private readonly IReportExporter _exporter;

    public ReportsController(ISender sender, IReportExporter exporter)
    {
        _sender = sender;
        _exporter = exporter;
    }

    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var (fromDate, toDate) = Range(from, to);
        ViewData["From"] = fromDate;
        ViewData["To"] = toDate;
        return View(await _sender.Send(new GetReportQuery(fromDate, toDate), ct));
    }

    public async Task<IActionResult> ExportCsv(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var (fromDate, toDate) = Range(from, to);
        var report = await _sender.Send(new GetReportQuery(fromDate, toDate), ct);
        return File(_exporter.ToCsv(report), "text/csv", $"campaign-report-{fromDate}-{toDate}.csv");
    }

    public async Task<IActionResult> ExportExcel(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var (fromDate, toDate) = Range(from, to);
        var report = await _sender.Send(new GetReportQuery(fromDate, toDate), ct);
        return File(_exporter.ToExcel(report),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"campaign-report-{fromDate}-{toDate}.xlsx");
    }

    public async Task<IActionResult> ExportPdf(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var (fromDate, toDate) = Range(from, to);
        var report = await _sender.Send(new GetReportQuery(fromDate, toDate), ct);
        return File(_exporter.ToPdf(report, "Campaign Manager Report", fromDate, toDate),
            "application/pdf", $"campaign-report-{fromDate}-{toDate}.pdf");
    }

    private static (DateOnly From, DateOnly To) Range(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return (from ?? today.AddDays(-29), to ?? today);
    }
}
