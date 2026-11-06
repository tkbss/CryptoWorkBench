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
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure(() => new App { ConfigureDesktopMcp = DesktopMcpSession.Attach })
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

}
