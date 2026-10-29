using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using NUnit.Framework;

namespace CryptoWorkBench.Mcp.Tests;

/// <summary>Opt-in test against an installed Codex executable. No model call, credentials or existing configuration.</summary>
[TestFixture]
public class CodexCompatibilityTests
{
    [Test, Explicit("Requires CRYPTOWORKBENCH_CODEX_PATH pointing to the installed local Codex executable.")]
    public async Task InstalledCodexDiscoversAndCallsStatus()
    {
        var executable = Environment.GetEnvironmentVariable("CRYPTOWORKBENCH_CODEX_PATH");
        Assert.That(executable, Is.Not.Null.And.Not.Empty);
        var directory = Directory.CreateTempSubdirectory("cryptoworkbench-codex-probe-");
        await using var host = await TestListener.StartAsync();
        var start = new ProcessStartInfo(executable!)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = directory.FullName
        };
        // Child-only isolated configuration root; never reads or modifies the user's .codex/config.toml.
        start.Environment["CODEX_HOME"] = directory.FullName;
        foreach (var argument in new[] { "app-server", "--stdio", "-c", $"mcp_servers.probe.url=\"{host.Endpoint}\"", "-c", "analytics.enabled=false" })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var errors = process.StandardError.ReadToEndAsync();
        var sequence = 0;
        async Task<JsonElement> Rpc(string method, object parameters)
        {
            var id = ++sequence;
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { id, method, @params = parameters }));
            await process.StandardInput.FlushAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            while (true)
            {
                var line = await process.StandardOutput.ReadLineAsync(timeout.Token);
                if (line is null) throw new InvalidOperationException("Codex closed its RPC stream.");
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.TryGetProperty("id", out var responseId) && responseId.ValueKind == JsonValueKind.Number && responseId.GetInt32() == id)
                {
                    if (root.TryGetProperty("error", out var error)) throw new InvalidOperationException(error.GetRawText());
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
            Assert.That(discovery.GetRawText(), Does.Contain("workbench_get_status"));
            TestContext.Out.WriteLine("Codex discovered workbench_get_status.");
            var result = await Rpc("mcpServer/tool/call", new { threadId, server = "probe", tool = "workbench_get_status", arguments = new { } });
            Assert.That(result.GetProperty("isError").GetBoolean(), Is.False);
            Assert.That(result.GetProperty("structuredContent").GetProperty("ok").GetBoolean(), Is.True);
            Assert.That(host.Service.StatusCalls, Is.EqualTo(1));
            TestContext.Out.WriteLine("Codex status: " + result.GetProperty("structuredContent").GetRawText());
            TestContext.Out.WriteLine("HTTP protocol headers: " + string.Join(", ", host.ProtocolVersions.Distinct()));
        }
        finally
        {
            process.StandardInput.Close();
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            var stderr = await errors;
            if (process.ExitCode != 0) TestContext.Out.WriteLine("Codex diagnostic: " + stderr[..Math.Min(stderr.Length, 4000)]);
            // Only the freshly-created directory is owned by this test.
            directory.Delete(recursive: true);
        }
    }
}
