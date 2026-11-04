using System;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBench.Mcp;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Views;
using CryptoWorkBenchAvalonia.Services;

namespace CryptoWorkBenchAvalonia.Desktop;

internal sealed class DesktopMcpSession(MainWindow owner, WorkbenchAutomationService service)
{
    private LocalMcpHost? host;
    private Task starting = Task.CompletedTask;
    private Task? stopping;
    private McpDisconnect? disconnect;
    private Window? dialog;

    internal static Func<Task> Attach(MainWindow owner, WorkbenchAutomationService service)
    {
        var session = new DesktopMcpSession(owner, service);
        var button = owner.McpSessionButton;
        // B3's production lease clock currently certifies Windows only.
        button.IsVisible = OperatingSystem.IsWindows();
        button.Click += async (_, _) =>
        {
            button.IsEnabled = false;
            try
            {
                if (session.host != null)
                {
                    await session.StopAsync();
                    return;
                }
                if (!await session.ShowAsync("Allow local Codex access?",
                    "Allow the paired client to read and edit this editor, inspect variable metadata, read approved execution output and request script execution. Each execution requires your approval.",
                    "Allow access", null) || service.IsStopping) return;
                string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                service.ActivateMcp();
                var caller = service.IssueCredential("local-codex", ImmutableHashSet.Create(
                    AutomationPermissions.StatusRead, AutomationPermissions.EditorRead, AutomationPermissions.EditorControl,
                    AutomationPermissions.EditorWrite, AutomationPermissions.ExecutionRead, AutomationPermissions.ExecutionRun,
                    AutomationPermissions.OutputRead, AutomationPermissions.VariablesMetadataRead));
                session.host = new LocalMcpHost(service, caller, token);
                session.host.Expired += () => UiDispatch.Post(async () =>
                {
                    try { await session.StopAsync(); } catch { }
                });
                session.starting = session.host.StartAsync();
                await session.starting;
                if (service.IsStopping) return;
                button.Content = "Disconnect Codex";
                ToolTip.SetTip(button, $"{LocalMcpHost.Endpoint}\nInstance {service.InstanceId}");
                await session.ShowAsync("Pair local Codex", $"Endpoint {LocalMcpHost.Endpoint}\nInstance {service.InstanceId}\n" +
                    $"Use this Workbench executable with --mcp-stdio as the MCP command.\n{Environment.ProcessPath}\n" +
                    $"Before starting Codex, set CRYPTOWORKBENCH_MCP_CERT_SHA256 to\n{session.host.CertificateFingerprint}\n" +
                    $"Session expires {session.host.ExpiresAt.ToLocalTime():g}.\n" +
                    "Set CRYPTOWORKBENCH_MCP_TOKEN to the token below. Forward both variables through the Codex MCP env_vars setting. Use the pinned STDIO connection; direct HTTPS CA configuration does not bind this instance exclusively. A new Workbench requires new pairing. Disconnect Codex to revoke access.", "Done", token);
            }
            catch
            {
                // Fixed diagnostic. Configuration, headers and script data never enter a log.
                try
                {
                    await session.StopAsync();
                    button.Content = "Codex unavailable — GUI released";
                    ToolTip.SetTip(button, "Local session failed. Check port 51837 and restart the workbench.");
                }
                catch { } // StopAsync already displays the safe drain fault.
            }
            finally { button.IsEnabled = session.stopping == null && !service.IsStopping; }
        };
        // Shell exit has its own DrainForExitAsync policy, including quiescent native faults.
        return session.StopTransportAsync;
    }

    private async Task<bool> ShowAsync(string title, string message, string accept, string? token)
    {
        var window = new Window { Title = title, Width = 570, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(16), Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        if (token != null) panel.Children.Add(new TextBox { Text = token, IsReadOnly = true });
        else
        {
            var cancel = new Button { Content = "Cancel" };
            cancel.Click += (_, _) => window.Close(false);
            panel.Children.Add(cancel);
        }
        var ok = new Button { Content = accept };
        ok.Click += (_, _) => window.Close(true);
        panel.Children.Add(ok);
        window.Content = panel;
        dialog = window;
        try { return await window.ShowDialog<bool>(owner); }
        finally { dialog = null; }
    }

    private Task StopAsync()
    {
        disconnect ??= new McpDisconnect(() => host?.Revoke(), service.DisableMcpAndDrainAsync, StopTransportAsync, state =>
        {
            owner.McpSessionButton.IsEnabled = false;
            owner.McpSessionButton.Content = state switch
            {
                McpDisconnectState.Revoking => "Revoking Codex access…",
                McpDisconnectState.WaitingForDrain => "Access revoked — waiting for editor…",
                McpDisconnectState.Disconnected => "Disconnected — GUI released",
                _ => "Access revoked — editor recovery failed"
            };
            ToolTip.SetTip(owner.McpSessionButton, state == McpDisconnectState.SafeFault
                ? "The editor remains protected. Close the workbench after pending work finishes."
                : "Access is revoked. Pending work must finish before the editor is released. Pair again in a new workbench process.");
        });
        return disconnect.RunAsync();
    }

    private Task StopTransportAsync() => stopping ??= StopTransportCoreAsync();

    private async Task StopTransportCoreAsync()
    {
        host?.Revoke();
        service.DisableMcp();
        dialog?.Close(false);
        await starting.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (host != null) await host.DisposeAsync();
    }
}
