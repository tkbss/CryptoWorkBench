using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using CryptoWorkBench.Automation.Abstractions;
using NUnit.Framework;

namespace CryptoWorkBench.Mcp.Tests;

[NonParallelizable]
public class B4F2SecurityTests
{
    private const string Canary = "F2_PRIVATE_CANARY";
    private static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static IWorkbenchAutomationV2 Service() => DispatchProxy.Create<IWorkbenchAutomationV2, BindingV2Tests.Probe>();

    [Test]
    public async Task AdapterUsesStandardFramesFixedErrorsAndExitsOnInputEof()
    {
        var token = Token();
        await using var host = new LocalMcpHost(Service(), FakeAutomationService.Caller, token, 0);
        await host.StartAsync();
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        var executable = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "../../../../CryptoWorkBenchAvalonia.Desktop/bin", configuration, "net10.0/CryptoWorkBenchAvalonia.Desktop.exe"));
        if (!OperatingSystem.IsWindows()) Assert.Ignore("Windows built desktop adapter.");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("--mcp-stdio");
        start.Environment["CRYPTOWORKBENCH_MCP_TOKEN"] = token;
        start.Environment["CRYPTOWORKBENCH_MCP_CERT_SHA256"] = host.CertificateFingerprint;
        start.Environment["CRYPTOWORKBENCH_MCP_ENDPOINT"] = host.BoundEndpoint.ToString();
        using var process = Process.Start(start)!;
        var errors = process.StandardError.ReadToEndAsync();
        async Task<JsonElement> Send(string request)
        {
            await process.StandardInput.WriteLineAsync(request); await process.StandardInput.FlushAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var line = await process.StandardOutput.ReadLineAsync(timeout.Token);
            Assert.That(line, Is.Not.Null);
            Assert.That(line, Does.Not.Contain(Canary).And.Not.Contain(token));
            return JsonSerializer.Deserialize<JsonElement>(line!);
        }
        try
        {
            var initialized = await Send("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"f2","version":"1"}}}""");
            Assert.That(initialized.GetProperty("result").GetProperty("capabilities").TryGetProperty("tools", out _), Is.True);
            await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
            var unknown = await Send($"{{\"jsonrpc\":\"2.0\",\"id\":\"correlation\",\"method\":\"{Canary}\"}}");
            Assert.That(unknown.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32601));
            Assert.That(unknown.GetProperty("id").GetString(), Is.EqualTo("correlation"));
            var malformed = await Send($"{{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{{\"name\":{{\"{Canary}\":1}}}}}}");
            Assert.That(malformed.TryGetProperty("error", out _), Is.True);
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));
            Assert.That(process.ExitCode, Is.Zero);
            Assert.That(await errors, Does.Not.Contain(Canary).And.Not.Contain(token));
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
            await errors;
        }
    }

    [TestCase("7"), TestCase("\"correlation\""), TestCase("-25")]
    public async Task UnknownMethodsPreserveIdWithoutDispatchOrReflection(string id)
    {
        var service = Service(); var token = Token();
        await using var host = new LocalMcpHost(service, FakeAutomationService.Caller, token, 0);
        await host.StartAsync();
        using var client = PinnedMcpStdio.CreateHttpClient(host.BoundEndpoint, host.CertificateFingerprint, token);
        foreach (var method in new[] { Canary, "resources/read", "tasks/get", "logging/setLevel" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, host.BoundEndpoint);
            request.Headers.Add("Accept", "application/json, text/event-stream");
            request.Content = new StringContent(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = JsonSerializer.Deserialize<JsonElement>(id), method,
                @params = new { input = Canary } }), Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(text);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(json.RootElement.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32601));
            Assert.That(json.RootElement.GetProperty("id").GetRawText(), Is.EqualTo(id));
            Assert.That(json.RootElement.GetProperty("error").GetProperty("message").GetString(), Is.EqualTo("Method not found."));
            Assert.That(text, Does.Not.Contain(Canary));
        }
        Assert.That(((BindingV2Tests.Probe)(object)service).Calls, Is.Zero);
    }

    [TestCase(true), TestCase(false)]
    public async Task PublicCaReadLockCannotPoisonTransportDisposal(bool releaseDuringRetry)
    {
        if (!OperatingSystem.IsWindows()) Assert.Ignore("Windows sharing/CNG proof.");
        var token = Token();
        var host = new LocalMcpHost(Service(), FakeAutomationService.Caller, token, 0);
        await host.StartAsync();
        var endpoint = host.BoundEndpoint;
        var identity = (LocalMcpIdentity)typeof(LocalMcpHost).GetField("identity", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(host)!;
        var keyFile = KeyFile(identity.Certificate);
        using var reader = new FileStream(host.TrustBundlePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var completion = host.DisposeAsync().AsTask();
        if (releaseDuringRetry) { await Task.Delay(50); reader.Dispose(); }
        await completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(File.Exists(keyFile), Is.False, "Private key release precedes public cleanup.");
        Assert.That(host.DisposeAsync().AsTask(), Is.SameAs(completion));
        using var socket = new TcpClient();
        await Assert.ThatAsync(async () => await socket.ConnectAsync(IPAddress.Loopback, endpoint.Port), Throws.TypeOf<SocketException>());
        Assert.That(identity.PublicCleanupPending, Is.EqualTo(!releaseDuringRetry));
        reader.Dispose();
        if (!releaseDuringRetry)
        {
            // Only this test's public residue. No recursive deletion.
            File.Delete(host.TrustBundlePath); Directory.Delete(Path.GetDirectoryName(host.TrustBundlePath)!);
        }
        else Assert.That(File.Exists(host.TrustBundlePath), Is.False);
    }

    internal static string KeyFile(X509Certificate2 certificate)
    {
        using var rsa = (RSACng)certificate.GetRSAPrivateKey()!;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Crypto", "Keys", rsa.Key.UniqueName!);
    }

    [Test]
    public async Task PublicExportAndListenerFailureReleaseActualPrivateKey()
    {
        if (!OperatingSystem.IsWindows()) Assert.Ignore("Windows CNG proof.");
        string? key = null, ca = null;
        Assert.Throws<IOException>(() => new LocalMcpIdentity(TimeSpan.FromMinutes(5), (certificate, path) =>
        {
            key = KeyFile(certificate); ca = path;
            Assert.That(File.Exists(key), Is.True);
            throw new IOException(Canary);
        }));
        Assert.That(key, Is.Not.Null); Assert.That(File.Exists(key), Is.False); Assert.That(File.Exists(ca), Is.False);
        using var occupied = new TcpListener(IPAddress.Loopback, 0); occupied.Start();
        var host = new LocalMcpHost(Service(), FakeAutomationService.Caller, Token(), ((IPEndPoint)occupied.LocalEndpoint).Port);
        var identity = (LocalMcpIdentity)typeof(LocalMcpHost).GetField("identity", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
        key = KeyFile(identity.Certificate);
        await Assert.ThatAsync(async () => await host.StartAsync(), Throws.InstanceOf<IOException>());
        await host.DisposeAsync();
        Assert.That(File.Exists(key), Is.False); Assert.That(File.Exists(identity.TrustBundlePath), Is.False);
    }

    [Test]
    public void FailedTemporaryDirectoryAndHostConstructionReleaseImportedCngKeys()
    {
        if (!OperatingSystem.IsWindows()) Assert.Ignore("Actual Windows CNG resources.");
        var keyDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Crypto", "Keys");
        string[] Keys() => Directory.GetFiles(keyDirectory);
        var before = Keys();
        var badTemp = Path.GetTempFileName();
        string? oldTemp = Environment.GetEnvironmentVariable("TEMP"), oldTmp = Environment.GetEnvironmentVariable("TMP");
        try
        {
            Environment.SetEnvironmentVariable("TEMP", badTemp); Environment.SetEnvironmentVariable("TMP", badTemp);
            Assert.Throws<IOException>(() => new LocalMcpIdentity(TimeSpan.FromMinutes(5)));
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEMP", oldTemp); Environment.SetEnvironmentVariable("TMP", oldTmp);
            File.Delete(badTemp);
        }
        Assert.That(Keys().Except(before), Is.Empty, "No newly imported CNG container survives directory failure.");
        Assert.Catch<ArgumentOutOfRangeException>(() => new LocalMcpHost(Service(), FakeAutomationService.Caller, Token(), -1));
        Assert.That(Keys().Except(before), Is.Empty, "No newly imported CNG container survives host builder failure.");
        // Never delete arbitrary files from the Windows key store.
    }

    [TestCase("isolated"), TestCase("ssl"), TestCase("bundle"), TestCase("proxy")]
    [Explicit("Requires installed Codex and the built desktop adapter executable.")]
    public async Task InstalledCodexPinnedIdentityRejectsForeignPortOwner(string trust)
    {
        var token = Token(); var service = Service();
        await using var host = new LocalMcpHost(service, FakeAutomationService.Caller, token, 0);
        await host.StartAsync();
        var endpoint = host.BoundEndpoint;
        await using var foreign = new WirePeer(endpoint.Port);
        await using var proxy = new WirePeer(0, proxy: true) { TargetPort = endpoint.Port };
        proxy.Start();
        var bundle = Path.GetTempFileName();
        string Public(string path) { using var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); using var r = new StreamReader(f); return r.ReadToEnd(); }
        await File.WriteAllTextAsync(bundle, Public(host.TrustBundlePath) + "\n" + Public(foreign.Identity.TrustBundlePath));
        var environment = new Dictionary<string, string>
        {
            ["SSL_CERT_FILE"] = trust is "ssl" or "proxy" ? foreign.Identity.TrustBundlePath : "",
            ["CODEX_CA_CERTIFICATE"] = trust == "bundle" ? bundle : host.TrustBundlePath,
            ["HTTPS_PROXY"] = trust == "proxy" ? $"http://127.0.0.1:{proxy.Port}" : "",
            ["ALL_PROXY"] = trust == "proxy" ? $"http://127.0.0.1:{proxy.Port}" : "", ["NO_PROXY"] = ""
        };
        try
        {
            await CodexCompatibilityTests.WithCodex(endpoint, true, async (discovery, call) =>
            {
                Assert.That(discovery.GetRawText(), Does.Contain("workbench_update_editor"));
                await call("workbench_get_status", new StatusV2("2"));
                Assert.That(((BindingV2Tests.Probe)(object)service).Calls, Is.EqualTo(1));
                host.Revoke(); await host.DisposeAsync(); foreign.Start();
                for (int i = 0; i < 3; i++)
                    await Reject(call, "workbench_update_editor", new EditorEditV2("2", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "1", EditorEditMode.Append, Canary));
                Assert.That(foreign.Tcp, Is.GreaterThanOrEqualTo(3));
                // Schannel's server-side handshake may complete before the client's pin rejection.
                // It is not proof that the client accepted the peer. No application bytes may follow.
                Assert.That(foreign.Tls, Is.LessThanOrEqualTo(foreign.Tcp)); Assert.That(foreign.Http, Is.Zero);
                Assert.That(foreign.Authorization, Is.Zero); Assert.That(foreign.Bodies, Is.Zero);
                Assert.That(proxy.WorkbenchConnect, Is.Zero, "Proxy configuration must not route adapter traffic.");
                TestContext.Out.WriteLine($"{trust}: TCP={foreign.Tcp}, serverTLS={foreign.Tls}, HTTP={foreign.Http}, bearer={foreign.Authorization}, body={foreign.Bodies}, fakeAccepted=0; proxyWorkbenchCONNECT={proxy.WorkbenchConnect}");
            }, host.CertificateFingerprint, token, environment);
        }
        finally { File.Delete(bundle); }
    }

    internal static async Task Reject(Func<string, object, Task<JsonElement>> call, string tool, object args)
    {
        try
        {
            var response = await call(tool, args);
            Assert.That(response.GetProperty("isError").GetBoolean(), Is.True);
            Assert.That(response.GetRawText(), Does.Not.Contain(Canary).And.Not.Contain("F2_FAKE_SUCCESS"));
        }
        catch (InvalidOperationException error) when (error.Message == "Codex mcpServer/tool/call failed (-32603).") { }
    }

    [Test, Explicit("Requires installed Codex and the built desktop adapter.")]
    public async Task InstalledCodexDoesNotReplayAnAmbiguousDispatchedRequest()
    {
        var token = Token(); var service = Service();
        await using var host = new LocalMcpHost(service, FakeAutomationService.Caller, token, 0);
        await host.StartAsync();
        await using var relay = new WirePeer(0) { Forward = host.BoundEndpoint, ForwardPin = host.CertificateFingerprint, DropToolResponse = true };
        relay.Start();
        await CodexCompatibilityTests.WithCodex(new Uri($"https://127.0.0.1:{relay.Port}/mcp"), true, async (discovery, call) =>
        {
            Assert.That(discovery.GetRawText(), Does.Contain("workbench_get_status"));
            await Reject(call, "workbench_get_status", new StatusV2("2"));
            await Task.Delay(400);
            Assert.That(relay.ToolPosts, Is.EqualTo(1), "One HTTP call, even though its result was lost after dispatch.");
            Assert.That(((BindingV2Tests.Probe)(object)service).Calls, Is.EqualTo(1));
            TestContext.Out.WriteLine("Ambiguous response after real dispatch: toolPOST=1, serviceCalls=1, fakeAccepted=0.");
        }, relay.Identity.Fingerprint, token);
    }

    [Test, Explicit("Requires installed Codex and the built desktop adapter.")]
    public async Task InstalledCodexRejectsNewWorkbenchIdentityEvenWithSameCredential()
    {
        var token = Token(); var service = Service();
        await using var original = new LocalMcpHost(service, FakeAutomationService.Caller, token, 0);
        await original.StartAsync(); var endpoint = original.BoundEndpoint; var originalPin = original.CertificateFingerprint;
        await CodexCompatibilityTests.WithCodex(endpoint, true, async (_, call) =>
        {
            await call("workbench_get_status", new StatusV2("2"));
            await original.DisposeAsync();
            var freshService = Service();
            await using var replacement = new LocalMcpHost(freshService, FakeAutomationService.Caller, token, endpoint.Port);
            await replacement.StartAsync();
            await Reject(call, "workbench_get_status", new StatusV2("2"));
            Assert.That(((BindingV2Tests.Probe)(object)freshService).Calls, Is.Zero);
            Assert.That(replacement.CertificateFingerprint, Is.Not.EqualTo(originalPin));
        }, originalPin, token);
    }

    // Independent raw TCP/TLS counter; counts before authentication and does not know the bearer.
    internal sealed class WirePeer(int port, bool proxy = false, TimeSpan? lifetime = null) : IAsyncDisposable
    {
        internal readonly LocalMcpIdentity Identity = new(lifetime ?? TimeSpan.FromMinutes(5));
        private readonly TcpListener listener = new(IPAddress.Loopback, port);
        private readonly CancellationTokenSource stop = new();
        private readonly List<Task> peers = [];
        private Task? accept;
        internal int Tcp, Tls, Http, Authorization, Bodies;
        internal int ToolPosts;
        internal int WorkbenchConnect, TargetPort;
        internal Uri? Forward;
        internal string? ForwardPin;
        internal bool DropToolResponse;
        internal int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
        internal void Start() { listener.Start(); accept = Accept(); }
        private async Task Accept()
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    var peer = await listener.AcceptTcpClientAsync(stop.Token);
                    Interlocked.Increment(ref Tcp); peers.Add(Serve(peer));
                }
            }
            catch (OperationCanceledException) { }
        }
        private async Task Serve(TcpClient peer)
        {
            using (peer)
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(8));
                try
                {
                    if (proxy)
                    {
                        // If contacted, accept CONNECT and attempt TLS interception with our foreign leaf.
                        using var headers = new StreamReader(peer.GetStream(), Encoding.ASCII, false, 1024, true);
                        var connect = await headers.ReadLineAsync(timeout.Token);
                        // Codex may make unrelated startup network probes. Never proxy those.
                        if (connect != $"CONNECT 127.0.0.1:{TargetPort} HTTP/1.1") return;
                        Interlocked.Increment(ref WorkbenchConnect);
                        while (!string.IsNullOrEmpty(await headers.ReadLineAsync(timeout.Token))) { }
                        await peer.GetStream().WriteAsync("HTTP/1.1 200 Connection established\r\n\r\n"u8.ToArray(), timeout.Token);
                    }
                    using var tls = new SslStream(peer.GetStream());
                    await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = Identity.Certificate }, timeout.Token);
                    Interlocked.Increment(ref Tls);
                    using var reader = new StreamReader(tls, Encoding.UTF8, false, 1024, true);
                    if (await reader.ReadLineAsync(timeout.Token) == null) return;
                    Interlocked.Increment(ref Http);
                    int length = 0;
                    var headersToForward = new List<KeyValuePair<string, string>>();
                    string? header;
                    while (!string.IsNullOrEmpty(header = await reader.ReadLineAsync(timeout.Token)))
                    {
                        if (header.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase)) Interlocked.Increment(ref Authorization);
                        if (header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) length = int.Parse(header.Split(':')[1]);
                        int colon = header.IndexOf(':');
                        if (colon > 0) headersToForward.Add(new(header[..colon], header[(colon + 1)..].Trim()));
                    }
                    if (headersToForward.Any(h => h.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) && h.Value == "chunked"))
                        length = int.Parse((await reader.ReadLineAsync(timeout.Token))!, System.Globalization.NumberStyles.HexNumber);
                    var body = new char[Math.Min(length, 1024 * 1024)];
                    int offset = 0;
                    while (offset < body.Length)
                    {
                        int read = await reader.ReadAsync(body.AsMemory(offset), timeout.Token);
                        if (read == 0) throw new IOException();
                        offset += read;
                    }
                    if (body.Length > 0) Interlocked.Increment(ref Bodies);
                    object id = 1L;
                    bool toolCall = false;
                    using (var json = JsonDocument.Parse(new string(body)))
                    {
                        if (json.RootElement.TryGetProperty("id", out var requestId)) id = requestId.Clone();
                        toolCall = json.RootElement.TryGetProperty("method", out var method) && method.GetString() == "tools/call";
                    }
                    if (toolCall) Interlocked.Increment(ref ToolPosts);
                    byte[] payload;
                    string contentType = "application/json";
                    int status = 200;
                    if (Forward != null)
                    {
                        using var forward = PinnedMcpStdio.CreateHttpClient(Forward, ForwardPin!, new string('A', 64));
                        forward.DefaultRequestHeaders.Authorization = null;
                        using var request = new HttpRequestMessage(HttpMethod.Post, Forward) { Content = new StringContent(new string(body), Encoding.UTF8, "application/json") };
                        foreach (var headerPair in headersToForward)
                            if (!new[] { "Host", "Content-Length", "Content-Type", "Connection", "Transfer-Encoding" }.Contains(headerPair.Key, StringComparer.OrdinalIgnoreCase))
                                request.Headers.TryAddWithoutValidation(headerPair.Key, headerPair.Value);
                        using var response = await forward.SendAsync(request, timeout.Token);
                        payload = await response.Content.ReadAsByteArrayAsync(timeout.Token);
                        if (toolCall && DropToolResponse) return; // Original service ran; destroy only its response.
                        contentType = response.Content.Headers.ContentType?.ToString() ?? contentType;
                        status = (int)response.StatusCode;
                    }
                    else payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result = new { isError = false, content = new[] { new { type = "text", text = "F2_FAKE_SUCCESS" } } } }));
                    await tls.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Response\r\nContent-Type: {contentType}\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n"), timeout.Token);
                    await tls.WriteAsync(payload, timeout.Token);
                }
                catch (Exception error) when (error is IOException or System.Security.Authentication.AuthenticationException or OperationCanceledException or JsonException) { }
            }
        }
        public async ValueTask DisposeAsync()
        {
            stop.Cancel(); listener.Stop();
            if (accept != null) await accept;
            await Task.WhenAll(peers); Identity.Dispose(); await Identity.PublicCleanup; stop.Dispose();
        }
    }
}
