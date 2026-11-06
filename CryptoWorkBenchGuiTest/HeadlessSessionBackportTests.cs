using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Headless;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class HeadlessSessionTests
{
    private static HeadlessUnitTestSession Start() =>
        HeadlessUnitTestSession.StartNew(typeof(AsyncPasswordGuiTests.TestApp));

    [Test]
    public void TesthostLoadsExactlyTheOfficialAvalonia12Package()
    {
        var assembly = typeof(HeadlessUnitTestSession).Assembly;
        Assert.That(assembly.FullName, Is.EqualTo(
            "Avalonia.Headless, Version=12.1.4.0, Culture=neutral, PublicKeyToken=c8d484a7012f9a8b"));
        Assert.That(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
            Is.EqualTo("12.1.4+2d79ba55fae1b1e71c69f93a668080c9029e6a31"));
        Assert.That(AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "Avalonia.Headless"), Is.EqualTo(1));
        string path = Path.Combine(AppContext.BaseDirectory, "Avalonia.Headless.dll");
        Assert.That(Path.GetFullPath(assembly.Location), Is.EqualTo(Path.GetFullPath(path)));
        string actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        // SHA-256 of the official 12.1.4 package lib/net10.0 asset; independent of build output.
        Assert.That(actualHash, Is.EqualTo("29CBFA10E0A4631AE9C762EB718D72A82419656419FB513A9D77F1741E8F70DE"));
        using var deps = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "CryptoWorkBenchGuiTest.deps.json")));
        var entries = deps.RootElement.GetProperty("libraries").EnumerateObject()
            .Where(p => p.Name.Contains("Avalonia.Headless", StringComparison.Ordinal)).ToArray();
        Assert.That(entries, Has.Length.EqualTo(1));
        Assert.That(entries[0].Name, Is.EqualTo("Avalonia.Headless/12.1.4"));
        Assert.That(entries[0].Value.GetProperty("type").GetString(), Is.EqualTo("package"));
        Assert.That(entries[0].Value.GetProperty("sha512").GetString(), Is.EqualTo(
            "sha512-XsVJaOc2OXDAMJ66eSnclm3YvfuVdiPay0zJUP0+fqoGoFv4XgPxvM2pMbwwt5VRVDyTSHHDpT0YGdg61ZCk/g=="));
        string runtimeTarget = deps.RootElement.GetProperty("runtimeTarget").GetProperty("name").GetString()!;
        var runtimeEntries = deps.RootElement.GetProperty("targets").GetProperty(runtimeTarget).EnumerateObject()
            .Where(p => p.Name.Contains("Avalonia.Headless", StringComparison.Ordinal)).ToArray();
        Assert.That(runtimeEntries, Has.Length.EqualTo(1));
        Assert.That(runtimeEntries[0].Name, Is.EqualTo("Avalonia.Headless/12.1.4"));
        var assets = runtimeEntries[0].Value.GetProperty("runtime").EnumerateObject().ToArray();
        Assert.That(assets, Has.Length.EqualTo(1));
        Assert.That(assets[0].Name, Is.EqualTo("lib/net10.0/Avalonia.Headless.dll"));
        Assert.That(assets[0].Value.GetProperty("assemblyVersion").GetString(), Is.EqualTo("12.1.4.0"));
        Assert.That(assets[0].Value.GetProperty("fileVersion").GetString(), Is.EqualTo("12.1.4.0"));
        TestContext.Progress.WriteLine($"Verified official package: {assembly.FullName}; SHA256={actualHash}");
    }

    [Test]
    public void TenThousandImmediateSessionLifetimesCompleteWithoutWorkerFaults()
    {
        for (int iteration = 0; iteration < 10000; iteration++)
        {
            // Deliberately no Dispatch, sleep or retry. Dispose must join the worker,
            // and any worker exception must fail this test through Task.Wait.
            using var session = Start();
        }
    }

    [Test]
    public void DisposePropagatesAWorkerFaultDuringShutdown()
    {
        // Fault injection only: no additional public API is required.
        // These private-field contracts are checked against the pinned official 12.1.4 assembly.
        var session = Start();
        var queue = Field<BlockingCollection<(Action, ExecutionContext?)>>(session, "_queue");
        var cancellation = Field<CancellationTokenSource>(session, "_cancellationTokenSource");
        var worker = Field<Task>(session, "_dispatchTask");
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var injected = new InvalidOperationException("F01-118-B injected worker fault");
        queue.Add((() =>
        {
            entered.Set();
            release.Wait();
            throw injected;
        }, null));
        Task? shutdown = null;
        try
        {
            Assert.That(entered.Wait(TimeSpan.FromSeconds(10)), Is.True, "Worker did not enter the injected action.");
            shutdown = Task.Run(session.Dispose);
            Assert.That(cancellation.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)), Is.True,
                "Dispose must request cancellation before joining the worker.");
            release.Set();
            var error = Assert.ThrowsAsync<AggregateException>(async () =>
                await shutdown.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.That(error!.InnerExceptions, Has.Count.EqualTo(1));
            Assert.That(error.InnerException, Is.SameAs(injected));
            Assert.That(worker.IsFaulted, Is.True);
        }
        finally
        {
            release.Set();
            // Observe completion before disposing the injected synchronization state.
            // The expected exception is asserted above; unrelated errors still propagate.
            if (shutdown is null) shutdown = Task.Run(session.Dispose);
            using var finished = ((IAsyncResult)shutdown).AsyncWaitHandle;
            Assert.That(finished.WaitOne(TimeSpan.FromSeconds(10)), Is.True,
                "Shutdown did not finish after releasing the worker.");
            queue.Dispose();
            cancellation.Dispose();
        }
    }

    private static T Field<T>(HeadlessUnitTestSession session, string name) where T : class =>
        typeof(HeadlessUnitTestSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(session) as T
        ?? throw new AssertionException($"Pinned 12.1.4 field missing or incompatible: {name}");
}
