using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Settings;

namespace CryptoWorkBenchAvalonia.Shell;

/// <summary>A document host title bar whose real pointer input enters Dock's whole-window drag pipeline.</summary>
internal static class DockDocumentWindowChrome
{
    internal static void Attach(HostWindow host, Func<bool> canRelocate)
    {
        // The default Avalonia 12 document host has an OS caption. Moving that caption
        // never calls HostWindow.TryBeginMoveDrag, so Dock cannot offer any drop targets.
        // Preserve Dock's content template and its overlays; replace only the caption.
        var original = host.ContentTemplate ?? throw new InvalidOperationException("Dock host content template is missing.");
        host.ContentTemplate = new FuncDataTemplate<IRootDock>((root, _) =>
        {
            var title = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) };
            title.Bind(TextBlock.TextProperty, new Binding(nameof(host.Title)) { Source = host });
            var grip = new Border { Background = Brushes.Transparent, Child = title };
            AutomationProperties.SetAutomationId(grip, "DockDocumentWindowGrip");
            AutomationProperties.SetName(grip, "Drag CryptoScript window to dock");
            grip.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed || !canRelocate()) return;
                if (e.ClickCount == 2)
                    host.WindowState = host.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                else
                {
                    DockLifecycleTrace.Write("document-caption:drag-begin", host);
                    host.TryBeginMoveDrag(e, WindowDragDockScope.FullWindow);
                }
                e.Handled = true;
            };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            void Button(string label, string name, Action action)
            {
                var button = new Button { Content = label, Width = 40, Height = 32, Padding = new Thickness(0) };
                AutomationProperties.SetName(button, name);
                ToolTip.SetTip(button, name);
                button.Click += (_, _) => action();
                buttons.Children.Add(button);
            }
            Button("—", "Minimize", () => host.WindowState = WindowState.Minimized);
            Button("□", "Maximize or restore", () => host.WindowState = host.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized);
            Button("×", "Close", host.Close);
            DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Right);
            var caption = new DockPanel { Height = 32, Children = { buttons, grip } };
            DockPanel.SetDock(caption, global::Avalonia.Controls.Dock.Top);
            var panel = new DockPanel { Children = { caption } };
            if (original.Build(root) is { } content) panel.Children.Add(content);
            return panel;
        });
        host.WindowDecorations = WindowDecorations.BorderOnly;
    }
}
