using System;

namespace CryptoWorkBenchAvalonia.Shell;

internal enum DockLayoutCommand { Maximize, Restore, Reset }
internal enum DockLayoutOutcome { Unavailable, Completed, Rejected, Recovered, Faulted }
internal sealed record DockLayoutResult(DockLayoutOutcome Outcome)
{
    internal string Message => Outcome switch
    {
        DockLayoutOutcome.Completed => "Layout updated.",
        DockLayoutOutcome.Unavailable => "Layout action unavailable while busy, or no applicable view.",
        DockLayoutOutcome.Rejected => "The layout action could not start. The layout was not changed.",
        DockLayoutOutcome.Recovered => "The layout action failed. The previous layout was restored; floating windows may have reopened.",
        _ => "The layout action failed and layout changes are disabled. Save your work before restarting."
    };
}

public sealed partial class DockShellLayout
{
    private sealed class LayoutMutationException(Exception cause, bool recovered)
        : Exception("A layout command failed after mutation began.", cause)
    {
        internal bool Recovered { get; } = recovered;
    }

    private bool _layoutCommandRunning;
    internal bool LayoutFaulted { get; private set; }
    internal DockLayoutResult? LastLayoutResult { get; private set; }

    // Read-only verification at the failed outer Factory operation's boundary.
    // This does not save host bounds, reinitialize owners, compensate, or retry.
    internal void VerifyFailedDockOperation(Exception original)
    {
        try { ValidateCurrentDockModel(); }
        catch (Exception validation)
        {
            LayoutFaulted = true;
            RecordSecondaryFailure(original, "Dock layout validation", validation);
        }
    }

    internal static void RecordSecondaryFailure(Exception original, string stage, Exception secondary)
    {
        // One entry per stage, never a growing error journal. Even reporting a
        // secondary failure must not replace the exception being propagated.
        try { original.Data[stage] = secondary; }
        catch (Exception) { /* The original exception remains authoritative. */ }
    }

    // The sole UI boundary for main and pane menus, including panes in native hosts.
    // No retries, no editor registration, and no alternative admission path.
    internal DockLayoutResult ExecuteLayout(DockLayoutCommand command, string? paneId = null)
    {
        if (_layoutCommandRunning) return new(DockLayoutOutcome.Unavailable);
        if (LayoutFaulted) return LastLayoutResult = new(DockLayoutOutcome.Faulted);
        _layoutCommandRunning = true;
        try
        {
            Verify();
            var accepted = command switch
            {
                DockLayoutCommand.Maximize => paneId == null ? MaximizeActive() : Maximize(paneId),
                DockLayoutCommand.Restore => Restore(),
                DockLayoutCommand.Reset => Reset(),
                _ => throw new ArgumentOutOfRangeException(nameof(command))
            };
            return LastLayoutResult = new(accepted ? DockLayoutOutcome.Completed : DockLayoutOutcome.Unavailable);
        }
        catch (LayoutMutationException error)
        {
            System.Diagnostics.Trace.WriteLine($"Layout {command}: recovered={error.Recovered}; {error}");
            return LastLayoutResult = new(error.Recovered ? DockLayoutOutcome.Recovered : DockLayoutOutcome.Faulted);
        }
        catch (Exception error) when (DockLayoutStore.Recoverable(error))
        {
            System.Diagnostics.Trace.WriteLine($"Layout {command}: rejected before mutation; {error}");
            return LastLayoutResult = new(DockLayoutOutcome.Rejected);
        }
        finally { _layoutCommandRunning = false; }
    }

    private LayoutMutationException MutationFailed(Exception error, Action compensate, Func<bool> verify)
    {
        try { compensate(); }
        catch (Exception recovery) when (DockLayoutStore.Recoverable(recovery))
        { System.Diagnostics.Trace.WriteLine($"Layout compensation: {recovery}"); }
        bool recovered;
        try { recovered = verify(); }
        catch (Exception validation) when (DockLayoutStore.Recoverable(validation))
        { System.Diagnostics.Trace.WriteLine($"Layout compensation validation: {validation}"); recovered = false; }
        if (!recovered) LayoutFaulted = true;
        return new(error, recovered);
    }
}
