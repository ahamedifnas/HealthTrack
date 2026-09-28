using HealthTrack.Models;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Controllers;

[Authorize(Roles = AppRoles.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AdminReportsController(ReportService reports) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Print() => View(await reports.AdminAsync());
}
