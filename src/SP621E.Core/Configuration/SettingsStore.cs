namespace SP621E.Core.Configuration;

using System.IO;
using System.Text.Json;

/// <summary>
/// Loads and persists <see cref="AppSettings"/> as JSON under
/// %LocalAppData%\SP621EMusicReactive\settings.json (same folder as diagnostics).
/// Corrupt or missing files fall back to defaults, logged via AppLog.
/// </summary>
public sealed class SettingsStore
{
    public const string AppDataFolderName = "SP621EMusicReactive";

    private readonly string _directory;
    private readonly string _path;
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public SettingsStore(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppDataFolderName);
        _path = Path.Combine(_directory, "settings.json");
    }

    public string SettingsPath => _path;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new AppSettings();

            var json = File.ReadAllText(_path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            if (settings is null)
            {
                Logging.AppLog.Warn($"Settings file {_path} was empty; using defaults.");
                return new AppSettings();
            }
            return settings;
        }
        catch (Exception ex)
        {
            Logging.AppLog.Error($"Failed to load settings from {_path}; using defaults.", ex);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            var json = JsonSerializer.Serialize(settings, _json);
            File.WriteAllText(_path, json);
        }
        catch (Exception ex)
        {
            Logging.AppLog.Error($"Failed to save settings to {_path}.", ex);
        }
    }
}