using System.Diagnostics;
using System.Text.Json;
using CryptoWorkBench.Automation.Abstractions;
using NUnit.Framework;

namespace CryptoWorkBench.Mcp.Tests;

/// <summary>Installed-client probes with isolated configuration and no model calls.</summary>
[TestFixture]
public class CodexCompatibilityTests
{
    [Test, Explicit("Requires CRYPTOWORKBENCH_CODEX_PATH pointing to the installed local Codex executable.")]
    public async Task InstalledCodexDiscoversAndCallsStatus()
    {
        await using var host = await TestListener.StartAsync();
        await WithCodex(host.Endpoint, paired: false, async (discovery, call) =>
        {
            Assert.That(discovery.GetRawText(), Does.Contain("workbench_get_status"));
            var result = await call("workbench_get_status", new { });
            Assert.That(result.GetProperty("isError").GetBoolean(), Is.False);
            Assert.That(result.GetProperty("structuredContent").GetProperty("ok").GetBoolean(), Is.True);
            Assert.That(host.Service.StatusCalls, Is.EqualTo(1));
            TestContext.Out.WriteLine("HTTP protocol headers: " + string.Join(", ", host.ProtocolVersions.Distinct()));
        });
    }

    [Test, Explicit("Requires a paired disposable desktop editor with the exact B4 AES seed and local execution approval.")]
    public Task InstalledCodexCompletesDesktopAes() => WithCodex(new Uri(LocalMcpHost.Endpoint), paired: true, VerifyDesktopAes);

