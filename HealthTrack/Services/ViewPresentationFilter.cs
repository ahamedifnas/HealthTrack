using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HealthTrack.Services;

// Common display values are prepared before Razor runs; views do not query services or the database.
public class ViewPresentationFilter(AppointmentClock clock) : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context) { }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Result is ViewResult view)
        {
            view.ViewData["TimeZoneId"] = clock.TimeZoneId;
            view.ViewData["Today"] = clock.Now.ToString("yyyy-MM-dd");
        }
    }
}
