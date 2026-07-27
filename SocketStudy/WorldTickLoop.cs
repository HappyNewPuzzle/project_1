// Runs the world simulation at a fixed server-controlled interval.
public sealed class WorldTickLoop
{
    private readonly WorldTickProcessor processor;
    private readonly TimeSpan interval;
    private readonly Action<DateTimeOffset>? processSimulation;

    public WorldTickLoop(
        WorldTickProcessor processor,
        TimeSpan interval,
        Action<DateTimeOffset>? processSimulation = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        this.processor = processor;
        this.interval = interval;
        this.processSimulation = processSimulation;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                processor.ProcessOnce();
                processSimulation?.Invoke(DateTimeOffset.UtcNow);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation is the normal shutdown path for the server tick loop.
        }
        finally
        {
            // Complete input already accepted before shutdown so callers do not wait forever.
            processor.ProcessOnce();
            processSimulation?.Invoke(DateTimeOffset.UtcNow);
        }
    }
}
