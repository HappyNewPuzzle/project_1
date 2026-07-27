public sealed class CharacterAutosaveLoop
{
    private readonly Func<PlayerSession[]> getSessions;
    private readonly CharacterSaveService saves;
    private readonly TimeSpan interval;

    public CharacterAutosaveLoop(
        Func<PlayerSession[]> getSessions,
        CharacterSaveService saves,
        TimeSpan interval)
    {
        this.getSessions = getSessions;
        this.saves = saves;
        this.interval = interval;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await SaveAllAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            await SaveAllAsync(CancellationToken.None);
        }
    }

    public async Task SaveAllAsync(CancellationToken cancellationToken)
    {
        foreach (PlayerSession session in getSessions())
        {
            await saves.SaveIfDirtyAsync(session, cancellationToken);
        }
    }
}
