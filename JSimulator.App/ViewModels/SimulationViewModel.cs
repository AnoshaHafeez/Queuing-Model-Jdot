using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using JSimulator.Core.Analytical;
using JSimulator.Core.Models;
using JSimulator.Core.Simulation;
using JSimulator.App.Services;

namespace JSimulator.App.ViewModels;

public class SimulationViewModel : ViewModelBase
{
    public IReadOnlyList<DistributionType> AvailableDistributions { get; } =
        Enum.GetValues<DistributionType>();

    private double _arrivalRate = 4;
    public double ArrivalRate { get => _arrivalRate; set => SetProperty(ref _arrivalRate, value); }

    private double _serviceRate = 5;
    public double ServiceRate { get => _serviceRate; set => SetProperty(ref _serviceRate, value); }

    private double _serviceTimeVariance = 0.04;
    public double ServiceTimeVariance { get => _serviceTimeVariance; set => SetProperty(ref _serviceTimeVariance, value); }

    private double _arrivalTimeVariance = 0.0625;
    public double ArrivalTimeVariance { get => _arrivalTimeVariance; set => SetProperty(ref _arrivalTimeVariance, value); }

    private DistributionType _arrivalDistribution = DistributionType.Exponential;
    public DistributionType ArrivalDistribution { get => _arrivalDistribution; set => SetProperty(ref _arrivalDistribution, value); }

    private DistributionType _serviceDistribution = DistributionType.Exponential;
    public DistributionType ServiceDistribution { get => _serviceDistribution; set => SetProperty(ref _serviceDistribution, value); }

    private int _customerCount = 50000;
    public int CustomerCount { get => _customerCount; set => SetProperty(ref _customerCount, value); }

    private int _warmUpCount = 2000;
    public int WarmUpCount { get => _warmUpCount; set => SetProperty(ref _warmUpCount, value); }

    private bool _isRunning;
    public bool IsRunning { get => _isRunning; private set { if (SetProperty(ref _isRunning, value)) RunCommand.NotifyCanExecuteChanged(); } }

    private string? _errorMessage;
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    private string? _dataImportSummary;
    public string? DataImportSummary { get => _dataImportSummary; private set => SetProperty(ref _dataImportSummary, value); }
    public bool HasDataImportSummary => !string.IsNullOrEmpty(DataImportSummary);

    private bool _hasResult;
    public bool HasResult { get => _hasResult; private set => SetProperty(ref _hasResult, value); }

    private QueueMetrics _simulatedMetrics = new();
    public QueueMetrics SimulatedMetrics { get => _simulatedMetrics; private set => SetProperty(ref _simulatedMetrics, value); }

    private QueueMetrics _analyticalReference = new();
    public QueueMetrics AnalyticalReference { get => _analyticalReference; private set => SetProperty(ref _analyticalReference, value); }

    public PlotModel ConvergencePlot { get; } = new();

    public NotifyingRelayCommand RunCommand { get; }

    public SimulationViewModel()
    {
        BuildPlotSkeleton();
        RunCommand = new NotifyingRelayCommand(async () => await RunAsync(), () => !IsRunning);

        // A Gamma distribution reproduces the imported mean and variance exactly;
        // users can still choose a different family to test another assumption.
        ImportedQueueProfile.Instance.Imported += ApplyImportedProfile;
        if (ImportedQueueProfile.Instance.Current is { } profile) ApplyImportedProfile(profile);
    }

    private void ApplyImportedProfile(QueueExcelImportResult profile)
    {
        // Apply the complete profile as a batch so Simulation uses the identical
        // λ, μ, Var(A), and Var(S) values shown in the Calculator workspace.
        _arrivalRate = profile.ArrivalRate;
        _serviceRate = profile.ServiceRate;
        _arrivalTimeVariance = profile.ArrivalTimeVariance;
        _serviceTimeVariance = profile.ServiceTimeVariance;
        _arrivalDistribution = DistributionType.Gamma;
        _serviceDistribution = DistributionType.Gamma;
        // Match the simulation run length to the imported observation count.
        // No artificial warm-up is used: the imported records already describe
        // the real observation period rather than a newly started system.
        _customerCount = profile.RecordCount;
        _warmUpCount = 0;

        OnPropertyChanged(nameof(ArrivalRate));
        OnPropertyChanged(nameof(ServiceRate));
        OnPropertyChanged(nameof(ArrivalTimeVariance));
        OnPropertyChanged(nameof(ServiceTimeVariance));
        OnPropertyChanged(nameof(ArrivalDistribution));
        OnPropertyChanged(nameof(ServiceDistribution));
        OnPropertyChanged(nameof(CustomerCount));
        OnPropertyChanged(nameof(WarmUpCount));

        DataImportSummary =
            $"J. Data loaded: {profile.RecordCount} records and {profile.InterarrivalIntervalCount} same-day arrival gaps. " +
            "The run length matches the data count; Gamma samples match the imported mean and variance (minutes).";
        OnPropertyChanged(nameof(HasDataImportSummary));
    }

    private async Task RunAsync()
    {
        var input = new QueueInput
        {
            ArrivalRate = ArrivalRate,
            ServiceRate = ServiceRate,
            ServiceTimeVariance = ServiceTimeVariance,
            ArrivalTimeVariance = ArrivalTimeVariance,
            ArrivalDistribution = ArrivalDistribution,
            ServiceDistribution = ServiceDistribution
        };

        var validationError = input.Validate();
        if (validationError != null)
        {
            ErrorMessage = validationError;
            OnPropertyChanged(nameof(HasError));
            return;
        }

        if (CustomerCount < 3)
        {
            ErrorMessage = "Customer count must be at least 3.";
            OnPropertyChanged(nameof(HasError));
            return;
        }

        ErrorMessage = null;
        OnPropertyChanged(nameof(HasError));
        IsRunning = true;

        try
        {
            var result = await Task.Run(() => new QueueSimulationEngine().Run(input, CustomerCount, WarmUpCount));

            SimulatedMetrics = result.Metrics;
            AnalyticalReference = GG1Calculator.Calculate(input);
            UpdateConvergencePlot(result);
            HasResult = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Simulation failed: {ex.Message}";
            OnPropertyChanged(nameof(HasError));
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void BuildPlotSkeleton()
    {
        ConvergencePlot.Title = "Cumulative Average Wait Time in Queue (Wq)";
        Theming.ChartTheme.Apply(ConvergencePlot);

        ConvergencePlot.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = "Customer index",
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = Theming.ChartTheme.GridlineColor
        });
        ConvergencePlot.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "Cumulative average Wq",
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = Theming.ChartTheme.GridlineColor
        });

        ConvergencePlot.Series.Add(new LineSeries
        {
            Title = "Simulated Wq",
            Color = Theming.ChartTheme.Success,
            StrokeThickness = 2,
            MarkerType = MarkerType.None
        });
    }

    private void UpdateConvergencePlot(SimulationResult result)
    {
        var series = (LineSeries)ConvergencePlot.Series[0];
        series.Points.Clear();

        var customers = result.Customers;
        int step = Math.Max(1, customers.Count / 400); // cap plotted points for smooth rendering
        double runningSum = 0;

        for (int i = 0; i < customers.Count; i++)
        {
            runningSum += customers[i].WaitTimeInQueue;
            if (i % step == 0 || i == customers.Count - 1)
            {
                double cumulativeAverage = runningSum / (i + 1);
                series.Points.Add(new DataPoint(i, cumulativeAverage));
            }
        }

        ConvergencePlot.InvalidatePlot(true);
    }
}
