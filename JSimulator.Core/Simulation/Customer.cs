namespace JSimulator.Core.Simulation;

/// <summary>
/// Snapshot of a single simulated customer's timeline through the system.
/// </summary>
public class Customer
{
    public int Index { get; set; }
    public double InterarrivalTime { get; set; }
    public double ArrivalTime { get; set; }
    public double ServiceTime { get; set; }
    public double ServiceStartTime { get; set; }
    public double DepartureTime { get; set; }
    public double WaitTimeInQueue => ServiceStartTime - ArrivalTime;
    public double TimeInSystem => DepartureTime - ArrivalTime;
}
