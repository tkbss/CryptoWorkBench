using Avalonia.Headless;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

/// <summary>
/// One dispatch thread for the test assembly: AvaloniaEdit caches thread-affine
/// KeyBindings statically. Each Dispatch still creates/disposes an isolated app.
/// Product objects, windows and services remain owned and cleaned up by each test.
/// </summary>
[SetUpFixture]
public sealed class GuiTestSession
{
    private static readonly Lazy<HeadlessUnitTestSession> Shared = new(() =>
        HeadlessUnitTestSession.StartNew(typeof(EditorCoordinatorTests.TestApp)));
    internal static HeadlessUnitTestSession Session => Shared.Value;

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        if (Shared.IsValueCreated) await Shared.Value.DisposeAsync();
    }
}
