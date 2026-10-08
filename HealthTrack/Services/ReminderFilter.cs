using System.Security.Claims;
using HealthTrack.Models;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HealthTrack.Services;

// Run before read actions, never inside a POST's transaction or on anonymous requests.
public class ReminderFilter(ReminderService reminders) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        var controller = context.RouteData.Values["controller"]?.ToString();
        if (HttpMethods.IsGet(context.HttpContext.Request.Method)
            && controller is "Dashboard" or "Appointments" or "Notifications"
            && user.Identity?.IsAuthenticated == true && !user.IsInRole(AppRoles.Admin)
            && (user.IsInRole(AppRoles.Client) || user.IsInRole(AppRoles.Trainer) || user.IsInRole(AppRoles.HealthcareProfessional))
            && user.FindFirstValue(ClaimTypes.NameIdentifier) is { } id)
            await reminders.GenerateAsync(id, user.IsInRole(AppRoles.Client) && !user.IsInRole(AppRoles.Trainer) && !user.IsInRole(AppRoles.HealthcareProfessional));
        await next();
    }
}
