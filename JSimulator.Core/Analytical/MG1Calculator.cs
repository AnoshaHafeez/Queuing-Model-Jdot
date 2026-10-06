using JSimulator.Core.Models;

namespace JSimulator.Core.Analytical;

/// <summary>
/// Pollaczek-Khinchine formula for the M/G/1 queue
/// (Poisson arrivals, general service time distribution, single server).
/// Requires the mean and variance of the service time.
/// </summary>
public static class MG1Calculator
{
    public static QueueMetrics Calculate(QueueInput input)
    {
        var error = input.Validate();
        if (error != null) throw new InvalidOperationException(error);

        double lambda = input.ArrivalRate;
        double rho = input.Rho;
        double meanService = input.MeanServiceTime;
        double varService = input.ServiceTimeVariance;

        // Lq = (λ² * Var(S) + ρ²) / (2 * (1 - ρ))
        // Pollaczek-Khinchine: Lq = [λ² Var(S) + ρ²] / [2(1-ρ)].
        // Wq follows from Little's law; W adds the mean service time E[S] = 1/μ.
        double lq = (lambda * lambda * varService + rho * rho) / (2 * (1 - rho));
        double wq = lq / lambda;
        double w = wq + meanService;
        double l = lambda * w;

        // P0 is exact for M/M/1 only; for general M/G/1, P0 = 1 - ρ still holds
        // (a consequence of PASTA / work conservation for any M/G/1 queue).
        double p0 = 1 - rho;

        return new QueueMetrics
        {
            ModelName = "M/G/1",
            Rho = rho,
            L = l,
            Lq = lq,
            W = w,
            Wq = wq,
            P0 = p0
        };
    }
}
