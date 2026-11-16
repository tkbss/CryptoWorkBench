using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;

namespace CryptoWorkBenchAvalonia.Services.Appearance;

public sealed class ThemePreferenceService
{
    private readonly Application _application;
    private readonly ThemePreferenceStore _store;
    public ThemePreference Preference { get; private set; }
    public string? Error => _store.Error;
    public ThemePreferenceService(Application application, ThemePreferenceStore store)
    {
        _application = application; _store = store;
        Apply(store.Load());
    }
    private void Apply(ThemePreference preference)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (!Enum.IsDefined(preference)) throw new ArgumentOutOfRangeException(nameof(preference));
        Preference = preference;
        _application.RequestedThemeVariant = preference switch
        {
            ThemePreference.Light => ThemeVariant.Light, ThemePreference.Dark => ThemeVariant.Dark, _ => ThemeVariant.Default
        };
    }
    public bool Select(ThemePreference preference) { Apply(preference); return _store.Save(preference); }

    public MenuItem CreateMenu(Action<string> feedback)
    {
        var menu = new MenuItem { Header = "_Theme" };
        foreach (var value in Enum.GetValues<ThemePreference>())
        {
            var item = new MenuItem { Header = value.ToString(), ToggleType = MenuItemToggleType.Radio,
                IsChecked = Preference == value, GroupName = "CwbThemePreference" };
            item.Click += (_, _) =>
            {
                Select(value);
                foreach (var sibling in menu.Items.OfType<MenuItem>()) sibling.IsChecked = (string)sibling.Header! == Preference.ToString();
                feedback(Error ?? $"Theme: {Preference}");
            };
            menu.Items.Add(item);
        }
        menu.SubmenuOpened += (_, _) =>
        {
            foreach (var item in menu.Items.OfType<MenuItem>())
                item.IsChecked = (string)item.Header! == Preference.ToString();
        };
        return menu;
    }
}
