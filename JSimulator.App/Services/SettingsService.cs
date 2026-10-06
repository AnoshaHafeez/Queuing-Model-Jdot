using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace JSimulator.App.Services;

/// <summary>
/// App-wide user preferences, persisted as JSON under
/// <c>%AppData%\JSimulator\settings.json</c>. A single shared instance
/// (<see cref="Instance"/>) implements <see cref="INotifyPropertyChanged"/>
/// so any view — the Settings page itself, or a metric label elsewhere in
/// the app — can bind directly to it and update live when a preference
/// changes, without needing to go through a ViewModel.
/// </summary>
public class SettingsService : INotifyPropertyChanged
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "JSimulator", "settings.json");

    public static SettingsService Instance { get; } = Load();

    public event PropertyChangedEventHandler? PropertyChanged;

    private int _decimalPlaces = 4;
    public int DecimalPlaces
    {
        get => _decimalPlaces;
        set { if (_decimalPlaces != value) { _decimalPlaces = value; OnChanged(); } }
    }

    private string _timeUnitLabel = "unit time";
    public string TimeUnitLabel
    {
        get => _timeUnitLabel;
        set { if (_timeUnitLabel != value) { _timeUnitLabel = value; OnChanged(); } }
    }

    private string _backgroundStyle = "Bubbles";
    /// <summary>Which decorative background layer is shown behind page content:
    /// <c>"None"</c>, <c>"Bubbles"</c> (drifting bubbles), or <c>"Network3D"</c>
    /// (rotating 3D node network). Mutually exclusive — exactly one is active.</summary>
    public string BackgroundStyle
    {
        get => _backgroundStyle;
        set { if (_backgroundStyle != value) { _backgroundStyle = value; OnChanged(); } }
    }

    private string? _defaultExportFolder;
    public string? DefaultExportFolder
    {
        get => _defaultExportFolder;
        set { if (_defaultExportFolder != value) { _defaultExportFolder = value; OnChanged(); } }
    }

    private SettingsService() { }

    private static SettingsService Load()
    {
        var service = new SettingsService();
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var data = JsonSerializer.Deserialize<AppSettingsData>(json);
                if (data != null)
                {
                    service._decimalPlaces = data.DecimalPlaces;
                    service._timeUnitLabel = data.TimeUnitLabel;
                    service._backgroundStyle = string.IsNullOrWhiteSpace(data.BackgroundStyle) ? "Bubbles" : data.BackgroundStyle;
                    service._defaultExportFolder = data.DefaultExportFolder;
                }
            }
        }
        catch
        {
            // Missing or corrupt settings file — fall back to defaults silently.
        }
        return service;
    }

    /// <summary>Writes the current values to disk. Best-effort — the app keeps working from memory if this fails.</summary>
    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var data = new AppSettingsData
            {
                DecimalPlaces = DecimalPlaces,
                TimeUnitLabel = TimeUnitLabel,
                BackgroundStyle = BackgroundStyle,
                DefaultExportFolder = DefaultExportFolder
            };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Ignore — persistence is a nicety, not a requirement for the app to function.
        }
    }

    private void OnChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
