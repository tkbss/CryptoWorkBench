using System;
using Avalonia;
using CryptoWorkBenchGuiTest;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows() || args.Length != 2 || args[0] is not ("dock" or "legacy" or "fallback" or "editor" or "editor-input" or "startup-failure" or "tools" or "tools-input" or "output" or "output-input" or "diagnostics" or "diagnostics-input" or "layout" or "layout-restart" or "layout-invalid" or "layout-input" or "integration" or "integration-input" or "integration-restart" or "redock" or "redock-manual" or "layout-roles" or "cleanup-save" or "cleanup-restore" or "operation-contract" or "partial-failure" or "native-acceptance" or "native-acceptance-input")) return 2;
        DockShellNativeProbe.Mode = args[0];
        DockShellNativeProbe.Output = args[1];
        System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(args[1] + ".trace.log"));
        System.Diagnostics.Trace.AutoFlush = true;
        try
        {
            return AppBuilder.Configure(() => new DockShellNativeProbe { UseDockShell = args[0] != "legacy" })
                .UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime([]);
        }
        catch (Exception error) when (args[0] == "startup-failure")
        {
            return DockShellNativeProbe.RecordStartupFailure(error);
        }
    }
}
