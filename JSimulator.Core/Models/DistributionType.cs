namespace JSimulator.Core.Models;

/// <summary>
/// Statistical distribution used to generate interarrival or service times.
/// </summary>
public enum DistributionType
{
    /// <summary>Memoryless exponential distribution (the "M" in Kendall notation).</summary>
    Exponential,

    /// <summary>Fixed, constant value with zero variance (the "D" in Kendall notation).</summary>
    Deterministic,

    /// <summary>Continuous uniform distribution over [mean - w, mean + w].</summary>
    Uniform,

    /// <summary>Normal (Gaussian) distribution, truncated to positive values.</summary>
    Normal,

    /// <summary>Gamma distribution, fit from a target mean and variance.</summary>
    Gamma
}
