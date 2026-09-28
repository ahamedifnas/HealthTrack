using System.Security.Claims;
using HealthTrack.Models;
using HealthTrack.Models.Reports;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Controllers;

[Authorize(Roles = "Admin,Trainer")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ReportsController(ReportService reports) : Controller
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
    public Task<IActionResult> Progress(string clientId) => ClientReport(clientId, ClientReportKind.Progress);

    [HttpGet, Authorize(Policy = FeaturePolicies.Trainer)]
    public Task<IActionResult> Workout(string clientId) => ClientReport(clientId, ClientReportKind.Workout);

    [HttpGet, Authorize(Policy = FeaturePolicies.Trainer)]
    public Task<IActionResult> Diet(string clientId) => ClientReport(clientId, ClientReportKind.Diet);

    [HttpGet, Authorize(Policy = FeaturePolicies.Trainer)]
    public Task<IActionResult> Appointments(string clientId) => ClientReport(clientId, ClientReportKind.Appointments);

    private async Task<IActionResult> ClientReport(string clientId, ClientReportKind kind)
    {
        if (string.IsNullOrWhiteSpace(clientId)) return NotFound();
        var report = await reports.ClientAsync(UserId, clientId, kind);
        return report is null ? NotFound() : View("Report", report);
    }
}
