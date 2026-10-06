using JSimulator.Core.Models;

namespace JSimulator.Core.Reporting;

/// <summary>
/// Produces the human-readable formula text and interpretation notes that
/// turn a bare set of numbers into something that reads like an analyst
/// wrote it. Kept separate from <see cref="ReportGenerator"/> so the wording
/// can be tuned/tested independently of PDF layout.
/// </summary>
public static class ReportNarrative
{
    /// <summary>Short description of the model's assumptions (Kendall notation, arrivals, service).</summary>
    public static string Description(string modelName) => modelName switch
    {
        "M/M/1" => "Poisson arrivals, exponential service time, single server, infinite queue capacity. Exact closed-form solution.",
        "M/G/1" => "Poisson arrivals, general (any) service time distribution with known mean and variance, single server. Exact via the Pollaczek-Khinchine formula.",
        "G/G/1" => "General interarrival and service time distributions, single server. No exact closed form exists; Kingman's (Allen-Cunneen) heavy-traffic approximation is used instead.",
        _ => "Single-server queueing model."
    };

    /// <summary>The governing formula(s), written out for the report.</summary>
    public static string Formula(string modelName) => modelName switch
    {
        "M/M/1" =>
            "L = ρ / (1 - ρ)      Lq = ρ² / (1 - ρ)\n" +
            "W = 1 / (μ - λ)      Wq = ρ / (μ - λ)      P0 = 1 - ρ",
        "M/G/1" =>
            "Lq = (λ² · Var(S) + ρ²) / (2 · (1 - ρ))     [Pollaczek-Khinchine]\n" +
            "Wq = Lq / λ      W = Wq + E[S]      L = λ · W      P0 = 1 - ρ",
        "G/G/1" =>
            "Wq ≈ ((Ca² + Cs²) / 2) · (ρ / (1 - ρ)) · E[S]     [Kingman's approximation]\n" +
            "Lq = λ · Wq      W = Wq + E[S]      L = λ · W      P0 ≈ 1 - ρ",
        _ => string.Empty
    };

    /// <summary>
    /// Auto-generated plain-English interpretation of a result: utilization
    /// commentary, stability headroom, and model-specific caveats.
    /// </summary>
    public static List<string> Interpretation(QueueInput input, QueueMetrics metrics)
    {
        var notes = new List<string>();

        string utilizationBand = metrics.Rho switch
        {
            < 0.5 => "lightly loaded — the system spends most of its time idle",
            < 0.75 => "moderately loaded, with a comfortable stability margin",
            < 0.9 => "heavily loaded — queue length grows quickly as ρ approaches 1",
            _ => "close to saturation — small increases in load will cause queue length and wait time to grow sharply"
        };
        notes.Add($"At ρ = {metrics.Rho:P1}, the system is {utilizationBand}.");

        notes.Add($"On average, an arriving customer waits {metrics.Wq:F4} time units in the queue " +
                   $"and spends {metrics.W:F4} time units in the system overall.");

        notes.Add($"There is a {metrics.P0:P1} chance the system is empty at any given moment " +
                   $"(P0{(metrics.ModelName == "M/M/1" ? "" : ", derived from work conservation")}).");

        if (metrics.ModelName == "G/G/1")
        {
            notes.Add("This result uses Kingman's approximation, which is asymptotically exact as ρ → 1 " +
                       "but can over- or under-estimate away from heavy traffic — especially for low-variance " +
                       "(e.g. Uniform or Deterministic) distributions. Cross-check against a simulation run " +
                       "for parameters far from saturation.");
        }
        else if (metrics.ModelName == "M/G/1")
        {
            notes.Add($"Service time variability (Cs² = {input.ServiceScv:F3}) directly drives queue length via the " +
                       "Pollaczek-Khinchine formula: more variable service times mean longer queues at the same ρ.");
        }

        return notes;
    }

    /// <summary>Interpretation notes comparing a simulation run against its analytical counterpart.</summary>
    public static List<string> SimulationComparison(QueueMetrics analytical, SimulationResult simulation)
    {
        var sim = simulation.Metrics;
        double lqDiffPct = analytical.Lq != 0 ? Math.Abs(sim.Lq - analytical.Lq) / analytical.Lq * 100 : 0;
        double wqDiffPct = analytical.Wq != 0 ? Math.Abs(sim.Wq - analytical.Wq) / analytical.Wq * 100 : 0;

        var notes = new List<string>
        {
            $"The simulation ran {simulation.CustomersSimulated:N0} customers " +
            (simulation.WarmUpCustomersDiscarded > 0
                ? $"({simulation.WarmUpCustomersDiscarded:N0} discarded as warm-up)."
                : "."),
            $"Simulated Lq ({sim.Lq:F4}) differs from the analytical estimate ({analytical.Lq:F4}) by {lqDiffPct:F1}%; " +
            $"simulated Wq ({sim.Wq:F4}) differs from the analytical estimate ({analytical.Wq:F4}) by {wqDiffPct:F1}%."
        };

        notes.Add(lqDiffPct < 5
            ? "The analytical approximation tracks the simulation closely for this configuration."
            : "The gap between analytical and simulated results is non-trivial for this configuration — " +
              "treat the analytical figure as a rough guide and prefer the simulated numbers here.");

        return notes;
    }
}
