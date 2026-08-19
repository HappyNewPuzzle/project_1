public sealed record LoadTestResult(int Total, int Succeeded, int Failed, TimeSpan Elapsed,
    double RequestsPerSecond, double AverageMilliseconds, double P50Milliseconds,
    double P95Milliseconds, double P99Milliseconds);
