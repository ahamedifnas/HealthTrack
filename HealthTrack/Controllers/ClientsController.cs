using System.Security.Claims;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers;

[Authorize(Policy = FeaturePolicies.Trainer)]
public class ClientsController(PlanAccess access) : Controller
{
    public async Task<IActionResult> Index() => View(await access.Clients(
        User.FindFirstValue(ClaimTypes.NameIdentifier)!).OrderBy(x => x.FullName).ToListAsync());
}

