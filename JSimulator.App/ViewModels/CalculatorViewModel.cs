using System.IO;
using System.Linq;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Wpf;
using JSimulator.Core.Analytical;
using JSimulator.Core.Models;
using JSimulator.Core.Reporting;
using JSimulator.App.Services;

namespace JSimulator.App.ViewModels;

public class CalculatorViewModel : ViewModelBase
{
    private static readonly string[] Models = { "M/M/1", "M/G/1", "G/G/1" };
    public IReadOnlyList<string> AvailableModels => Models;

    private string _selectedModel = "M/M/1";
    public string SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (SetProperty(ref _selectedModel, value))
            {
                OnPropertyChanged(nameof(NeedsServiceVariance));
                OnPropertyChanged(nameof(NeedsArrivalVariance));
                Recalculate();
            }
        }
    }

    public bool NeedsServiceVariance => SelectedModel is "M/G/1" or "G/G/1";
    public bool NeedsArrivalVariance => SelectedModel is "G/G/1";

    private double _arrivalRate = 4;
    public double ArrivalRate { get => _arrivalRate; set { if (SetProperty(ref _arrivalRate, value)) Recalculate(); } }

    private double _serviceRate = 5;
    public double ServiceRate { get => _serviceRate; set { if (SetProperty(ref _serviceRate, value)) Recalculate(); } }

    private double _serviceTimeVariance = 0.04;
    public double ServiceTimeVariance { get => _serviceTimeVariance; set { if (SetProperty(ref _serviceTimeVariance, value)) Recalculate(); } }

    private double _arrivalTimeVariance = 0.0625;
    public double ArrivalTimeVariance { get => _arrivalTimeVariance; set { if (SetProperty(ref _arrivalTimeVariance, value)) Recalculate(); } }

    private string? _errorMessage;
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    private QueueMetrics _metrics = new();
    public QueueMetrics Metrics { get => _metrics; private set => SetProperty(ref _metrics, value); }

    public PlotModel SensitivityPlot { get; } = new();

    private string? _reportStatus;
    public string? ReportStatus { get => _reportStatus; private set => SetProperty(ref _reportStatus, value); }

    private string? _dataImportSummary;
    /// <summary>Explains the source and units of inputs most recently loaded from J. Data.</summary>
    public string? DataImportSummary { get => _dataImportSummary; private set => SetProperty(ref _dataImportSummary, value); }
    public bool HasDataImportSummary => !string.IsNullOrEmpty(DataImportSummary);

    private bool _usesImportedMinuteData;
    /// <summary>
    /// Results derived from the J. Data worksheet use minutes, while manual input
    /// continues to use the application's configured unit label.
    /// </summary>
    public string TimeUnitLabel => _usesImportedMinuteData ? "minutes" : Services.SettingsService.Instance.TimeUnitLabel;

    public RelayCommand ExportReportCommand { get; }
    public RelayCommand ImportDataCommand { get; }

    public CalculatorViewModel()
    {
        ExportReportCommand = new RelayCommand(ExportReport, () => !HasError);
        ImportDataCommand = new RelayCommand(ImportExcelData);
        BuildPlotSkeleton();
        Recalculate();

        // Metrics.L/Lq/W/Wq are formatted via MetricFormatConverter using the
        // Settings-page decimal precision. Changing that setting doesn't touch
        // Metrics itself, so re-raise its change notification to force the
        // bound TextBlocks to re-format with the new precision immediately.
        Services.SettingsService.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Services.SettingsService.DecimalPlaces))
                OnPropertyChanged(nameof(Metrics));
            if (e.PropertyName == nameof(Services.SettingsService.TimeUnitLabel) && !_usesImportedMinuteData)
                OnPropertyChanged(nameof(TimeUnitLabel));
        };
    }

    /// <summary>
    /// Imports J. Data's Date, Arrival_Time, Service_In, and Service_Out columns.
    /// Observed timestamps rarely follow exponential distributions exactly, so the
    /// calculator selects G/G/1 and uses their empirical variances by default.
    /// </summary>
    private void ImportExcelData()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import J. Data queue timestamps",
            Filter = "Excel workbooks (*.xlsx;*.xls)|*.xlsx;*.xls|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var imported = QueueExcelDataImporter.Import(dialog.FileName);

            // Apply all four estimates together. Calling the public setters one at
            // a time would recalculate transient combinations of old/new values.
            _arrivalRate = imported.ArrivalRate;
            _serviceRate = imported.ServiceRate;
            _arrivalTimeVariance = imported.ArrivalTimeVariance;
            _serviceTimeVariance = imported.ServiceTimeVariance;
            _usesImportedMinuteData = true;

            OnPropertyChanged(nameof(ArrivalRate));
            OnPropertyChanged(nameof(ServiceRate));
            OnPropertyChanged(nameof(ArrivalTimeVariance));
            OnPropertyChanged(nameof(ServiceTimeVariance));
            OnPropertyChanged(nameof(TimeUnitLabel));

            // G/G/1 is the appropriate default for measured arrival and service
            // sequences; M/M/1 and M/G/1 remain available if their assumptions fit.
            SelectedModel = "G/G/1";
            Recalculate();

            DataImportSummary =
                $"Imported {imported.RecordCount} records and {imported.InterarrivalIntervalCount} same-day arrival gaps. " +
                "Rates are customers/minute; time results and variances are in minutes and minutes².";
            OnPropertyChanged(nameof(HasDataImportSummary));

            // Broadcast the same validated estimates so the other two analysis
            // workspaces update without requiring the user to import again.
            ImportedQueueProfile.Instance.Publish(imported);
        }
        catch (Exception ex)
        {
            DataImportSummary = null;
            OnPropertyChanged(nameof(HasDataImportSummary));
            ErrorMessage = $"Excel import failed: {ex.Message}";
            OnPropertyChanged(nameof(HasError));
        }
    }

    private void ExportReport()
    {
        try
        {
            var defaultFolder = Services.SettingsService.Instance.DefaultExportFolder;
            var dialog = new SaveFileDialog
            {
                Filter = "PDF Report (*.pdf)|*.pdf",
                FileName = $"QueueReport_{SelectedModel.Replace('/', '-')}_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                InitialDirectory = !string.IsNullOrEmpty(defaultFolder) && Directory.Exists(defaultFolder) ? defaultFolder : string.Empty
            };
            if (dialog.ShowDialog() != true) return;

            var input = BuildInput(ArrivalRate);
            byte[]? chartBytes = null;
            try
            {
                chartBytes = ExportChartForPrint(SensitivityPlot);
            }
            catch
            {
                // Chart export is best-effort; report still generates without it.
            }

            var data = new ReportData
            {
                Title = $"{SelectedModel} Queueing Analysis",
                Input = input,
                Results = new List<QueueMetrics> { Metrics },
                ChartImage = chartBytes,
                ChartCaption = "Lq vs. traffic intensity (ρ), current input highlighted."
            };

            ReportGenerator.GenerateToFile(data, dialog.FileName);
            ReportStatus = $"Report saved to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            ReportStatus = $"Failed to generate report: {ex.Message}";
        }
    }

    /// <summary>
    /// Renders a plot to PNG using a light, print-friendly palette instead of
    /// the app's dark on-screen theme, so the chart stays readable when
    /// embedded on a white PDF page. Colors are swapped temporarily and
    /// restored afterwards; the on-screen chart is unaffected.
    /// </summary>
    private static byte[] ExportChartForPrint(PlotModel model)
    {
        var originalBackground = model.Background;
        var originalText = model.TextColor;
        var originalBorder = model.PlotAreaBorderColor;
        var originalGridlines = model.Axes.Select(a => a.MajorGridlineColor).ToList();

        model.Background = OxyColors.White;
        model.TextColor = OxyColor.FromRgb(0x1A, 0x1F, 0x2E);
        model.PlotAreaBorderColor = OxyColor.FromRgb(0xE3, 0xE7, 0xF1);
        foreach (var axis in model.Axes) axis.MajorGridlineColor = OxyColor.FromRgb(0xF0, 0xF1, 0xF6);
        model.InvalidatePlot(false);

        try
        {
            using var stream = new MemoryStream();
            var exporter = new PngExporter { Width = 900, Height = 500 };
            exporter.Export(model, stream);
            return stream.ToArray();
        }
        finally
        {
            model.Background = originalBackground;
            model.TextColor = originalText;
            model.PlotAreaBorderColor = originalBorder;
            for (int i = 0; i < model.Axes.Count; i++) model.Axes[i].MajorGridlineColor = originalGridlines[i];
            model.InvalidatePlot(false);
        }
    }

    private QueueInput BuildInput(double arrivalRate)
    {
        return new QueueInput
        {
            ArrivalRate = arrivalRate,
            ServiceRate = ServiceRate,
            ServiceTimeVariance = SelectedModel == "M/M/1" ? Math.Pow(1.0 / ServiceRate, 2) : ServiceTimeVariance,
            ArrivalTimeVariance = SelectedModel switch
            {
                "M/M/1" or "M/G/1" => Math.Pow(1.0 / arrivalRate, 2), // Poisson arrivals => Ca^2 = 1
                _ => ArrivalTimeVariance
            }
        };
    }

    private QueueMetrics CalculateFor(QueueInput input) => SelectedModel switch
    {
        "M/M/1" => MM1Calculator.Calculate(input),
        "M/G/1" => MG1Calculator.Calculate(input),
        _ => GG1Calculator.Calculate(input)
    };

    private void Recalculate()
    {
        try
        {
            var input = BuildInput(ArrivalRate);
            var error = input.Validate();
            if (error != null)
            {
                ErrorMessage = error;
                OnPropertyChanged(nameof(HasError));
                return;
            }

            Metrics = CalculateFor(input);
            ErrorMessage = null;
            OnPropertyChanged(nameof(HasError));
            UpdateSensitivityPlot();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            OnPropertyChanged(nameof(HasError));
        }
    }

    private void BuildPlotSkeleton()
    {
        SensitivityPlot.Title = "Queue Length (Lq) vs. Traffic Intensity (ρ)";
        Theming.ChartTheme.Apply(SensitivityPlot);

        SensitivityPlot.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = "ρ (utilization)",
            Minimum = 0,
            Maximum = 1,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = Theming.ChartTheme.GridlineColor
        });
        SensitivityPlot.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "Lq (expected customers waiting)",
            Minimum = 0,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = Theming.ChartTheme.GridlineColor
        });

        SensitivityPlot.Series.Add(new LineSeries
        {
            Title = "Lq",
            Color = Theming.ChartTheme.Accent,
            StrokeThickness = 3,
            MarkerType = MarkerType.None
        });

        SensitivityPlot.Series.Add(new ScatterSeries
        {
            Title = "Current input",
            MarkerType = MarkerType.Circle,
            MarkerSize = 7,
            MarkerFill = Theming.ChartTheme.Danger
        });
    }

    private void UpdateSensitivityPlot()
    {
        var line = (LineSeries)SensitivityPlot.Series[0];
        var point = (ScatterSeries)SensitivityPlot.Series[1];
        line.Points.Clear();
        point.Points.Clear();

        for (double rho = 0.05; rho <= 0.95; rho += 0.02)
        {
            double lambda = rho * ServiceRate;
            try
            {
                var input = BuildInput(lambda);
                if (input.Validate() != null) continue;
                var metrics = CalculateFor(input);
                line.Points.Add(new DataPoint(rho, metrics.Lq));
            }
            catch
            {
                // Skip unstable/invalid points in the sweep.
            }
        }

        point.Points.Add(new ScatterPoint(Metrics.Rho, Metrics.Lq));
        SensitivityPlot.InvalidatePlot(true);
    }
}
