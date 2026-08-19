public sealed class ServerMetrics
{
    private long accepted, rejected, active, messages, commands, commandTicks;
    public void ConnectionAccepted() { Interlocked.Increment(ref accepted); Interlocked.Increment(ref active); }
    public void ConnectionRejected() => Interlocked.Increment(ref rejected);
    public void ConnectionClosed() => Interlocked.Decrement(ref active);
    public void MessageReceived() => Interlocked.Increment(ref messages);
    public void CommandProcessed(TimeSpan elapsed) { Interlocked.Increment(ref commands); Interlocked.Add(ref commandTicks, elapsed.Ticks); }
    public ServerMetricsSnapshot Snapshot()
    {
        long count = Interlocked.Read(ref commands);
        return new(Interlocked.Read(ref accepted), Interlocked.Read(ref rejected),
            Interlocked.Read(ref active), Interlocked.Read(ref messages), count,
            count == 0 ? 0 : TimeSpan.FromTicks(Interlocked.Read(ref commandTicks) / count).TotalMilliseconds);
    }
    public string Format()
    {
        ServerMetricsSnapshot v = Snapshot();
        return $"Metrics: accepted={v.AcceptedConnections}, rejected={v.RejectedConnections}, active={v.ActiveConnections}, messages={v.ReceivedMessages}, commands={v.ProcessedCommands}, avg-command-ms={v.AverageCommandMilliseconds:F2}";
    }
}
