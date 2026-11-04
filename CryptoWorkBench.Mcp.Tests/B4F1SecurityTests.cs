using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CryptoWorkBench.Automation.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace CryptoWorkBench.Mcp.Tests;

[NonParallelizable]
public class B4F1SecurityTests
{
    [Test]
    public async Task CertificateCanAuthenticateOnThisPlatform()
    {
        using var identity = new LocalMcpIdentity(TimeSpan.FromMinutes(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var server = Task.Run(async () =>
        {
            using var peer = await listener.AcceptTcpClientAsync();
            using var tls = new SslStream(peer.GetStream());
            await tls.AuthenticateAsServerAsync(identity.Certificate);
        });
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        using var stream = new SslStream(client.GetStream(), false, (_, certificate, _, _) =>
            certificate != null && Convert.ToHexString(SHA256.HashData(certificate.GetRawCertData())) == identity.Fingerprint);
        try { await stream.AuthenticateAsClientAsync("127.0.0.1"); }
        finally { await server; }
    }
    private const string Body = """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"workbench_get_status","arguments":{"contractVersion":"2"}}}""";
    private const string Canary = "F1_PRIVATE_CANARY";
    private static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static IWorkbenchAutomationV2 Service() => DispatchProxy.Create<IWorkbenchAutomationV2, BindingV2Tests.Probe>();

    internal static HttpClient CreateClient(Uri endpoint, string fingerprint, string token)
    {
        var client = new HttpClient(new HttpClientHandler
        {
            UseProxy = false, AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                certificate != null && Convert.ToHexString(SHA256.HashData(certificate.RawData)) == fingerprint
        });
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static async Task<string> Raw(LocalMcpHost host, string token, string path = "/mcp",
        string? version = "2025-11-25", string headers = "", string? authority = null, string body = Body)
    {
        using var socket = new TcpClient();
        await socket.ConnectAsync(IPAddress.Loopback, host.BoundEndpoint.Port);
        using var tls = new SslStream(socket.GetStream(), false, (_, certificate, _, _) =>
            certificate != null && Convert.ToHexString(SHA256.HashData(certificate.GetRawCertData())) == host.CertificateFingerprint);
        await tls.AuthenticateAsClientAsync("127.0.0.1");
        var request = $"POST {path} HTTP/1.1\r\nHost: {authority ?? host.BoundEndpoint.Authority}\r\nAuthorization: Bearer {token}\r\n" +
            (version == null ? "" : $"MCP-Protocol-Version: {version}\r\n") + headers +
            $"Accept: application/json, text/event-stream\r\nContent-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}";
        await tls.WriteAsync(Encoding.UTF8.GetBytes(request));
        using var reader = new StreamReader(tls);
        return await reader.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(15));
    }

    [Test]
    public async Task RawTargetsAndHostAreExact()
    {
        var service = Service(); var token = Token();
        await using var host = new LocalMcpHost(service, FakeAutomationService.Caller, token, 0);
        await host.StartAsync();
        foreach (var path in new[] { "/MCP", "/%6dcp", "/x/../mcp", "/mcp?", "/mcp?a=1" })
            Assert.That(await Raw(host, token, path), Does.StartWith("HTTP/1.1 403"));
        foreach (var authority in new[] { "localhost", "127.0.0.1", "127.0.0.1:1", "127.0.0.1.evil:51837" })
            Assert.That(await Raw(host, token, authority: authority), Does.StartWith("HTTP/1.1 403"));
        Assert.That(((BindingV2Tests.Probe)(object)service).Calls, Is.Zero);
        Assert.That(await Raw(host, token), Does.StartWith("HTTP/1.1 200"));
        Assert.That(((BindingV2Tests.Probe)(object)service).Calls, Is.EqualTo(1));
    }

    [Test]
    public async Task RoutingContradictionsAndCanariesNeverReachDispatch()
    {
        var service = Service(); var token = Token();
        await using var host = new LocalMcpHost(service, FakeAutomationService.Caller, token, 0);
        await host.StartAsync();
        string modern = Body.Replace("\"arguments\":", "\"_meta\":{\"io.modelcontextprotocol/protocolVersion\":\"2026-07-28\",\"io.modelcontextprotocol/clientCapabilities\":{},\"io.modelcontextprotocol/clientInfo\":{\"name\":\"f1\",\"version\":\"1\"}},\"arguments\":");
        foreach (string? revision in new[] { "2026-07-28", null })
        {
            var response = await Raw(host, token, version: revision,
                headers: $"Mcp-Method: tools/call\r\nMcp-Name: {Canary}\r\n", body: modern);
            Assert.That(response, Does.StartWith("HTTP/1.1 400").And.Not.Contain(Canary));
        }
        Assert.That(await Raw(host, token, version: Canary), Does.StartWith("HTTP/1.1 400").And.Not.Contain(Canary));
        var unknown = await Raw(host, token, body: Body.Replace("workbench_get_status", Canary));
        Assert.That(unknown, Does.StartWith("HTTP/1.1 200").And.Contain("-32602").And.Not.Contain(Canary));
        Assert.That(((BindingV2Tests.Probe)(object)service).Calls, Is.Zero);
        Assert.That(await Raw(host, token, version: null), Does.StartWith("HTTP/1.1 200"));
        Assert.That(await Raw(host, token, version: "2026-07-28",
            headers: "Mcp-Method: tools/call\r\nMcp-Name: workbench_get_status\r\n", body: modern), Does.StartWith("HTTP/1.1 200"));
        Assert.That(((BindingV2Tests.Probe)(object)service).Calls, Is.EqualTo(2));
    }

    internal sealed class Replacement : IAsyncDisposable
    {
        private readonly LocalMcpIdentity identity;
        private readonly WebApplication app;
        internal int HttpRequests;
        internal string TrustBundlePath => identity.TrustBundlePath;
        internal string Fingerprint => identity.Fingerprint;
        internal Uri Endpoint => new(new Uri(app.Urls.Single()), "/mcp");
        internal Replacement(int port, TimeSpan? lifetime = null)
        {
            identity = new(lifetime ?? TimeSpan.FromMinutes(5));
            var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { Args = [] });
            builder.WebHost.UseKestrelCore().UseKestrelHttpsConfiguration().UseSockets()
                .ConfigureKestrel(k => k.Listen(IPAddress.Loopback, port, listen => listen.UseHttps(identity.Certificate)));
            app = builder.Build();
            app.Run(async context =>
            {
                Interlocked.Increment(ref HttpRequests);
                await context.Response.WriteAsync("""{"jsonrpc":"2.0","id":1,"result":{"isError":false,"content":[{"type":"text","text":"F1_FAKE_SUCCESS"}]}}""");
            });
        }
        internal Task StartAsync() => app.StartAsync();
        public async ValueTask DisposeAsync() { await app.StopAsync(); await app.DisposeAsync(); identity.Dispose(); }
    }

    [Test]
    public void SessionTrustFileIsPublicLockedAndRemovedOnDispose()
    {
        var identity = new LocalMcpIdentity(TimeSpan.FromMinutes(1));
        string path = identity.TrustBundlePath;
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(file))
            Assert.That(reader.ReadToEnd(), Does.Contain("BEGIN CERTIFICATE").And.Not.Contain("PRIVATE KEY"));
        if (OperatingSystem.IsWindows())
            Assert.Throws<IOException>(() => { using var denied = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); });
        identity.Dispose();
        Assert.That(File.Exists(path), Is.False);
    }

    [Test]
    public async Task ReplacementCertificateIsRejectedBeforeAnyHttpData()
    {
        var token = Token();
        var host = new LocalMcpHost(Service(), FakeAutomationService.Caller, token, 0);
        await host.StartAsync();
        var endpoint = host.BoundEndpoint;
        using var client = CreateClient(endpoint, host.CertificateFingerprint, token);
        client.DefaultRequestHeaders.ConnectionClose = true;
        using var ok = await client.GetAsync(endpoint);
        Assert.That(ok.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
        await host.DisposeAsync();
        await using var replacement = new Replacement(endpoint.Port);
        await replacement.StartAsync();
        for (int i = 0; i < 2; i++)
            Assert.ThrowsAsync<HttpRequestException>(async () => await client.PostAsync(endpoint, new StringContent(Canary)));
        using var noTrust = new HttpClient(new HttpClientHandler { UseProxy = false });
        Assert.ThrowsAsync<HttpRequestException>(async () => await noTrust.GetAsync(endpoint));
        Assert.That(replacement.HttpRequests, Is.Zero);
    }

    [Test]
    public async Task ExpirationRevokesAnAlreadyEstablishedClient()
    {
        var token = Token();
        await using var host = new LocalMcpHost(Service(), FakeAutomationService.Caller, token, 0, TimeSpan.FromSeconds(3));
        var expired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Expired += () => expired.TrySetResult();
        await host.StartAsync();
        using var client = CreateClient(host.BoundEndpoint, host.CertificateFingerprint, token);
        using var first = await client.GetAsync(host.BoundEndpoint);
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
        await expired.Task.WaitAsync(TimeSpan.FromSeconds(6));
        using var second = await client.GetAsync(host.BoundEndpoint);
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task SlowUnauthenticatedHeadersReleaseConnectionSlots()
    {
        var token = Token();
        await using var host = new LocalMcpHost(Service(), FakeAutomationService.Caller, token, 0);
        await host.StartAsync();
        var connections = new List<SslStream>();
        try
        {
            for (int i = 0; i < 32; i++)
            {
                var socket = new TcpClient();
                await socket.ConnectAsync(IPAddress.Loopback, host.BoundEndpoint.Port);
                var tls = new SslStream(socket.GetStream(), false, (_, certificate, _, _) =>
                    certificate != null && Convert.ToHexString(SHA256.HashData(certificate.GetRawCertData())) == host.CertificateFingerprint);
                connections.Add(tls);
                await tls.AuthenticateAsClientAsync("127.0.0.1");
                await tls.WriteAsync(Encoding.ASCII.GetBytes("POST /mcp HTTP/1.1\r\n"));
            }
            // Kestrel's timeout heartbeat adds up to one second to the five-second header deadline.
            await Task.Delay(TimeSpan.FromSeconds(7));
            Assert.That(await Raw(host, token), Does.StartWith("HTTP/1.1 200"));
        }
        finally { foreach (var connection in connections) connection.Dispose(); }
    }

    [Test, Explicit("Requires installed Codex.")]
    public async Task InstalledCodexRejectsPortTakeoverOverHttps()
    {
        var token = Token(); var service = Service();
        await using var host = new LocalMcpHost(service, FakeAutomationService.Caller, token, 0);
        await host.StartAsync();
        var endpoint = host.BoundEndpoint;
        await CodexCompatibilityTests.WithCodex(endpoint, true, async (discovery, call) =>
        {
            Assert.That(discovery.GetRawText(), Does.Contain("workbench_get_status"));
            await call("workbench_get_status", new StatusV2("2"));
            Assert.That(((BindingV2Tests.Probe)(object)service).Calls, Is.EqualTo(1));
            host.Revoke();
            await host.DisposeAsync();
            await using var replacement = new Replacement(endpoint.Port);
            await replacement.StartAsync();
            for (int i = 0; i < 2; i++)
            {
                try
                {
                    var answer = await call("workbench_update_editor", new EditorEditV2("2", Guid.NewGuid(), Guid.NewGuid(),
                        Guid.NewGuid(), "1", EditorEditMode.Append, Canary));
                    Assert.That(answer.GetProperty("isError").GetBoolean(), Is.True);
                    Assert.That(answer.GetRawText(), Does.Not.Contain("F1_FAKE_SUCCESS").And.Not.Contain(Canary));
                }
                catch (InvalidOperationException error) when (error.Message == "Codex mcpServer/tool/call failed (-32603).") { }
            }
            Assert.That(replacement.HttpRequests, Is.Zero);
            TestContext.Out.WriteLine("Real Codex discovery/status succeeded. Two post-disconnect edits rejected; replacement decrypted HTTP requests = 0.");
        }, host.CertificateFingerprint, token);
    }

    [TestCase("missing"), TestCase("different"), TestCase("expired")]
    [Explicit("Requires installed Codex.")]
    public async Task InstalledCodexRefusesUntrustedOrExpiredServer(string trust)
    {
        using var unrelated = new LocalMcpIdentity(TimeSpan.FromMinutes(5));
        await using var server = new Replacement(0, trust == "expired" ? TimeSpan.FromSeconds(-5) : null);
        await server.StartAsync();
        string bundle = trust switch
        {
            "missing" => "",
            "different" => unrelated.Fingerprint,
            _ => server.Fingerprint
        };
        await CodexCompatibilityTests.WithCodex(server.Endpoint, true, async (_, call) =>
        {
            await Assert.ThatAsync(async () => await call("workbench_get_status", new StatusV2("2")),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(server.HttpRequests, Is.Zero);
            TestContext.Out.WriteLine($"Trust case {trust}: no decrypted HTTP requests.");
        }, bundle, Token());
    }
}
