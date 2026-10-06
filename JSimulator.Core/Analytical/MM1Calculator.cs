using JSimulator.Core.Models;

namespace JSimulator.Core.Analytical;

/// <summary>
/// Closed-form steady-state formulas for the M/M/1 queue
/// (Poisson arrivals, exponential service, single server, infinite capacity).
/// </summary>
public static class MM1Calculator
{
    public static QueueMetrics Calculate(QueueInput input)
    {
        var error = input.Validate();
        if (error != null) throw new InvalidOperationException(error);

        double rho = input.Rho;
        double lambda = input.ArrivalRate;
        double mu = input.ServiceRate;

        // Exact M/M/1 steady-state formulas:
        // L = ρ/(1-ρ), Lq = ρ²/(1-ρ), W = 1/(μ-λ), Wq = ρ/(μ-λ), P0 = 1-ρ.
        // Little's law (L = λW and Lq = λWq) is satisfied by these expressions.
        double l = rho / (1 - rho);
        double lq = (rho * rho) / (1 - rho);
        double w = 1.0 / (mu - lambda);
        double wq = rho / (mu - lambda);
        double p0 = 1 - rho;

        return new QueueMetrics
        {
            ModelName = "M/M/1",
            Rho = rho,
            L = l,
            Lq = lq,
            W = w,
            Wq = wq,
            P0 = p0
        };
    }

    /// <summary>Probability of exactly n customers in the system, Pn = (1-ρ)ρⁿ.</summary>
    public static double ProbabilityOfN(QueueInput input, int n)
    {
        double rho = input.Rho;
        return (1 - rho) * Math.Pow(rho, n);
    }

    /// <summary>Probability that an arriving customer must wait (system is not empty), = ρ.</summary>
    public static double ProbabilityOfWaiting(QueueInput input) => input.Rho;
}
