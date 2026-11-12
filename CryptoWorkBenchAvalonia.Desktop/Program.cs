using System;

using Avalonia;

namespace CryptoWorkBenchAvalonia.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--mcp-stdio")
        {
            Environment.ExitCode = CryptoWorkBench.Mcp.PinnedMcpStdio.RunAsync().GetAwaiter().GetResult();
            return;
        }
        // CryptoScript also writes Print and parser diagnostics to inherited console streams.
        // The desktop presents those through the editor and the approved result projection.
        Console.SetOut(System.IO.TextWriter.Null);
        Console.SetError(System.IO.TextWriter.Null);
        int traceIndex = Array.IndexOf(args, "--dock-native-trace");
        if (traceIndex >= 0 && (traceIndex + 1 >= args.Length || Array.IndexOf(args, "--dock-shell") < 0))
            throw new ArgumentException("--dock-native-trace requires an output path and --dock-shell.");
        NativeDockWindowTrace? trace = null;
        try
        {
            BuildAvaloniaApp(Array.IndexOf(args, "--dock-shell") >= 0, shell => trace?.Attach(shell),
                    () => { if (traceIndex >= 0) trace = new NativeDockWindowTrace(args[traceIndex + 1]); })
                .StartWithClassicDesktopLifetime(args);
        }
        finally { trace?.Dispose(); }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => BuildAvaloniaApp(false);

    public static AppBuilder BuildAvaloniaApp(bool useDockShell)
        => BuildAvaloniaApp(useDockShell, null);

    private static AppBuilder BuildAvaloniaApp(bool useDockShell, Action<Shell.DockShellWindow>? observe, Action? initializeDiagnostics = null)
        => AppBuilder.Configure(() => new App { UseDockShell = useDockShell, ConfigureDesktopMcp = DesktopMcpSession.Attach, ObserveDockShell = observe, InitializeDockDiagnostics = initializeDiagnostics })
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

}
