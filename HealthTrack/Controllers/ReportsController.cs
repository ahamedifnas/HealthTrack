using System.Security.Claims;
using HealthTrack.Models;
using HealthTrack.Models.Reports;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Controllers;

[Authorize(Roles = "Admin,Trainer")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ReportsController(ReportService reports, AnalyticsService analytics) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (User.IsInRole(AppRoles.Admin)) return RedirectToAction(nameof(Admin));
        return View(await reports.ClientsAsync(UserId));
    }

    [HttpGet, Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Admin() => View("Report", await reports.AdminAsync());

    [HttpGet, Authorize(Policy = FeaturePolicies.Trainer)]
    public Task<IActionResult> Progress(string clientId, ReportFilter filter) => ClientReport(clientId, ClientReportKind.Progress, filter);

    [HttpGet, Authorize(Policy = FeaturePolicies.Trainer)]
    public Task<IActionResult> Workout(string clientId, ReportFilter filter) => ClientReport(clientId, ClientReportKind.Workout, filter);

    [HttpGet, Authorize(Policy = FeaturePolicies.Trainer)]
    public Task<IActionResult> Diet(string clientId, ReportFilter filter) => ClientReport(clientId, ClientReportKind.Diet, filter);

    [HttpGet, Authorize(Policy = FeaturePolicies.Trainer)]
    public Task<IActionResult> Appointments(string clientId, ReportFilter filter) => ClientReport(clientId, ClientReportKind.Appointments, filter);

    [HttpGet, Authorize(Policy = FeaturePolicies.Trainer)]
    public async Task<IActionResult> Analytics() => View("Report", await analytics.BuildAsync(UserId));

    private async Task<IActionResult> ClientReport(string clientId, ClientReportKind kind, ReportFilter filter)
    {
        if (string.IsNullOrWhiteSpace(clientId)) return NotFound();
        if (kind != ClientReportKind.Appointments && filter.Status.HasValue) ModelState.AddModelError("Status", "Status applies only to appointment reports.");
        if (!ModelState.IsValid) { Response.StatusCode = 400; return View("InvalidFilter"); }
        var report = await reports.ClientAsync(UserId, clientId, kind, filter);
        if (report is not null)
        {
            report.FilterClientId = clientId;
            report.ShowStatusFilter = kind == ClientReportKind.Appointments;
            report.DownloadUrl = Url.Action("Trainer", "ReportDownloads", new { kind, clientId, fromDate = filter.FromDate?.ToString("yyyy-MM-dd"), toDate = filter.ToDate?.ToString("yyyy-MM-dd"), status = filter.Status, download = true });
        }
        return report is null ? NotFound() : View("Report", report);
    }
}
