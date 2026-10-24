using System.Net;
using Microsoft.Data.SqlClient;

namespace HealthTrack.Services.Payments;

public sealed class PaymentMode
{
    public const string DemoDatabase = "HealthTrack_LocalDemoOnly";
    public const string Label = "Demo Payment – No Real Money";
    public bool IsDemo { get; }
    public PaymentMode(IConfiguration configuration, IHostEnvironment environment)
    {
        var provider = configuration["Payments:Provider"] ?? "PayHere";
        if (provider != "Demo" && provider != "PayHere")
            throw new InvalidOperationException("Payments:Provider must be Demo or PayHere.");
        IsDemo = provider == "Demo";
        if (!IsDemo) return;
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("Demo payments require Development.");
        var connection = new SqlConnectionStringBuilder(configuration.GetConnectionString("DemoConnection"));
        if (connection.InitialCatalog != DemoDatabase || connection.DataSource != @".\SQLEXPRESS" || !connection.IntegratedSecurity)
            throw new InvalidOperationException("Demo payments require the dedicated local SQL Express database and Windows authentication.");
    }

    // Evaluate the original connection before any forwarding middleware. Never trust proxy headers.
    public static bool IsLocalRequest(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress;
        if (remote is null || !IPAddress.IsLoopback(remote)) return false;
        if (!IsLocalHost(context.Request.Host.Host)) return false;
        if (context.Request.Headers.Keys.Any(k => k.StartsWith("X-Forwarded", StringComparison.OrdinalIgnoreCase)
            || k.Equals("Forwarded", StringComparison.OrdinalIgnoreCase)
            || k.Equals("Via", StringComparison.OrdinalIgnoreCase)
            || k.StartsWith("Ngrok", StringComparison.OrdinalIgnoreCase))) return false;
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            if (!Uri.TryCreate(context.Request.Headers.Origin.ToString(), UriKind.Absolute, out var origin)
                || !IsLocalHost(origin.Host) || origin.Authority != context.Request.Host.Value
                || origin.Scheme != context.Request.Scheme) return false;
        }
        return true;
    }
    private static bool IsLocalHost(string host) => host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host == "127.0.0.1" || host == "[::1]" || host == "::1";
}
