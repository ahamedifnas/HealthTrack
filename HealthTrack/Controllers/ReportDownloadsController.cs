using System.Security.Claims;
using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Reports;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ReportDownloadsController(DownloadReportService reports, ReportService trainerReports, IPdfReportService pdf, ApplicationDbContext database) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet, Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Admin(AdminReportKind kind, ReportFilter filter, string? clientId = null, bool download = false)
    {
        if (!Enum.IsDefined(kind)) return BadRequest();
        if (kind != AdminReportKind.Appointments && (filter.FromDate.HasValue || filter.ToDate.HasValue || filter.Status.HasValue || clientId is not null))
            ModelState.AddModelError("", "Date, client and status filters are supported by the Appointment report. Other admin reports show their stated system-wide scope.");
        if (!ModelState.IsValid) return InvalidFilter();
        if (clientId is not null && !await database.Users.AnyAsync(x => x.Id == clientId && database.UserRoles.Any(ur => ur.UserId == x.Id && database.Roles.Any(r => r.Id == ur.RoleId && r.Name == AppRoles.Client)))) return NotFound();
        var report = await reports.AdminAsync(kind, filter, clientId);
        if (kind == AdminReportKind.Appointments)
        {
            report.FilterClients = await database.Users.Where(x => database.UserRoles.Any(ur => ur.UserId == x.Id && database.Roles.Any(r => r.Id == ur.RoleId && r.Name == AppRoles.Client)))
                .OrderBy(x => x.FullName).Select(x => new ReportClient(x.Id, x.FullName, x.Email)).ToListAsync();
            report.FilterClientId = clientId;
            report.Filter = filter;
            report.ShowStatusFilter = true;
        }
        return Deliver(report, kind.ToString(), download);
    }

    [HttpGet, Authorize(Policy = FeaturePolicies.Trainer)]
    public async Task<IActionResult> Trainer(ClientReportKind kind, string clientId, ReportFilter filter, bool download = false)
    {
        if (!Enum.IsDefined(kind)) return BadRequest();
        if (kind != ClientReportKind.Appointments && filter.Status.HasValue) ModelState.AddModelError("Status", "Status applies only to appointment reports.");
        if (!ModelState.IsValid) return InvalidFilter();
        var report = await trainerReports.ClientAsync(UserId, clientId, kind, filter);
        if (report is null) return NotFound();
        report.FilterClientId = clientId;
        report.ShowStatusFilter = kind == ClientReportKind.Appointments;
        return Deliver(report, kind.ToString(), download);
    }

    [HttpGet, Authorize(Policy = FeaturePolicies.Client)]
    public async Task<IActionResult> Client(PersonalReportKind kind, ReportFilter filter, string? clientId = null, bool download = false)
    {
        if (!Enum.IsDefined(kind)) return BadRequest();
        if (clientId is not null && clientId != UserId) return NotFound();
        if (kind != PersonalReportKind.Appointments && filter.Status.HasValue) ModelState.AddModelError("Status", "Status applies only to appointment reports.");
        if (!ModelState.IsValid) return InvalidFilter();
        var report = await reports.PersonalAsync(UserId, kind, filter);
        if (report is null) return NotFound();
        report.ShowStatusFilter = kind == PersonalReportKind.Appointments;
        return Deliver(report, kind.ToString(), download);
    }

    [HttpGet, Authorize(Roles = AppRoles.HealthcareProfessional)]
    public async Task<IActionResult> Healthcare(ReportFilter filter, bool download = false)
    {
        if (!ModelState.IsValid) return InvalidFilter();
        var report = await reports.AppointmentAsync(database.Appointments.Where(x => x.HealthcareProfessionalId == UserId), filter, "Healthcare Professional Appointment Report", User.Identity?.Name);
        report.ShowStatusFilter = true;
        return Deliver(report, "Appointments", download);
    }

    private IActionResult Deliver(ReportViewModel report, string kind, bool download)
    {
        // A filename is REQUIRED: MVC sets Content-Disposition: attachment (never inline).
        if (download) return File(pdf.Generate(report), "application/pdf", pdf.FileName(report));
        report.DownloadKind = kind;
        report.DownloadUrl = Url.Action(ControllerContext.ActionDescriptor.ActionName, new
        {
            kind, clientId = report.FilterClientId, fromDate = report.Filter?.FromDate?.ToString("yyyy-MM-dd"),
            toDate = report.Filter?.ToDate?.ToString("yyyy-MM-dd"), status = report.Filter?.Status, download = true
        });
        return View("~/Views/Reports/Report.cshtml", report);
    }

    private IActionResult InvalidFilter()
    {
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View("~/Views/Reports/InvalidFilter.cshtml");
    }
}
