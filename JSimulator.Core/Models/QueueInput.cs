namespace JSimulator.Core.Models;

/// <summary>
/// Parameters describing a single-server queueing system.
/// Not every field is used by every model: M/M/1 only needs
/// <see cref="ArrivalRate"/> and <see cref="ServiceRate"/>; M/G/1 additionally
/// needs <see cref="ServiceTimeVariance"/>; G/G/1 additionally needs
/// <see cref="ArrivalTimeVariance"/>.
/// </summary>
public class QueueInput
{
    /// <summary>Mean arrival rate, λ (customers per unit time).</summary>
    public double ArrivalRate { get; set; }

    /// <summary>Mean service rate, μ (customers per unit time).</summary>
    public double ServiceRate { get; set; }

    /// <summary>Variance of the service time distribution. Required for M/G/1 and G/G/1.</summary>
    public double ServiceTimeVariance { get; set; }

    /// <summary>Variance of the interarrival time distribution. Required for G/G/1.</summary>
    public double ArrivalTimeVariance { get; set; }

    /// <summary>Distribution family used to generate interarrival times in the simulation.</summary>
    public DistributionType ArrivalDistribution { get; set; } = DistributionType.Exponential;

    /// <summary>Distribution family used to generate service times in the simulation.</summary>
    public DistributionType ServiceDistribution { get; set; } = DistributionType.Exponential;

    /// <summary>Mean service time, E[S] = 1/μ.</summary>
    public double MeanServiceTime => ServiceRate > 0 ? 1.0 / ServiceRate : 0.0;

    /// <summary>Mean interarrival time, E[A] = 1/λ.</summary>
    public double MeanInterarrivalTime => ArrivalRate > 0 ? 1.0 / ArrivalRate : 0.0;

    /// <summary>Traffic intensity, ρ = λ/μ. Must be less than 1 for a stable (non-exploding) queue.</summary>
    public double Rho => ServiceRate > 0 ? ArrivalRate / ServiceRate : double.PositiveInfinity;

    /// <summary>Squared coefficient of variation of service time, Cs² = Var(S)/E[S]².</summary>
    public double ServiceScv => MeanServiceTime > 0 ? ServiceTimeVariance / (MeanServiceTime * MeanServiceTime) : 0.0;

    /// <summary>Squared coefficient of variation of interarrival time, Ca² = Var(A)/E[A]².</summary>
    public double ArrivalScv => MeanInterarrivalTime > 0 ? ArrivalTimeVariance / (MeanInterarrivalTime * MeanInterarrivalTime) : 0.0;

    /// <summary>Basic validation shared by all calculators. Returns null if valid, otherwise an error message.</summary>
    public string? Validate()
    {
        if (ArrivalRate <= 0) return "Arrival rate (λ) must be greater than 0.";
        if (ServiceRate <= 0) return "Service rate (μ) must be greater than 0.";
        if (Rho >= 1.0) return $"System is unstable: ρ = λ/μ = {Rho:F3} must be less than 1.";
        if (ServiceTimeVariance < 0) return "Service time variance cannot be negative.";
        if (ArrivalTimeVariance < 0) return "Arrival time variance cannot be negative.";
        return null;
    }
}
