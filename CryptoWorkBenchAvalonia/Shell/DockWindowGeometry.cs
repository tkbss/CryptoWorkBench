using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;

namespace CryptoWorkBenchAvalonia.Shell;

internal readonly record struct LayoutScreen(PixelRect WorkingArea, double Scaling);
internal static class DockWindowGeometry
{
    internal static LayoutScreen[] Screens(Window window) => window.Screens.All
        .Select(s => new LayoutScreen(s.WorkingArea, s.Scaling)).ToArray();

    internal static LayoutWindow Clamp(LayoutWindow saved, IReadOnlyList<LayoutScreen> screens)
    {
        // Window position is native pixels; Avalonia client dimensions are device-independent pixels.
        if (screens.Count == 0) return new(0, 0, 800, 600, saved.Maximized);
        var x = double.IsFinite(saved.X) && Math.Abs(saved.X) < int.MaxValue ? saved.X : 0;
        var y = double.IsFinite(saved.Y) && Math.Abs(saved.Y) < int.MaxValue ? saved.Y : 0;
        var screen = screens.FirstOrDefault(s => s.WorkingArea.Contains(new PixelPoint((int)x, (int)y)));
        if (screen.WorkingArea.Width <= 0) screen = screens[0];
        var area = screen.WorkingArea;
        var scale = double.IsFinite(screen.Scaling) && screen.Scaling > 0 ? screen.Scaling : 1;
        var width = Math.Clamp(double.IsFinite(saved.Width) && saved.Width > 0 ? saved.Width : 800,
            Math.Min(240, area.Width / scale), Math.Max(1, area.Width / scale));
        var height = Math.Clamp(double.IsFinite(saved.Height) && saved.Height > 0 ? saved.Height : 600,
            Math.Min(160, Math.Max(1, (area.Height - 40) / scale)), Math.Max(1, (area.Height - 40) / scale));
        x = Math.Clamp(x, area.X, Math.Max(area.X, area.Right - width * scale));
        y = Math.Clamp(y, area.Y, Math.Max(area.Y, area.Bottom - height * scale - 40));
        return new(x, y, width, height, saved.Maximized);
    }
}
