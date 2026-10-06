using Microsoft.Win32;
using JSimulator.App.Services;

namespace JSimulator.App.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settings = SettingsService.Instance;

    public string[] TimeUnitOptions { get; } = { "seconds", "minutes", "hours", "unit time" };
    public int[] DecimalPlaceOptions { get; } = { 2, 3, 4, 5, 6 };

    public int DecimalPlaces
    {
        get => _settings.DecimalPlaces;
        set
        {
            if (_settings.DecimalPlaces == value) return;
            _settings.DecimalPlaces = value;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    public string TimeUnitLabel
    {
        get => _settings.TimeUnitLabel;
        set
        {
            if (_settings.TimeUnitLabel == value) return;
            _settings.TimeUnitLabel = value;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    public string BackgroundStyle
    {
        get => _settings.BackgroundStyle;
        set
        {
            if (_settings.BackgroundStyle == value) return;
            _settings.BackgroundStyle = value;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    public string DefaultExportFolderDisplay =>
        string.IsNullOrEmpty(_settings.DefaultExportFolder)
            ? "Not set — you'll be asked to choose a location each time you export."
            : _settings.DefaultExportFolder;

    public RelayCommand BrowseFolderCommand { get; }
    public RelayCommand ResetCommand { get; }

    public SettingsViewModel()
    {
        BrowseFolderCommand = new RelayCommand(BrowseFolder);
        ResetCommand = new RelayCommand(ResetToDefaults);
    }

    private void BrowseFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Choose a default folder for exported PDF reports" };
        if (dialog.ShowDialog() != true) return;

        _settings.DefaultExportFolder = dialog.FolderName;
        _settings.Save();
        OnPropertyChanged(nameof(DefaultExportFolderDisplay));
    }

    private void ResetToDefaults()
    {
        _settings.DecimalPlaces = 4;
        _settings.TimeUnitLabel = "unit time";
        _settings.BackgroundStyle = "Bubbles";
        _settings.DefaultExportFolder = null;
        _settings.Save();

        OnPropertyChanged(nameof(DecimalPlaces));
        OnPropertyChanged(nameof(TimeUnitLabel));
        OnPropertyChanged(nameof(BackgroundStyle));
        OnPropertyChanged(nameof(DefaultExportFolderDisplay));
    }
}
