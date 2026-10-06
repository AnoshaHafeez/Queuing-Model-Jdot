namespace JSimulator.App.ViewModels;

/// <summary>
/// Landing page view model. Currently holds only static descriptive content,
/// kept as a proper view model (rather than a literal in XAML) so it can grow
/// without touching the view.
/// </summary>
public class HomeViewModel : ViewModelBase
{
    public string AppTitle => "Queuing Theory Suite";
    public string AppSubtitle => "Analytical calculators and discrete-event simulation for M/M/1, M/G/1 and G/G/1 queues.";

    public string MM1Description => "Poisson arrivals, exponential service, single server. The classic, fully closed-form model.";
    public string MG1Description => "Poisson arrivals, any service time distribution. Solved exactly via the Pollaczek-Khinchine formula.";
    public string GG1Description => "General arrivals and service. No exact closed form — solved here with Kingman's heavy-traffic approximation and verified by simulation.";
}
