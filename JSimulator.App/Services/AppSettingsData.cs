namespace JSimulator.App.Services;

/// <summary>Plain serializable shape for the settings file — no behavior, just the saved values.</summary>
public class AppSettingsData
{
    public int DecimalPlaces { get; set; } = 4;
    public string TimeUnitLabel { get; set; } = "unit time";

    /// <summary>One of <c>"None"</c>, <c>"Bubbles"</c>, or <c>"Network3D"</c> — see <see cref="SettingsService.BackgroundStyle"/>.</summary>
    public string BackgroundStyle { get; set; } = "Bubbles";

    public string? DefaultExportFolder { get; set; }
}
