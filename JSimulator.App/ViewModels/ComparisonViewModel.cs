using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using JSimulator.Core.Analytical;
using JSimulator.Core.Models;
using JSimulator.App.Services;

namespace JSimulator.App.ViewModels;

/// <summary>
/// Sweeps traffic intensity ρ (by varying λ against a fixed μ) and plots Lq
/// for all three models side by side, so the effect of service/arrival
/// variability on queue length is directly visible.
/// </summary>
public class ComparisonViewModel : ViewModelBase
{
    private double _serviceRate = 5;
    public double ServiceRate { get => _serviceRate; set { if (SetProperty(ref _serviceRate, value)) Recalculate(); } }

    private double _serviceScv = 1.0;
    public double ServiceScv { get => _serviceScv; set { if (SetProperty(ref _serviceScv, value)) Recalculate(); } }

    private double _arrivalScv = 1.5;
    public double ArrivalScv { get => _arrivalScv; set { if (SetProperty(ref _arrivalScv, value)) Recalculate(); } }

    private double _observedArrivalRate;
    /// <summary>
    /// The observed lambda imported from J. Data. The graph still sweeps lambda
    /// to compare models, but this value is marked as the data's reference point.
    /// </summary>
    public double ObservedArrivalRate { get => _observedArrivalRate; private set { if (SetProperty(ref _observedArrivalRate, value)) Recalculate(); } }

    private bool _hasImportedData;
    public bool HasImportedData => _hasImportedData;
    private string? _dataImportSummary;
    public string? DataImportSummary { get => _dataImportSummary; private set => SetProperty(ref _dataImportSummary, value); }
    public bool HasDataImportSummary => !string.IsNullOrEmpty(DataImportSummary);

    private string? _chartStatus;
    /// <summary>Non-fatal validation feedback for a temporarily invalid chart input.</summary>
    public string? ChartStatus { get => _chartStatus; private set => SetProperty(ref _chartStatus, value); }
    public bool HasChartStatus => !string.IsNullOrEmpty(ChartStatus);

    public PlotModel ComparisonPlot { get; } = new();

    public ComparisonViewModel()
    {
        BuildPlotSkeleton();
        Recalculate();

        ImportedQueueProfile.Instance.Imported += ApplyImportedProfile;
        if (ImportedQueueProfile.Instance.Current is { } profile) ApplyImportedProfile(profile);
    }

    private void ApplyImportedProfile(QueueExcelImportResult profile)
    {
        // Cs² = Var(S) / E[S]² and Ca² = Var(A) / E[A]². This converts the
        // imported variances into the dimensionless variability inputs used here.
        double meanService = 1.0 / profile.ServiceRate;
        double meanInterarrival = 1.0 / profile.ArrivalRate;

        _serviceRate = profile.ServiceRate;
        _serviceScv = profile.ServiceTimeVariance / (meanService * meanService);
        _arrivalScv = profile.ArrivalTimeVariance / (meanInterarrival * meanInterarrival);
        _observedArrivalRate = profile.ArrivalRate;
        _hasImportedData = true;

        OnPropertyChanged(nameof(ServiceRate));
        OnPropertyChanged(nameof(ServiceScv));
        OnPropertyChanged(nameof(ArrivalScv));
        OnPropertyChanged(nameof(ObservedArrivalRate));
        OnPropertyChanged(nameof(HasImportedData));
        DataImportSummary = $"J. Data loaded: {profile.RecordCount} records and {profile.InterarrivalIntervalCount} same-day arrival gaps. The dot marks its observed G/G/1 operating point.";
        OnPropertyChanged(nameof(HasDataImportSummary));
        Recalculate();
    }

