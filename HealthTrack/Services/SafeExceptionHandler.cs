using System.Diagnostics;
using HealthTrack.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace HealthTrack.Services;

// Render without re-running authentication, database-backed navigation or normal action filters.
public class SafeExceptionHandler(ILogger<SafeExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled request {RequestId}", context.TraceIdentifier);
        context.Response.Headers.CacheControl = "no-store";
        var result = new ViewResult
        {
            ViewName = "~/Views/Shared/Error.cshtml",
            StatusCode = StatusCodes.Status500InternalServerError,
            ViewData = new ViewDataDictionary<ErrorViewModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = new ErrorViewModel { RequestId = Activity.Current?.Id ?? context.TraceIdentifier }
            }
        };
        await result.ExecuteResultAsync(new ActionContext(context, new RouteData(), new ActionDescriptor()));
        return true;
    }
}
