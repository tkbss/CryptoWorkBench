using System.Net;
using System.Security.Cryptography;
using System.Text;
using CryptoWorkBench.Automation.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http.Features;

[assembly: InternalsVisibleTo("CryptoWorkBench.Mcp.Tests")]

namespace CryptoWorkBench.Mcp;

/// <summary>One loopback listener and one host-issued credential for a single desktop activation.</summary>
public sealed class LocalMcpHost : IAsyncDisposable
{
    public const string Endpoint = "https://127.0.0.1:51837/mcp";
    private readonly WebApplication app;
    private readonly byte[] expected;
    private readonly LocalMcpIdentity identity;
    private readonly CancellationTokenSource lifetime = new();
    private Task? disposal;
    private int revoked;
    public string CertificateFingerprint => identity.Fingerprint;
    public string TrustBundlePath => identity.TrustBundlePath;
    public DateTimeOffset ExpiresAt => identity.ExpiresAt;
    public event Action? Expired;

    internal Uri BoundEndpoint => new(new Uri(app.Urls.Single()), "/mcp");

    public LocalMcpHost(IWorkbenchAutomationV2 service, AutomationCaller caller, string token)
        : this(service, caller, token, new Uri(Endpoint).Port) { }

    internal LocalMcpHost(IWorkbenchAutomationV2 service, AutomationCaller caller, string token, int port,
        TimeSpan? sessionLifetime = null)
    {
        identity = new LocalMcpIdentity(sessionLifetime ?? TimeSpan.FromHours(8));
        expected = Encoding.ASCII.GetBytes("Bearer " + token);
        try
        {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Production" });
        builder.Configuration.Sources.Clear();
        builder.WebHost.UseKestrelCore().UseKestrelHttpsConfiguration().UseSockets().ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, port, listen => listen.UseHttps(https =>
            {
                https.ServerCertificate = identity.Certificate;
                https.HandshakeTimeout = TimeSpan.FromSeconds(5);
            }));
            options.Limits.MaxRequestBodySize = AutomationContracts.MaxHttpBodyBytes;
            options.Limits.MaxConcurrentConnections = 32;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(5);
            options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(15);
            options.AddServerHeader = false;
        });
        builder.Services.AddSingleton<IHostLifetime, DesktopLifetime>();
        builder.Services.AddRouting();
        builder.Services.AddWorkbenchV2(service, _ => Volatile.Read(ref revoked) == 0
            ? caller : throw new InvalidOperationException());
        app = builder.Build();
        app.Use(async (context, next) =>
        {
            var request = context.Request;
            if (request.Host.Value != $"127.0.0.1:{context.Connection.LocalPort}" || request.Headers.ContainsKey("Origin") ||
                context.Features.Get<IHttpRequestFeature>()?.RawTarget != "/mcp")
            {
                context.Response.StatusCode = 403;
                return;
            }
            var authorization = request.Headers.Authorization;
            if (DateTimeOffset.UtcNow >= ExpiresAt || Volatile.Read(ref revoked) != 0 || authorization.Count != 1 ||
                authorization[0] is not { } value || value.Length != expected.Length ||
                !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(value), expected))
            {
                context.Response.StatusCode = 401;
                return;
            }
            await next(context);
        });
        app.UseRouting();
        app.MapWorkbenchV2("/mcp");
        }
        catch
        {
            try { if (app != null) app.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            finally
            {
                CryptographicOperations.ZeroMemory(expected);
                identity.Dispose();
                lifetime.Dispose();
            }
            throw;
        }
    }

    public async Task StartAsync()
    {
        await app.StartAsync().ConfigureAwait(false);
        _ = ExpireAsync();
    }
    private async Task ExpireAsync()
    {
        try
        {
            var remaining = ExpiresAt - DateTimeOffset.UtcNow;
            await Task.Delay(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, lifetime.Token).ConfigureAwait(false);
            Revoke();
            Expired?.Invoke();
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }
    public void Revoke() => Interlocked.Exchange(ref revoked, 1);
    public ValueTask DisposeAsync()
    {
        lock (lifetime) return new(disposal ??= DisposeCoreAsync());
    }
    private async Task DisposeCoreAsync()
    {
        Revoke();
        lifetime.Cancel();
        try { await app.StopAsync().ConfigureAwait(false); }
        finally
        {
            try { await app.DisposeAsync().ConfigureAwait(false); }
            finally
            {
                CryptographicOperations.ZeroMemory(expected);
                identity.Dispose();
                await identity.PublicCleanup.ConfigureAwait(false);
                lifetime.Dispose();
            }
        }
    }

    private sealed class DesktopLifetime : IHostLifetime
    {
        public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
