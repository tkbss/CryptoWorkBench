using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CryptoScript.Model;
using CryptoWorkBenchAvalonia.Views;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.Services;

internal sealed class PasswordDialogHost : IDisposable
{
    private readonly Func<Window?> getOwner;
    private readonly CancellationTokenSource lifetime = new();
    internal PasswordDialogHost(Func<Window?>? owner = null) => getOwner = owner ??
        (() => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);

    internal async Task<PasswordResponse> RequestAsync(object sender, PasswordRequestEventArgs request)
    {
        return await UiDispatch.InvokeAsync(() => ShowAsync(request));
    }

    private async Task<PasswordResponse> ShowAsync(PasswordRequestEventArgs request)
    {
        Dispatcher.UIThread.VerifyAccess();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(request.CancellationToken, lifetime.Token);
        linked.Token.ThrowIfCancellationRequested();
        var owner = getOwner() ?? throw new InvalidOperationException("Password dialog owner is unavailable.");
        var dialog = new PasswordDialog(request);
        void OwnerClosed(object? sender, EventArgs args) => linked.Cancel();
        owner.Closed += OwnerClosed;
        using var cancellation = linked.Token.Register(() => UiDispatch.Post(() => dialog.Close()));
        PasswordResponse? response = null;
        try
        {
            // The registration may have posted a close before ShowDialog; recheck first.
            linked.Token.ThrowIfCancellationRequested();
            response = await dialog.ShowDialog<PasswordResponse?>(owner);
            if (linked.IsCancellationRequested)
            {
                response?.Dispose(); response = null;
                request.CancellationToken.ThrowIfCancellationRequested();
                return PasswordResponse.UserCancelled(request.RequestId);
            }
            var result = response ?? PasswordResponse.UserCancelled(request.RequestId);
            response = null;
            return result;
        }
        finally { owner.Closed -= OwnerClosed; response?.Dispose(); }
    }
    public void Dispose() { lifetime.Cancel(); /* token registrations can still be unwinding */ }
}
