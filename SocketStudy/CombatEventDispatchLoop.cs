// Sends queued combat events without blocking the world simulation tick.
public sealed class CombatEventDispatchLoop
{
    private readonly CombatEventQueue events;
    private readonly Func<CombatNotification, Task> dispatchAsync;
    private readonly TimeSpan interval;

    public CombatEventDispatchLoop(
        CombatEventQueue events,
        Func<CombatNotification, Task> dispatchAsync,
        TimeSpan interval)
    {
        this.events = events;
        this.dispatchAsync = dispatchAsync;
        this.interval = interval;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await DispatchPendingAsync();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation is the normal shutdown path.
        }
        finally
        {
            await DispatchPendingAsync();
        }
    }

    private async Task DispatchPendingAsync()
    {
        while (events.TryDequeue(out CombatNotification? notification))
        {
            if (notification is not null)
            {
                await dispatchAsync(notification);
            }
        }
    }
}
