using System;
using System.Threading;
using Avalonia;
using Avalonia.Threading;
using CryptoWorkBenchGuiTest;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows()) return 2;
        AppBuilder.Configure<EditorCoordinatorTests.TestApp>().UsePlatformDetect().SetupWithoutStarting();
        using var done = new CancellationTokenSource();
        int exit = 1;
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await LeaseV2Tests.NativeTransitionsAsync(Array.IndexOf(args, "--manual-expiry") >= 0);
                Console.WriteLine("PASS native window / ownership / release / shutdown");
                exit = 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex.GetType().Name); }
            finally { done.Cancel(); }
        });
        Dispatcher.UIThread.MainLoop(done.Token);
        return exit;
    }
}
