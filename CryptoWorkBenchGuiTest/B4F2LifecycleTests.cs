using System.Reflection;
using System.Security.Cryptography;
using CryptoWorkBenchAvalonia;
using CryptoWorkBenchAvalonia.Services.Automation;
using NUnit.Framework;
using static CryptoWorkBenchGuiTest.B1EditorIntegrationTests;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class B4F2LifecycleTests
{
    [Test]
    public Task RealDesktopStopWithCaReadLockStillWaitsForOriginalDrainAndCloses() => Ui(async () =>
    {
        if (!OperatingSystem.IsWindows()) Assert.Ignore("Windows sharing/native desktop path.");
        await using var h = new Host("F2 synthetic editor");
        h.Service.ActivateMcp();
        var caller = h.Service.IssueCredential("f2", WorkbenchAutomationService.LocalCaller.Permissions);
        // Load the built production desktop without introducing a GUI-test -> executable project cycle.
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        var desktopDirectory = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "../../../../CryptoWorkBenchAvalonia.Desktop/bin", configuration, "net10.0"));
        var mcp = Assembly.LoadFrom(Path.Combine(desktopDirectory, "CryptoWorkBench.Mcp.dll"));
        var desktop = Assembly.LoadFrom(Path.Combine(desktopDirectory, "CryptoWorkBenchAvalonia.Desktop.dll"));
        var type = mcp.GetType("CryptoWorkBench.Mcp.LocalMcpHost")!;
        var host = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object?[] { h.Service, caller, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), 0, null }, null)!;
        await (Task)type.GetMethod("StartAsync")!.Invoke(host, null)!;
        string ca = (string)type.GetProperty("TrustBundlePath")!.GetValue(host)!;
        var sessionType = desktop.GetType("CryptoWorkBenchAvalonia.Desktop.DesktopMcpSession")!;
        // StopTransportCoreAsync never uses owner; UI state transitions are tested separately.
        var session = Activator.CreateInstance(sessionType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new object?[] { null, h.Service }, null)!;
        sessionType.GetField("host", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, host);
        Task Stop() => (Task)sessionType.GetMethod("StopTransportAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, null)!;
        using var reader = new FileStream(ca, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var held = h.Owner.TryBegin()!;
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Window.Closed += (_, _) => closed.TrySetResult();
        App.CoordinateClosing(h.Window, h.Service, Stop);
        h.Window.Close();
        await Task.Delay(450);
        Assert.That(h.Window.IsVisible, Is.True); Assert.That(h.Owner.CanStartGui, Is.False);
        var transportCompletion = Stop();
        Assert.That(transportCompletion.IsCompletedSuccessfully, Is.True, "Public cleanup cannot poison the cached real desktop stop task.");
        Assert.That(Stop(), Is.SameAs(transportCompletion));
        Assert.That((await h.Service.GetStatusAsync(caller, default)).Error, Is.Not.Null);
        reader.Dispose(); held.Dispose();
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(h.Window.IsVisible, Is.False);
        File.Delete(ca); Directory.Delete(Path.GetDirectoryName(ca)!);
    });
}
