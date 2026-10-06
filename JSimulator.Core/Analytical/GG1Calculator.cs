using JSimulator.Core.Models;

namespace JSimulator.Core.Analytical;

/// <summary>
/// Kingman's (Allen-Cunneen) approximation for the G/G/1 queue
/// (general interarrival and service time distributions, single server).
/// This is a well-established heavy-traffic approximation, not an exact formula.
/// </summary>
public static class GG1Calculator
{
    public static QueueMetrics Calculate(QueueInput input)
    {
        var error = input.Validate();
        if (error != null) throw new InvalidOperationException(error);

        double lambda = input.ArrivalRate;
        double rho = input.Rho;
        double meanService = input.MeanServiceTime;
        double ca2 = input.ArrivalScv;
        double cs2 = input.ServiceScv;

        // Kingman's approximation: Wq ≈ ((Ca² + Cs²) / 2) * (ρ / (1 - ρ)) * E[S]
        // Kingman's approximation: Wq ≈ ((Ca² + Cs²)/2) × (ρ/(1-ρ)) × E[S].
        // Then apply Little's law (Lq = λWq, L = λW) for the remaining metrics.
        double wq = ((ca2 + cs2) / 2.0) * (rho / (1 - rho)) * meanService;
        double lq = lambda * wq;
        double w = wq + meanService;
        double l = lambda * w;
        double p0 = 1 - rho; // approximate

        return new QueueMetrics
        {
            ModelName = "G/G/1",
            Rho = rho,
            L = l,
            Lq = lq,
            W = w,
            Wq = wq,
            P0 = p0
        };
    }
}
