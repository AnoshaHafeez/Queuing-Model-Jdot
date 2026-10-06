using JSimulator.Core.Simulation;

namespace JSimulator.Core.Models;

/// <summary>
/// Aggregated output of a discrete-event simulation run, alongside the
/// per-customer records used to produce it (handy for charting/inspection).
/// </summary>
public class SimulationResult
{
    public QueueMetrics Metrics { get; set; } = new();
    public List<Customer> Customers { get; set; } = new();
    public int CustomersSimulated { get; set; }
    public int WarmUpCustomersDiscarded { get; set; }
}
