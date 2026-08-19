public sealed record ServerMetricsSnapshot(long AcceptedConnections, long RejectedConnections,
    long ActiveConnections, long ReceivedMessages, long ProcessedCommands,
    double AverageCommandMilliseconds);
