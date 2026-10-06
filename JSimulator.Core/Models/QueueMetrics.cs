namespace JSimulator.Core.Models;

/// <summary>
/// Standard steady-state performance metrics for a queueing system.
/// </summary>
public class QueueMetrics
{
    /// <summary>Name of the model that produced these metrics (e.g. "M/M/1").</summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>Traffic intensity / server utilization, ρ.</summary>
    public double Rho { get; set; }

    /// <summary>Expected number of customers in the system, L.</summary>
    public double L { get; set; }

    /// <summary>Expected number of customers in the queue (not being served), Lq.</summary>
    public double Lq { get; set; }

    /// <summary>Expected time a customer spends in the system, W.</summary>
    public double W { get; set; }

    /// <summary>Expected time a customer spends waiting in the queue, Wq.</summary>
    public double Wq { get; set; }

    /// <summary>Probability the system is empty, P0. Only exact for M/M/1; treated as an estimate elsewhere.</summary>
    public double P0 { get; set; }

    public override string ToString() =>
        $"{ModelName}: ρ={Rho:F4}  L={L:F4}  Lq={Lq:F4}  W={W:F4}  Wq={Wq:F4}  P0={P0:F4}";
}
