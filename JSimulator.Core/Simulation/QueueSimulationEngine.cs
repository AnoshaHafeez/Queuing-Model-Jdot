using JSimulator.Core.Models;

namespace JSimulator.Core.Simulation;

/// <summary>
/// Simulates a single-server FIFO queue by generating interarrival and service
/// times from the chosen distributions and applying Lindley's recursion
/// (Wq[i] = max(0, Wq[i-1] + S[i-1] - A[i])) to obtain each customer's waiting
/// time. This works for any G/G/1 system, so the same engine drives the
/// M/M/1, M/G/1 and G/G/1 simulations depending on the distributions chosen.
/// </summary>
public class QueueSimulationEngine
{
    /// <summary>
    /// Runs the simulation.
    /// </summary>
    /// <param name="input">Arrival/service rates, variances and distribution families.</param>
    /// <param name="customerCount">Total number of customers to simulate.</param>
    /// <param name="warmUpCount">Number of leading customers excluded from the statistics (to remove startup bias).</param>
    /// <param name="seed">Optional seed for reproducible runs.</param>
    public SimulationResult Run(QueueInput input, int customerCount = 20000, int warmUpCount = 1000, int? seed = null)
    {
        if (customerCount <= 0) throw new ArgumentException("Customer count must be positive.");
        if (warmUpCount < 0 || warmUpCount >= customerCount) warmUpCount = Math.Min(1000, customerCount / 10);

        var arrivalGen = new RandomVariateGenerator(input.ArrivalDistribution, input.MeanInterarrivalTime, input.ArrivalTimeVariance, seed);
        var serviceGen = new RandomVariateGenerator(input.ServiceDistribution, input.MeanServiceTime, input.ServiceTimeVariance, seed.HasValue ? seed.Value + 1 : null);

        var customers = new List<Customer>(customerCount);

        double arrivalClock = 0.0;
        double previousDeparture = 0.0;
        double previousServiceTime = 0.0;
        double previousWaitInQueue = 0.0;

        for (int i = 0; i < customerCount; i++)
        {
            double interarrival = i == 0 ? 0.0 : arrivalGen.Next();
            arrivalClock += interarrival;

            double serviceTime = serviceGen.Next();

            double waitInQueue = i == 0
                ? 0.0
                : Math.Max(0.0, previousWaitInQueue + previousServiceTime - interarrival);

            double serviceStart = arrivalClock + waitInQueue;
            double departure = serviceStart + serviceTime;

            customers.Add(new Customer
            {
                Index = i,
                InterarrivalTime = interarrival,
                ArrivalTime = arrivalClock,
                ServiceTime = serviceTime,
                ServiceStartTime = serviceStart,
                DepartureTime = departure
            });

            previousDeparture = departure;
            previousServiceTime = serviceTime;
            previousWaitInQueue = waitInQueue;
        }

        var measured = customers.Skip(warmUpCount).ToList();

        double meanInterarrival = measured.Skip(1).Select(c => c.InterarrivalTime).DefaultIfEmpty(input.MeanInterarrivalTime).Average();
        double meanService = measured.Select(c => c.ServiceTime).Average();
        double meanWq = measured.Select(c => c.WaitTimeInQueue).Average();
        double meanW = measured.Select(c => c.TimeInSystem).Average();

        double lambdaEffective = meanInterarrival > 0 ? 1.0 / meanInterarrival : input.ArrivalRate;
        double lq = lambdaEffective * meanWq;
        double l = lambdaEffective * meanW;
        double rho = lambdaEffective * meanService;
        double p0 = Math.Max(0.0, 1.0 - rho);

        var metrics = new QueueMetrics
        {
            ModelName = "Simulation",
            Rho = rho,
            L = l,
            Lq = lq,
            W = meanW,
            Wq = meanWq,
            P0 = p0
        };

        return new SimulationResult
        {
            Metrics = metrics,
            Customers = customers,
            CustomersSimulated = customerCount,
            WarmUpCustomersDiscarded = warmUpCount
        };
    }
}
