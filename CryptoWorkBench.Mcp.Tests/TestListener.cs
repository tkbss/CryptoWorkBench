using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CryptoWorkBench.Mcp.Tests;

internal sealed class TestListener : IAsyncDisposable
{
    private readonly WebApplication app;
    public Uri Endpoint { get; }
    public FakeAutomationService Service { get; }
    public CapturedLogs Logs { get; }
    public System.Collections.Concurrent.ConcurrentQueue<string> ProtocolVersions { get; }
    private TestListener(WebApplication application, FakeAutomationService service, System.Collections.Concurrent.ConcurrentQueue<string> versions, CapturedLogs logs)
    { app = application; Service = service; ProtocolVersions = versions; Logs = logs; Endpoint = new Uri(app.Urls.Single() + "/mcp"); }
    public static async Task<TestListener> StartAsync(FakeAutomationService? service = null, bool normalizeParseErrors = true)
    {
        service ??= new();
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Testing" });
        builder.Configuration.Sources.Clear();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, 0);
            options.Limits.MaxRequestBodySize = Automation.Abstractions.AutomationContracts.MaxHttpBodyBytes;
        });
        var logs = new CapturedLogs();
        builder.Logging.ClearProviders().AddProvider(logs);
        builder.Services.AddWorkbenchStatusProbe(service, () => FakeAutomationService.Caller);
        var app = builder.Build();
        var versions = new System.Collections.Concurrent.ConcurrentQueue<string>();
        app.Use((context, next) => { versions.Enqueue(context.Request.Headers["MCP-Protocol-Version"].ToString()); return next(context); });
        app.UseRouting();
        if (normalizeParseErrors) app.UseSafeMcpParseErrors();
        app.MapMcp("/mcp").WithMetadata(new McpParseErrorProtectionMetadata());
        try { await app.StartAsync(); return new(app, service, versions, logs); }
        catch { await app.DisposeAsync(); throw; }
    }
    public async ValueTask DisposeAsync()
    {
        Service.StatusRelease.TrySetResult();
        Service.FinishExecution.TrySetResult();
        await app.StopAsync();
        await app.DisposeAsync();
    }
}
