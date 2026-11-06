using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using CryptoScript.Model;
using System;

namespace CryptoWorkBenchAvalonia.Views;

// No password binding/ViewModel. TextBox strings are cleared on every close path;
// Avalonia/GC copies cannot be guaranteed erased.
internal sealed class PasswordDialog : Window
{
    private readonly PasswordRequestEventArgs request;
    internal TextBox PasswordInput { get; } = new() { PasswordChar = '●', Name = "PasswordInput" };
    internal TextBox ConfirmationInput { get; } = new() { PasswordChar = '●', Name = "ConfirmationInput" };
    internal TextBlock Validation { get; } = new();

    internal PasswordDialog(PasswordRequestEventArgs request)
    {
        this.request = request;
        Title = request.ConfirmationRequired ? "SecureSave password" : "SecureLoad password";
        Width = 380; SizeToContent = SizeToContent.Height; CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = "Password" });
        panel.Children.Add(PasswordInput);
        if (request.ConfirmationRequired)
        {
            panel.Children.Add(new TextBlock { Text = "Confirm password" });
            panel.Children.Add(ConfirmationInput);
        }
        panel.Children.Add(Validation);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var confirm = new Button { Content = "Confirm", IsDefault = true };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        confirm.Click += (_, _) => Confirm();
        cancel.Click += (_, _) => Close();
        buttons.Children.Add(confirm); buttons.Children.Add(cancel); panel.Children.Add(buttons);
        Content = panel;
        Closed += (_, _) => { PasswordInput.Text = string.Empty; ConfirmationInput.Text = string.Empty; };
        Opened += (_, _) => PasswordInput.Focus();
    }

    internal void Confirm()
    {
        if (string.IsNullOrEmpty(PasswordInput.Text)) { Validation.Text = "Enter a password."; return; }
        if (request.ConfirmationRequired && !string.Equals(PasswordInput.Text, ConfirmationInput.Text, StringComparison.Ordinal))
        { Validation.Text = "Passwords do not match."; return; }
        PasswordSecret? password = null, confirmation = null;
        PasswordResponse? response = null;
        try
        {
            password = PasswordSecret.TakeOwnership(PasswordInput.Text.ToCharArray());
            if (request.ConfirmationRequired) confirmation = PasswordSecret.TakeOwnership(ConfirmationInput.Text!.ToCharArray());
            response = PasswordResponse.Supplied(request.RequestId, password, confirmation);
            Close(response);
            response = null; password = confirmation = null;
        }
        finally { response?.Dispose(); password?.Dispose(); confirmation?.Dispose(); }
    }
}
