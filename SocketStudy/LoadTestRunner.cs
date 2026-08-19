using System.Collections.Concurrent;
using System.Diagnostics;

public sealed class LoadTestRunner
{
    public async Task<LoadTestResult> RunAsync(int users, int requestsPerUser,
        Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(users);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestsPerUser);
        var latencies = new ConcurrentBag<double>();
        int succeeded = 0, failed = 0;
        var totalWatch = Stopwatch.StartNew();
        await Task.WhenAll(Enumerable.Range(0, users).Select(async _ =>
        {
            for (int i = 0; i < requestsPerUser; i++)
            {
                var watch = Stopwatch.StartNew();
                try { await operation(cancellationToken); Interlocked.Increment(ref succeeded); }
                catch when (!cancellationToken.IsCancellationRequested) { Interlocked.Increment(ref failed); }
                finally { latencies.Add(watch.Elapsed.TotalMilliseconds); }
            }
        }));
        totalWatch.Stop();
        double[] sorted = latencies.Order().ToArray();
        int total = succeeded + failed;
        return new(total, succeeded, failed, totalWatch.Elapsed,
            total / Math.Max(0.001, totalWatch.Elapsed.TotalSeconds), sorted.Average(),
            Percentile(sorted, .50), Percentile(sorted, .95), Percentile(sorted, .99));
    }

    private static double Percentile(double[] values, double percentile)
    {
        int index = Math.Clamp((int)Math.Ceiling(values.Length * percentile) - 1, 0, values.Length - 1);
        return values[index];
    }
}
