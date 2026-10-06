using OxyPlot;

namespace JSimulator.App.Theming;

/// <summary>
/// OxyPlot color constants mirroring the palette in <c>Themes/Theme.xaml</c>.
/// XAML resources aren't reachable from chart-construction code (OxyPlot
/// models are built in C#, not styled via WPF resources), so this keeps the
/// three charts visually consistent with the app's light interface.
/// </summary>
public static class ChartTheme
{
    public static readonly OxyColor PlotAreaBorder = OxyColor.FromRgb(0xE4, 0xE9, 0xF2);
    public static readonly OxyColor AxisText = OxyColor.FromRgb(0x4B, 0x5A, 0x73);
    public static readonly OxyColor GridlineColor = OxyColor.FromRgb(0xEE, 0xF1, 0xF6);

    public static readonly OxyColor Accent = OxyColor.FromRgb(0x31, 0x5E, 0xFB);
    public static readonly OxyColor Success = OxyColor.FromRgb(0x15, 0x9B, 0x68);
    public static readonly OxyColor Warning = OxyColor.FromRgb(0xE2, 0x94, 0x22);
    public static readonly OxyColor Danger = OxyColor.FromRgb(0xD1, 0x43, 0x43);

    /// <summary>Applies the shared border/text/background styling to a plot model.</summary>
    public static void Apply(PlotModel model)
    {
        model.PlotAreaBorderColor = PlotAreaBorder;
        model.TextColor = AxisText;
        model.Background = OxyColors.Transparent;
    }
}
