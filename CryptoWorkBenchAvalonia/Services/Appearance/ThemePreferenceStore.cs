using System;
using System.IO;
using System.Text.Json;

namespace CryptoWorkBenchAvalonia.Services.Appearance;

public enum ThemePreference { System, Light, Dark }

public sealed class ThemePreferenceStore
{
    private readonly string _path;
    public string? Error { get; private set; }
    public ThemePreferenceStore(string? path = null) => _path = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CryptoWorkBench", "appearance-v1.json");

    public ThemePreference Load()
    {
        Error = null;
        try
        {
            if (!File.Exists(_path)) return ThemePreference.System;
            if (new FileInfo(_path).Length > 4096) throw new InvalidDataException("Appearance file exceeds size limit.");
            using var doc = JsonDocument.Parse(File.ReadAllText(_path));
            var root = doc.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != 1) throw new InvalidDataException("Unsupported appearance schema.");
            return root.GetProperty("theme").GetString() switch
            {
                "System" => ThemePreference.System, "Light" => ThemePreference.Light,
                "Dark" => ThemePreference.Dark, _ => throw new InvalidDataException("Unknown theme.")
            };
        }
        catch (Exception e) when (Recoverable(e)) { Error = "Appearance could not be loaded; using System. " + e.Message; return ThemePreference.System; }
    }

    public bool Save(ThemePreference preference)
    {
        if (!Enum.IsDefined(preference)) throw new ArgumentOutOfRangeException(nameof(preference));
        Error = null;
        var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            File.WriteAllText(temp, JsonSerializer.Serialize(new { schemaVersion = 1, theme = preference.ToString() }));
            File.Move(temp, _path, true);
            return true;
        }
        catch (Exception e) when (Recoverable(e)) { Error = "Theme applied, but appearance could not be saved. " + e.Message; return false; }
        finally { try { if (File.Exists(temp)) File.Delete(temp); } catch (Exception e) when (Recoverable(e)) { } }
    }
    private static bool Recoverable(Exception e) => e is InvalidDataException or IOException or UnauthorizedAccessException or JsonException
        or InvalidOperationException or System.Collections.Generic.KeyNotFoundException or FormatException;
}
