using System.Net;
using HealthTrack.Services.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace HealthTrack.Tests;

public class PaymentSafetyTests
{
    private sealed class EnvironmentStub(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private static PaymentMode Mode(string environment, string provider, string database = PaymentMode.DemoDatabase) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Payments:Provider"] = provider,
            ["ConnectionStrings:DemoConnection"] = $"Server=.\\SQLEXPRESS;Database={database};Trusted_Connection=True"
        }).Build(), new EnvironmentStub(environment));

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void DemoCannotStartOutsideDevelopment(string environment) =>
        Assert.Throws<InvalidOperationException>(() => Mode(environment, "Demo"));
    [Fact] public void DemoCannotUseExistingDatabase() => Assert.Throws<InvalidOperationException>(() => Mode("Development", "Demo", "HealthTrackDB"));
    [Fact] public void ExplicitDemoUsesIsolatedDatabase() => Assert.True(Mode("Development", "Demo").IsDemo);
    [Fact] public void PayHereRemainsAvailableInProduction() => Assert.False(Mode("Production", "PayHere").IsDemo);
    [Fact] public void UnknownProviderFailsClosed() => Assert.Throws<InvalidOperationException>(() => Mode("Development", "demo"));

    private static DefaultHttpContext Local()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Host = new HostString("127.0.0.1:5076");
        context.Request.Scheme = "http";
        context.Request.Method = "GET";
        return context;
    }
    [Fact] public void DirectLocalRequestAllowed() => Assert.True(PaymentMode.IsLocalRequest(Local()));
    [Theory]
    [InlineData("X-Forwarded-For")]
    [InlineData("X-Forwarded-Host")]
    [InlineData("X-Forwarded-Proto")]
    [InlineData("Forwarded")]
    [InlineData("Via")]
    [InlineData("Ngrok-Trace-Id")]
    public void TunnelHeadersRejectedEvenWithRewrittenLocalHost(string header)
    {
        var c = Local(); c.Request.Headers[header] = "localhost";
        Assert.False(PaymentMode.IsLocalRequest(c));
    }
    [Fact] public void PublicHostRejected() { var c=Local(); c.Request.Host=new HostString("example.ngrok-free.app"); Assert.False(PaymentMode.IsLocalRequest(c)); }
    [Fact] public void RemoteConnectionRejected() { var c=Local(); c.Connection.RemoteIpAddress=IPAddress.Parse("192.0.2.1"); Assert.False(PaymentMode.IsLocalRequest(c)); }
    [Theory]
    [InlineData("")]
    [InlineData("https://example.ngrok-free.app")]
    [InlineData("http://127.0.0.1:9999")]
    [InlineData("null")]
    public void UnsafePostOriginRejected(string origin) { var c=Local(); c.Request.Method="POST"; c.Request.Headers.Origin=origin; Assert.False(PaymentMode.IsLocalRequest(c)); }
    [Fact] public void SameOriginLocalPostAllowed() { var c=Local(); c.Request.Method="POST"; c.Request.Headers.Origin="http://127.0.0.1:5076"; Assert.True(PaymentMode.IsLocalRequest(c)); }
}
