using JSimulator.Core.Models;

namespace JSimulator.Core.Simulation;

/// <summary>
/// Generates non-negative random variates for a chosen distribution family,
/// parameterized by a target mean and variance so it can be swapped in for
/// either the arrival or service process of a simulation.
/// </summary>
public class RandomVariateGenerator
{
    private readonly Random _random;
    private readonly DistributionType _distribution;
    private readonly double _mean;
    private readonly double _variance;

    public RandomVariateGenerator(DistributionType distribution, double mean, double variance, int? seed = null)
    {
        if (mean <= 0) throw new ArgumentException("Mean must be greater than 0.", nameof(mean));
        if (variance < 0) throw new ArgumentException("Variance cannot be negative.", nameof(variance));

        _distribution = distribution;
        _mean = mean;
        _variance = variance;
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>Draws the next random variate, always clamped to a small positive minimum.</summary>
    public double Next()
    {
        const double floor = 1e-6;
        double value = _distribution switch
        {
            DistributionType.Deterministic => _mean,
            DistributionType.Exponential => NextExponential(_mean),
            DistributionType.Uniform => NextUniform(_mean, _variance),
            DistributionType.Normal => NextNormal(_mean, _variance),
            DistributionType.Gamma => NextGamma(_mean, _variance),
            _ => NextExponential(_mean)
        };
        return value < floor ? floor : value;
    }

    private double NextExponential(double mean)
    {
        double u = 1.0 - _random.NextDouble(); // avoid log(0)
        return -mean * Math.Log(u);
    }

    private double NextUniform(double mean, double variance)
    {
        // Var(Uniform(a,b)) = (b-a)^2 / 12  =>  half-width = sqrt(3 * variance)
        double halfWidth = Math.Sqrt(3.0 * variance);
        double a = Math.Max(0.0, mean - halfWidth);
        double b = mean + halfWidth;
        return a + _random.NextDouble() * (b - a);
    }

    private double NextNormal(double mean, double variance)
    {
        // Box-Muller transform
        double u1 = 1.0 - _random.NextDouble();
        double u2 = _random.NextDouble();
        double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        double stdDev = Math.Sqrt(variance);
        return mean + z * stdDev;
    }

    private double NextGamma(double mean, double variance)
    {
        if (variance <= 0) return mean;

        double shape = (mean * mean) / variance; // k
        double scale = variance / mean;           // theta

        double sample = shape >= 1.0
            ? MarsagliaTsangGamma(shape)
            : MarsagliaTsangGamma(shape + 1.0) * Math.Pow(1.0 - _random.NextDouble(), 1.0 / shape);

        return sample * scale;
    }

    /// <summary>Marsaglia & Tsang (2000) method for Gamma(shape, 1) with shape >= 1.</summary>
    private double MarsagliaTsangGamma(double shape)
    {
        double d = shape - 1.0 / 3.0;
        double c = 1.0 / Math.Sqrt(9.0 * d);

        while (true)
        {
            double x, v;
            do
            {
                double u1 = 1.0 - _random.NextDouble();
                double u2 = 1.0 - _random.NextDouble();
                x = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2); // standard normal
                v = 1.0 + c * x;
            } while (v <= 0);

            v = v * v * v;
            double uFinal = _random.NextDouble();

            if (uFinal < 1.0 - 0.0331 * (x * x) * (x * x)) return d * v;
            if (Math.Log(uFinal) < 0.5 * x * x + d * (1.0 - v + Math.Log(v))) return d * v;
        }
    }
}
