using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Settings;

namespace CryptoWorkBenchAvalonia.Desktop;

/// <summary>Explicit, local diagnostic mode of the real desktop entry point. Never mutates docking or HWNDs.</summary>
internal sealed class NativeDockWindowTrace : IDisposable
{
    private readonly DockTraceOutput writer;
    internal DockTraceFailure? Failure => writer.Failure;
    private readonly List<Window> windows = new();
    private readonly Dictionary<Window, long> handles = new();
    private readonly List<IDisposable> subscriptions = new();
    private readonly WinEvent callback;
    private readonly nint hook;
    private readonly DispatcherTimer timer;
    private DockShellLayout? layout;
    private long sequence;
    private bool disposed;
    private readonly bool previousLogging;
    private readonly Action<string>? previousHandler;

    internal NativeDockWindowTrace(string path)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Native docking trace requires Windows.");
        writer = new DockTraceOutput(path);
        callback = OnNativeEvent;
        // Out-of-context, restricted to our PID. Includes native move-loop and object create/destroy events.
        hook = SetWinEventHook(1, 0x800B, 0, callback, (uint)Environment.ProcessId, 0, 0);
        if (hook == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        DockLifecycleTrace.Sink = (reason, subject) => Record(reason, subject);
        previousLogging = DockSettings.EnableDiagnosticsLogging;
        previousHandler = DockSettings.DiagnosticsLogHandler;
        DockSettings.EnableDiagnosticsLogging = true;
        DockSettings.DiagnosticsLogHandler = message => { Record("dock-log:" + message); previousHandler?.Invoke(message); };
        subscriptions.Add(Window.WindowOpenedEvent.AddClassHandler<Window>((window, _) => Track(window)));
        subscriptions.Add(InputElement.PointerPressedEvent.AddClassHandler<Window>((w, e) => Pointer("MouseDown:tunnel", w, e), RoutingStrategies.Tunnel, true));
        subscriptions.Add(InputElement.PointerReleasedEvent.AddClassHandler<Window>((w, e) => Pointer("MouseUp:tunnel", w, e), RoutingStrategies.Tunnel, true));
        subscriptions.Add(InputElement.PointerReleasedEvent.AddClassHandler<Window>((w, e) => Pointer("MouseUp:bubble", w, e), RoutingStrategies.Bubble, true));
        subscriptions.Add(InputElement.PointerMovedEvent.AddClassHandler<Window>((w, e) =>
        {
            if (e.GetCurrentPoint(w).Properties.IsLeftButtonPressed) Pointer("MouseMove:left", w, e);
        }, RoutingStrategies.Tunnel, true));
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) => Record("census");
        timer.Start();
        Record("trace-start");
    }

    internal void Attach(DockShellWindow shell)
    {
        layout = shell.Layout;
        Track(shell);
        var factory = layout.Factory;
        var original = factory.HostWindowLocator![nameof(IDockWindow)];
        factory.HostWindowLocator[nameof(IDockWindow)] = () =>
        {
            var host = original();
            if (host is Window window) Track(window);
            Record("host-constructor-return", host);
            return host;
        };
        factory.WindowAdded += (_, e) => Record("factory-window-added", e);
        factory.WindowRemoved += (_, e) => Record("factory-window-removed", e);
        factory.WindowClosing += (_, e) => Record("factory-window-closing", e);
        factory.WindowClosed += (_, e) => Record("factory-window-closed", e);
        factory.WindowMoveDragBegin += (_, e) => Record("factory-drag-begin", e);
        factory.WindowMoveDrag += (_, e) => Record("factory-drag-move", e);
        factory.WindowMoveDragEnd += (_, e) => Record("factory-drag-end", e);
        factory.DockableRemoved += (_, e) => Record("factory-dockable-removed", e);
        factory.DockableMoved += (_, e) => Record("factory-dockable-moved", e);
        factory.DockableDocked += (_, e) => Record("factory-dockable-docked", e);
        Record("shell-attached", shell);
    }

    private void Track(Window window)
    {
        if (windows.Contains(window)) return;
        windows.Add(window);
        window.Opened += (_, _) => Record("managed-opened", window);
        window.Activated += (_, _) => Record("managed-activated", window);
        window.Deactivated += (_, _) => Record("managed-deactivated", window);
        window.Closing += (_, e) => Record($"managed-closing:reason={e.CloseReason}:cancel={e.Cancel}", window);
        window.Closed += (_, _) => Record("managed-closed", window);
        Record("managed-discovered", window);
    }

    private static int Id(object? value) => value is null ? 0 : RuntimeHelpers.GetHashCode(value);
    private static object Dockable(IDockable item) => new
    {
        identity = Id(item), item.Id, type = item.GetType().FullName, owner = Id(item.Owner),
        ownerId = item.Owner?.Id, factory = Id(item.Factory), item.CanClose, item.CanDrag, item.CanDrop,
        isCollapsable = (item as IDock)?.IsCollapsable,
        active = Id((item as IDock)?.ActiveDockable),
        children = (item as IDock)?.VisibleDockables?.Select(Id).ToArray(),
        window = Id((item as IRootDock)?.Window)
    };
    private static object Model(IDockWindow model) => new
    {
        identity = Id(model), model.Id, owner = Id(model.Owner), host = Id(model.Host), factory = Id(model.Factory),
        layout = Id(model.Layout), panes = model.Layout is null ? [] : DockShellLayout.VisibleTree(model.Layout)
            .Where(d => d is ITool or IDocument).Select(d => new { d.Id, identity = Id(d), group = Id(d.Owner), groupId = d.Owner?.Id }).ToArray()
    };

    private object Native(nint hwnd)
    {
        var title = new StringBuilder(512); var kind = new StringBuilder(256);
        GetWindowText(hwnd, title, title.Capacity); GetClassName(hwnd, kind, kind.Capacity);
        var style = GetWindowLongPtr(hwnd, -16).ToInt64(); var extended = GetWindowLongPtr(hwnd, -20).ToInt64();
        var managed = handles.FirstOrDefault(pair => pair.Value == hwnd.ToInt64()).Key;
        return new
        {
            hwnd = hwnd.ToInt64(), exists = IsWindow(hwnd), nativeClass = kind.ToString(), title = title.ToString(),
            owner = GetWindow(hwnd, 4).ToInt64(), parent = GetParent(hwnd).ToInt64(), visible = IsWindowVisible(hwnd),
            style, extendedStyle = extended, wsChild = (style & 0x40000000) != 0,
            wsExAppWindow = (extended & 0x40000) != 0, wsExToolWindow = (extended & 0x80) != 0,
            wsExNoActivate = (extended & 0x8000000) != 0, managedIdentity = Id(managed), managedType = managed?.GetType().FullName,
            factoryRegistered = managed is IHostWindow host && layout?.Factory.HostWindows.Contains(host) == true
        };
    }

    internal void Record(string reason, object? subject = null)
    {
        if (disposed) return;
        writer.Write(() =>
        {
            foreach (var window in windows)
                if (window.TryGetPlatformHandle() is { } handle) handles[window] = handle.Handle.ToInt64();
            var natives = new List<object>();
            EnumWindows((hwnd, _) =>
            {
                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == Environment.ProcessId) natives.Add(Native(hwnd));
                return true;
            }, 0);
            return JsonSerializer.Serialize(new
            {
                sequence = ++sequence, utc = DateTime.UtcNow, pid = Environment.ProcessId, reason,
                subject = new { identity = Id(subject), type = subject?.GetType().FullName,
                    dockable = subject is IDockable item ? Dockable(item) : null,
                    model = subject is IDockWindow model ? Model(model) : subject is IHostWindow host && host.Window is { } bound ? Model(bound) : null },
                dockTree = layout is null ? null : DockShellLayout.VisibleTree(layout.Root).Select(Dockable).ToArray(),
                nativeWindows = natives,
                managedWindows = windows.Select(w => new
                {
                    identity = Id(w), type = w.GetType().FullName, w.Title, w.IsVisible, w.ShowInTaskbar,
                    hwnd = w.TryGetPlatformHandle()?.Handle.ToInt64(), lastHwnd = handles.GetValueOrDefault(w),
                    nativeAlive = handles.TryGetValue(w, out long h) && IsWindow((nint)h),
                    closed = (w as IHostWindowCloseState)?.IsClosed,
                    registered = w is IHostWindow host && layout?.Factory.HostWindows.Contains(host) == true,
                    model = w is IHostWindow host2 && host2.Window is { } model2 ? Model(model2) : null
                }).ToArray(),
                registeredHosts = layout?.Factory.HostWindows.Select(Id).ToArray(),
                floating = layout?.Root.Windows?.Select(Model).ToArray(),
                currentDockable = layout?.Factory.CurrentDockable?.Id,
                canRelocate = (layout?.Factory as EditorDockFactory)?.Session?.CanRelocate, observerDepth = WorkbenchAutomationService.Shared.EditorCoordinator.ObserverDepth,
                disposedLayout = layout?.IsDisposed
            });
        });
    }

    private void OnNativeEvent(nint eventHook, uint evt, nint hwnd, int objectId, int childId, uint thread, uint time)
    {
        if (hwnd == 0 || objectId != 0 || childId != 0 || disposed) return;
        // Events can arrive after destruction. Keep the HWND even if its properties no longer exist.
        Record($"win-event:0x{evt:X}:hwnd={hwnd.ToInt64()}:eventTime={time}");
    }

    private void Pointer(string phase, Window window, PointerEventArgs args)
    {
        Record($"{phase}:position={args.GetPosition(window)}:handled={args.Handled}:source={args.Source?.GetType().FullName}", window);
        if (phase == "MouseUp:tunnel")
            Dispatcher.UIThread.Post(() => Record("MouseUp:dispatch-complete", window), DispatcherPriority.Background);
    }

    public void Dispose()
    {
        if (disposed) return;
        Record("trace-dispose");
        disposed = true;
        timer.Stop();
        foreach (var subscription in subscriptions) subscription.Dispose();
        if (!UnhookWinEvent(hook)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        DockLifecycleTrace.Sink = null;
        DockSettings.EnableDiagnosticsLogging = previousLogging;
        DockSettings.DiagnosticsLogHandler = previousHandler;
        writer.Dispose();
    }

    private delegate bool EnumCallback(nint hwnd, nint state);
    private delegate void WinEvent(nint hook, uint evt, nint hwnd, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumCallback callback, nint state);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll")] private static extern nint GetParent(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint hwnd, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder text, int count);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWinEventHook(uint min, uint max, nint module, WinEvent callback, uint pid, uint thread, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWinEvent(nint hook);
}
