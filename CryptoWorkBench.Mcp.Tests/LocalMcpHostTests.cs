using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using CryptoWorkBench.Automation.Abstractions;
using NUnit.Framework;

namespace CryptoWorkBench.Mcp.Tests;

public class LocalMcpHostTests
{
    [Test]
    public async Task OccupiedLoopbackPortFailsWithoutBindingAnotherEndpoint()
    {
        using var occupied = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        int port = ((IPEndPoint)occupied.LocalEndpoint).Port;
        await using var host = new LocalMcpHost(DispatchProxy.Create<IWorkbenchAutomationV2, BindingV2Tests.Probe>(),
            FakeAutomationService.Caller, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), port);
        Assert.CatchAsync<IOException>(async () => await host.StartAsync());
    }

    [Test]
    public async Task OnlyPairedLoopbackPostsReachTheOriginalCallerUntilRevoked()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var service = DispatchProxy.Create<IWorkbenchAutomationV2, BindingV2Tests.Probe>();
        await using var host = new LocalMcpHost(service, FakeAutomationService.Caller, token, 0);
        await host.StartAsync();
        Assert.That(host.BoundEndpoint.Host, Is.EqualTo("127.0.0.1"));
        using var client = B4F1SecurityTests.CreateClient(host.BoundEndpoint, host.CertificateFingerprint, token);
        client.DefaultRequestHeaders.Authorization = null;
        async Task<HttpStatusCode> Post(string? bearer, string? origin = null, string? authority = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, host.BoundEndpoint);
            request.Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"workbench_get_status","arguments":{"contractVersion":"2"}}}""", Encoding.UTF8, "application/json");
            request.Headers.Add("Accept", "application/json, text/event-stream");
            request.Headers.Add("MCP-Protocol-Version", "2025-11-25");
            if (bearer != null) request.Headers.Authorization = new("Bearer", bearer);
            if (origin != null) request.Headers.Add("Origin", origin);
            if (authority != null) request.Headers.Host = authority;
            using var result = await client.SendAsync(request);
            return result.StatusCode;
        }
        Assert.That(await Post(null), Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(await Post(new string('!', token.Length)), Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(await Post(token, "null"), Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(await Post(token, authority: "localhost"), Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(((BindingV2Tests.Probe)(object)service).Calls, Is.Zero);
        Assert.That(await Post(token), Is.EqualTo(HttpStatusCode.OK));
        host.Revoke();
        Assert.That(await Post(token), Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(((BindingV2Tests.Probe)(object)service).Calls, Is.EqualTo(1));
    }
}