    private void BuildPlotSkeleton()
    {
        ComparisonPlot.Title = "Expected Queue Length (Lq) vs. Traffic Intensity (ρ)";
        Theming.ChartTheme.Apply(ComparisonPlot);

        ComparisonPlot.Legends.Add(new Legend
        {
            LegendPosition = LegendPosition.TopLeft,
            LegendPlacement = LegendPlacement.Inside,
            LegendTextColor = Theming.ChartTheme.AxisText
        });

        ComparisonPlot.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = "ρ (utilization)",
            Minimum = 0,
            Maximum = 1,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = Theming.ChartTheme.GridlineColor
        });
        ComparisonPlot.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "Lq (expected customers waiting)",
            Minimum = 0,
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = Theming.ChartTheme.GridlineColor
        });

        ComparisonPlot.Series.Add(new LineSeries { Title = "M/M/1", Color = Theming.ChartTheme.Accent, StrokeThickness = 3 });
        ComparisonPlot.Series.Add(new LineSeries { Title = "M/G/1", Color = Theming.ChartTheme.Success, StrokeThickness = 3 });
        ComparisonPlot.Series.Add(new LineSeries { Title = "G/G/1 (approx.)", Color = Theming.ChartTheme.Warning, StrokeThickness = 3, LineStyle = LineStyle.Dash });
        ComparisonPlot.Series.Add(new ScatterSeries { Title = "Imported J. Data", MarkerType = MarkerType.Circle, MarkerSize = 6, MarkerFill = Theming.ChartTheme.Danger });
    }

    private void Recalculate()
    {
        var mm1 = (LineSeries)ComparisonPlot.Series[0];
        var mg1 = (LineSeries)ComparisonPlot.Series[1];
        var gg1 = (LineSeries)ComparisonPlot.Series[2];
        var observed = (ScatterSeries)ComparisonPlot.Series[3];
        mm1.Points.Clear();
        mg1.Points.Clear();
        gg1.Points.Clear();
        observed.Points.Clear();

        // A TextBox can temporarily produce zero or an incomplete value while a
        // user edits it. Keep the existing app window alive and show feedback
        // instead of allowing invalid chart input to propagate into OxyPlot.
        if (!double.IsFinite(ServiceRate) || ServiceRate <= 0 ||
            !double.IsFinite(ServiceScv) || ServiceScv < 0 ||
            !double.IsFinite(ArrivalScv) || ArrivalScv < 0)
        {
            ChartStatus = "Enter a positive service rate and non-negative SCV values to draw the comparison.";
            OnPropertyChanged(nameof(HasChartStatus));
            ComparisonPlot.InvalidatePlot(true);
            return;
        }

        ChartStatus = null;
        OnPropertyChanged(nameof(HasChartStatus));

        try
        {
            double meanService = 1.0 / ServiceRate;
            double serviceVariance = ServiceScv * meanService * meanService;

            for (double rho = 0.05; rho <= 0.95; rho += 0.02)
            {
                double lambda = rho * ServiceRate;
                double meanInterarrival = 1.0 / lambda;

            // M/M/1: Poisson arrivals (Ca^2 = 1), exponential service (Cs^2 = 1) — always.
            var mm1Input = new QueueInput
            {
                ArrivalRate = lambda,
                ServiceRate = ServiceRate,
                ServiceTimeVariance = meanService * meanService,
                ArrivalTimeVariance = meanInterarrival * meanInterarrival
            };
            if (mm1Input.Validate() is null)
                mm1.Points.Add(new DataPoint(rho, MM1Calculator.Calculate(mm1Input).Lq));

            // M/G/1: Poisson arrivals (Ca^2 = 1), general service using the chosen Cs^2.
            var mg1Input = new QueueInput
            {
                ArrivalRate = lambda,
                ServiceRate = ServiceRate,
                ServiceTimeVariance = serviceVariance,
                ArrivalTimeVariance = meanInterarrival * meanInterarrival
            };
            if (mg1Input.Validate() is null)
                mg1.Points.Add(new DataPoint(rho, MG1Calculator.Calculate(mg1Input).Lq));

            // G/G/1: general arrivals and service using the chosen Ca^2 and Cs^2.
            var gg1Input = new QueueInput
            {
                ArrivalRate = lambda,
                ServiceRate = ServiceRate,
                ServiceTimeVariance = serviceVariance,
                ArrivalTimeVariance = ArrivalScv * meanInterarrival * meanInterarrival
            };
                if (gg1Input.Validate() is null)
                    gg1.Points.Add(new DataPoint(rho, GG1Calculator.Calculate(gg1Input).Lq));
            }

            // The imported value is not used to constrain the sweep; it is shown as
            // a reference point on the G/G/1 curve for the observed operating load.
            double observedRho = ObservedArrivalRate / ServiceRate;
            if (_hasImportedData && double.IsFinite(observedRho) && observedRho > 0 && observedRho < 1)
            {
                double observedMeanInterarrival = 1.0 / ObservedArrivalRate;
                var observedInput = new QueueInput
                {
                    ArrivalRate = ObservedArrivalRate,
                    ServiceRate = ServiceRate,
                    ServiceTimeVariance = serviceVariance,
                    ArrivalTimeVariance = ArrivalScv * observedMeanInterarrival * observedMeanInterarrival
                };
                if (observedInput.Validate() is null)
                    observed.Points.Add(new ScatterPoint(observedRho, GG1Calculator.Calculate(observedInput).Lq));
            }

            ComparisonPlot.InvalidatePlot(true);
        }
        catch (Exception ex)
        {
            // Never let an optional chart redraw close the whole desktop app.
            ChartStatus = $"Comparison could not update: {ex.Message}";
            OnPropertyChanged(nameof(HasChartStatus));
            ComparisonPlot.InvalidatePlot(true);
        }
    }
}