    [Test, Explicit("Requires a paired disposable desktop, local execution approval, and subsequent GUI disconnect.")]
    public Task InstalledCodexCompletesDesktopAesAndRejectsTakeover() => WithCodex(new Uri(LocalMcpHost.Endpoint), true, async (discovery, call) =>
    {
        await VerifyDesktopAes(discovery, call);
        var readyFile = Environment.GetEnvironmentVariable("CRYPTOWORKBENCH_F1_READY_FILE");
        Assert.That(readyFile, Is.Not.Null.And.Not.Empty);
        await File.WriteAllTextAsync(readyFile!, "AES passed; lease released; same Codex is waiting for GUI disconnect.");
        var endpoint = new Uri(LocalMcpHost.Endpoint);
        // Human pairing, native file dialogs and accessibility recovery can exceed five minutes.
        // This bounds only the manual disconnect phase, never a tool invocation or lease.
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(30));
        while (true)
        {
            using var socket = new System.Net.Sockets.TcpClient();
            try { await socket.ConnectAsync(System.Net.IPAddress.Loopback, endpoint.Port, timeout.Token); }
            catch (System.Net.Sockets.SocketException) { break; }
            await Task.Delay(250, timeout.Token);
        }
        await using var replacement = new B4F1SecurityTests.Replacement(endpoint.Port);
        await replacement.StartAsync();
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var result = await call("workbench_update_editor", new EditorEditV2("2", Guid.NewGuid(), Guid.NewGuid(),
                    Guid.NewGuid(), "1", EditorEditMode.Append, "F1_NATIVE_PRIVATE_CANARY"));
                Assert.That(result.GetProperty("isError").GetBoolean(), Is.True);
                Assert.That(result.GetRawText(), Does.Not.Contain("F1_FAKE_SUCCESS").And.Not.Contain("F1_NATIVE_PRIVATE_CANARY"));
            }
            catch (InvalidOperationException error) when (error.Message == "Codex mcpServer/tool/call failed (-32603).") { }
        }
        Assert.That(replacement.HttpRequests, Is.Zero);
        TestContext.Out.WriteLine("Native desktop disconnected; same running Codex rejected two edits; replacement decrypted HTTP requests = 0.");
    });

    private static async Task VerifyDesktopAes(JsonElement discovery, Func<string, object, Task<JsonElement>> call)
    {
        const string seed = "KEY k=GenerateKey(AES-CBC,0x(2b7e151628aed2a6abf7158809cf4f3c))";
        const string tail = "\nPARAM p=Parameters(#MECH:AES-CBC,#IV:0x(000102030405060708090a0b0c0d0e0f),#PAD:NONE)\n" +
            "VAR pt=0x(6bc1bee22e409f96e93d7e117393172a)\nVAR ct=Encrypt(p,k,pt)\nPrint(ct)";
        async Task<JsonElement> Tool(string suffix, object args)
        {
            var wire = await call("workbench_" + suffix, args);
            return wire.GetProperty("structuredContent");
        }
        var server = discovery.GetProperty("data").EnumerateArray().Single(s => s.GetProperty("name").GetString() == "probe");
        var names = server.GetProperty("tools").EnumerateObject().Select(t => t.Value.GetProperty("name").GetString()).ToArray();
        Assert.That(names, Is.EquivalentTo(new[] { "workbench_get_status", "workbench_get_editor", "workbench_update_editor",
            "workbench_acquire_editor", "workbench_renew_editor", "workbench_release_editor", "workbench_execute_editor",
            "workbench_get_execution", "workbench_get_diagnostics", "workbench_cancel_execution", "workbench_get_variables" }));
        var getStatus = new StatusV2("2");
        var status = await Tool("get_status", getStatus);
        var instance = status.GetProperty("instanceId").GetGuid();
        var editor = status.GetProperty("result").GetProperty("editorId").GetGuid();
        var read = new ReadEditorV2("2", instance, editor);
        var before = (await Tool("get_editor", read)).GetProperty("result");
        // Refuse to touch unrelated content. Never print an unexpected editor snapshot.
        Assert.That(before.GetProperty("text").GetString() == seed, Is.True, "A disposable editor with the B4 seed is required.");
        var revision = before.GetProperty("revision").GetString()!;
        var acquire = new AcquireEditorV2("2", instance, editor, Guid.NewGuid(), revision);
        var receipt = await Tool("acquire_editor", acquire);
        var lease = receipt.GetProperty("result").GetProperty("leaseId").GetGuid();
        try
        {
            var second = await Tool("acquire_editor", acquire with { AcquisitionId = Guid.NewGuid() });
            Assert.That(second.GetProperty("error").GetProperty("code").GetString(), Is.EqualTo("editorOwned").IgnoreCase);
            var mutation = new EditorEditV2("2", instance, editor, lease, revision, EditorEditMode.Append, tail);
            var edit = await Tool("update_editor", mutation);
            Assert.That(edit.GetProperty("textEffect").GetString(), Is.EqualTo("applied"));
            Assert.That(edit.GetProperty("historyEffect").GetString(), Is.EqualTo("applied"));
            Assert.That(edit.GetProperty("finalization").GetString(), Is.EqualTo("complete"));
            var stale = await Tool("update_editor", mutation with { Text = "stale" });
            Assert.That(stale.GetProperty("error").GetProperty("code").GetString(), Is.EqualTo("revisionConflict").IgnoreCase);
            var current = (await Tool("get_status", getStatus)).GetProperty("result");
            var execute = new EditorExecutionV2("2", instance, editor, lease, Guid.NewGuid(),
                edit.GetProperty("observedRevision").GetString()!, current.GetProperty("stateVersion").GetString()!,
                120000, OutputPolicy.IncludeText);
            var result = (await Tool("execute_editor", execute)).GetProperty("result");
            Assert.That(result.GetProperty("state").GetString(), Is.EqualTo("completed"));
            Assert.That(result.GetProperty("hasErrors").GetBoolean(), Is.False);
            Assert.That(result.GetProperty("outputs")[0].GetProperty("text").GetString(),
                Is.EqualTo("out: 0x(7649abac8119b246cee98e9b12e9197d)").IgnoreCase);
            var job = new JobRequestV2("2", instance, execute.ExecutionId);
            Assert.That((await Tool("get_diagnostics", job)).GetProperty("result").GetProperty("isFinal").GetBoolean(), Is.True);
            TestContext.Out.WriteLine($"AES-CBC matched, editor {editor}, revision {revision} to {execute.ExpectedRevision}, job {execute.ExecutionId}");
        }
        finally
        {
            var release = await Tool("release_editor", new ReleaseEditorV2("2", instance, editor, lease));
            Assert.That(release.GetProperty("error").ValueKind, Is.EqualTo(JsonValueKind.Null));
        }
        var after = (await Tool("get_editor", read)).GetProperty("result");
        Assert.That(after.GetProperty("text").GetString()?.ReplaceLineEndings("\n") == seed + tail, Is.True);
        var releasedWrite = new EditorEditV2("2", instance, editor, lease, after.GetProperty("revision").GetString()!,
            EditorEditMode.Append, "retired");
        var rejected = await Tool("update_editor", releasedWrite);
        Assert.That(rejected.GetProperty("error").GetProperty("code").GetString(), Is.EqualTo("leaseRevoked").IgnoreCase);
        Assert.That(rejected.GetProperty("textEffect").GetString(), Is.EqualTo("none"));
        Assert.That(rejected.GetProperty("historyEffect").GetString(), Is.EqualTo("none"));
        Assert.That((await Tool("get_editor", read)).GetProperty("result").GetProperty("revision").GetString(),
            Is.EqualTo(releasedWrite.ExpectedRevision));
        Assert.That((await Tool("get_status", getStatus)).GetProperty("result").GetProperty("phase").GetString(), Is.EqualTo("gui"));
    }

    internal static async Task WithCodex(Uri endpoint, bool paired,
        Func<JsonElement, Func<string, object, Task<JsonElement>>, Task> verify,
        string? fingerprint = null, string? token = null, IReadOnlyDictionary<string, string>? extraEnvironment = null)
    {
        var executable = Environment.GetEnvironmentVariable("CRYPTOWORKBENCH_CODEX_PATH");
        Assert.That(executable, Is.Not.Null.And.Not.Empty);
        var directory = Directory.CreateTempSubdirectory("cryptoworkbench-codex-probe-");
        var start = new ProcessStartInfo(executable!)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = directory.FullName
        };
        // Child-only isolated configuration root; never reads or modifies the user's .codex/config.toml.
        start.Environment["CODEX_HOME"] = directory.FullName;
        foreach (var argument in new[] { "app-server", "--stdio", "-c", "analytics.enabled=false" })
            start.ArgumentList.Add(argument);
        if (paired)
        {
            if (token != null) start.Environment["CRYPTOWORKBENCH_MCP_TOKEN"] = token;
            if (fingerprint != null) start.Environment["CRYPTOWORKBENCH_MCP_CERT_SHA256"] = fingerprint;
            Assert.That(start.Environment.ContainsKey("CRYPTOWORKBENCH_MCP_TOKEN"), Is.True);
            Assert.That(start.Environment.ContainsKey("CRYPTOWORKBENCH_MCP_CERT_SHA256"), Is.True);
            var adapter = Environment.GetEnvironmentVariable("CRYPTOWORKBENCH_MCP_ADAPTER");
            Assert.That(adapter, Is.Not.Null.And.Not.Empty);
            start.Environment["CRYPTOWORKBENCH_MCP_ENDPOINT"] = endpoint.ToString();
            foreach (var config in new[] {
                $"mcp_servers.probe.command={JsonSerializer.Serialize(adapter)}",
                "mcp_servers.probe.args=[\"--mcp-stdio\"]",
                "mcp_servers.probe.env_vars=[\"CRYPTOWORKBENCH_MCP_TOKEN\",\"CRYPTOWORKBENCH_MCP_CERT_SHA256\",\"CRYPTOWORKBENCH_MCP_ENDPOINT\",\"SSL_CERT_FILE\",\"CODEX_CA_CERTIFICATE\",\"HTTPS_PROXY\",\"ALL_PROXY\",\"NO_PROXY\"]",
                "mcp_servers.probe.tool_timeout_sec=150" })
            { start.ArgumentList.Add("-c"); start.ArgumentList.Add(config); }
        }
        else
        {
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add($"mcp_servers.probe.url=\"{endpoint}\"");
        }
        if (extraEnvironment != null)
            foreach (var pair in extraEnvironment) start.Environment[pair.Key] = pair.Value;
        using var process = Process.Start(start)!;
        var errors = process.StandardError.ReadToEndAsync();
        var sequence = 0;
        async Task<JsonElement> Rpc(string method, object parameters)
        {
            var id = ++sequence;
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { id, method, @params = parameters }));
            await process.StandardInput.FlushAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(paired ? 160 : 45));
            while (true)
            {
                var line = await process.StandardOutput.ReadLineAsync(timeout.Token);
                if (line is null) throw new InvalidOperationException("Codex closed its RPC stream.");
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.TryGetProperty("id", out var responseId) && responseId.ValueKind == JsonValueKind.Number && responseId.GetInt32() == id)
                {
                    if (root.TryGetProperty("error", out var error)) throw new InvalidOperationException($"Codex {method} failed ({error.GetProperty("code")}).");
                    return root.GetProperty("result").Clone();
                }
            }
        }
        try
        {
            var initialize = await Rpc("initialize", new { clientInfo = new { name = "cryptoworkbench-probe", version = "1" }, capabilities = new { experimentalApi = true } });
            TestContext.Out.WriteLine("Codex initialize: " + initialize.GetRawText());
            await process.StandardInput.WriteLineAsync("{\"method\":\"initialized\"}");
            var thread = await Rpc("thread/start", new { cwd = directory.FullName, ephemeral = true });
            var threadId = thread.GetProperty("thread").GetProperty("id").GetString()!;
            var discovery = await Rpc("mcpServerStatus/list", new { threadId, serverName = "probe", detail = "toolsAndAuthOnly" });
            await verify(discovery, (tool, args) => Rpc("mcpServer/tool/call", new { threadId, server = "probe", tool,
                arguments = JsonSerializer.SerializeToElement(args, AutomationJson.Options) }));
        }
        finally
        {
            process.StandardInput.Close();
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (TimeoutException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            await errors;
            // Child working-directory handles can briefly survive process exit on Windows.
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    // Only this newly-created test profile is owned by the probe.
                    foreach (var file in directory.EnumerateFiles("*", SearchOption.AllDirectories))
                        file.Attributes &= ~FileAttributes.ReadOnly;
                    directory.Delete(recursive: true);
                    break;
                }
                catch (IOException) when (OperatingSystem.IsWindows() && attempt < 10)
                { await Task.Delay(100 * (attempt + 1)); }
            }
        }
    }
}
