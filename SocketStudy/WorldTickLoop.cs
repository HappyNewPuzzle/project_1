// Runs the world simulation at a fixed server-controlled interval.
public sealed class WorldTickLoop
{
    private readonly WorldTickProcessor processor;
    private readonly TimeSpan interval;

    public WorldTickLoop(WorldTickProcessor processor, TimeSpan interval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        this.processor = processor;
        this.interval = interval;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                processor.ProcessOnce();
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
        }
    }
}
