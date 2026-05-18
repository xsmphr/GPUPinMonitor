using System.IO;
using System.Text.Json;

namespace AstralPinWidget.Services;

public sealed record WidgetSettings(double? Left, double? Top, bool IsTopmost);

public sealed class WindowSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public WindowSettingsStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _settingsPath = Path.Combine(appData, "AstralPinWidget", "settings.json");
    }

    public WidgetSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new WidgetSettings(null, null, true);
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<WidgetSettings>(json, SerializerOptions)
                   ?? new WidgetSettings(null, null, true);
        }
        catch
        {
            return new WidgetSettings(null, null, true);
        }
    }

    public void Save(WidgetSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(_settingsPath, json);
    }
}
