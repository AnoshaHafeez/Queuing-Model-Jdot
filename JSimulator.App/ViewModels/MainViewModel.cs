namespace JSimulator.App.ViewModels;

public class MainViewModel : ViewModelBase
{
    public CalculatorViewModel CalculatorVM { get; } = new();
    public SimulationViewModel SimulationVM { get; } = new();
    public ComparisonViewModel ComparisonVM { get; } = new();

    private string _currentTab = "Calculator";
    public string CurrentTab
    {
        get => _currentTab;
        set
        {
            if (SetProperty(ref _currentTab, value))
                OnPropertyChanged(nameof(CurrentViewModel));
        }
    }

    public object CurrentViewModel => CurrentTab switch
    {
        "Calculator" => CalculatorVM,
        "Simulation" => SimulationVM,
        "Comparison" => ComparisonVM,
        _ => CalculatorVM
    };
}
