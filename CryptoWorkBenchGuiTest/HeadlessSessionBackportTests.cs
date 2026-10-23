using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Headless;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class HeadlessSessionBackportTests
{
    private static HeadlessUnitTestSession Start() =>
        HeadlessUnitTestSession.StartNew(typeof(AsyncPasswordGuiTests.TestApp));

    [Test]
    public void TesthostLoadsExactlyTheVerifiedSourceBuild()
    {
        var assembly = typeof(HeadlessUnitTestSession).Assembly;
        Assert.That(assembly.FullName, Is.EqualTo(
            "Avalonia.Headless, Version=11.3.20.0, Culture=neutral, PublicKeyToken=c8d484a7012f9a8b"));
        Assert.That(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
            Is.EqualTo("11.3.20-cwb.1+upstream.984c3b833f29806eb159fb7f12a6132e936b75df.pr22222"));
        Assert.That(AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "Avalonia.Headless"), Is.EqualTo(1));
        string path = Path.Combine(AppContext.BaseDirectory, "Avalonia.Headless.dll");
        Assert.That(Path.GetFullPath(assembly.Location), Is.EqualTo(Path.GetFullPath(path)));
        string actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        Assert.That(actualHash, Is.EqualTo(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Avalonia.Headless.Backport.sha256")).Trim()).IgnoreCase);
        using var deps = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "CryptoWorkBenchGuiTest.deps.json")));
        var entries = deps.RootElement.GetProperty("libraries").EnumerateObject()
            .Where(p => p.Name.Contains("Avalonia.Headless", StringComparison.Ordinal)).ToArray();
        Assert.That(entries, Has.Length.EqualTo(1));
        Assert.That(entries[0].Name, Is.EqualTo("Avalonia.Headless.Backport/11.3.20-cwb.1"));
        Assert.That(entries[0].Value.GetProperty("type").GetString(), Is.EqualTo("project"));
        TestContext.Progress.WriteLine($"Verified loaded backport: {assembly.FullName}; SHA256={actualHash}");
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
        // Fault injection only: the backport adds no public API for test hooks.
        // These private-field contracts are pinned to the vendored 11.3.20 source.
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
        ?? throw new AssertionException($"Pinned 11.3.20 field missing or incompatible: {name}");
}
