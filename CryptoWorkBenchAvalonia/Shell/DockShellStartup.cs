using System;
using Avalonia.Controls;
using Prism.Ioc;

namespace CryptoWorkBenchAvalonia.Shell;

internal static class DockShellStartup
{
    internal static void Register(IContainerRegistry registry)
    {
        registry.RegisterSingleton<DockShellState>();
        registry.RegisterSingleton<DockShellLayout>();
        registry.RegisterSingleton<DockShellWindow>();
        registry.RegisterSingleton<DockEditorSession>();
    }

    internal static Window Select(bool preview, Func<Window> createDock, Func<Window> createLegacy,
        Action<Exception> failed)
    {
        if (preview)
        {
            try { return createDock(); }
            catch (Exception exception) { failed(exception); }
        }
        return createLegacy();
    }
}
