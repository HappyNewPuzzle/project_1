using System.Collections.Concurrent;

public sealed class CharacterSaveService
{
    private readonly ICharacterRepository repository;
    private readonly ConcurrentDictionary<long, SemaphoreSlim> playerGates = new();

    public CharacterSaveService(ICharacterRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CharacterSaveOutcome> SaveIfDirtyAsync(
        PlayerSession session,
        CancellationToken cancellationToken = default)
    {
        if (!session.IsAuthenticated || !session.IsDirty)
        {
            return new(CharacterSaveStatus.NotDirty, session.SaveVersion, 0);
        }

        SemaphoreSlim gate = playerGates.GetOrAdd(session.PlayerId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!session.IsDirty)
            {
                return new(CharacterSaveStatus.NotDirty, session.SaveVersion, 0);
            }

            for (int attempt = 1; attempt <= WorldRules.CharacterSaveMaxAttempts; attempt++)
            {
                try
                {
                    CharacterSaveData saved = await repository.SaveAsync(
                        session.CreateSaveData(),
                        cancellationToken);
                    session.MarkSaved(saved.Version);
                    return new(CharacterSaveStatus.Saved, saved.Version, attempt);
                }
                catch (CharacterConcurrencyException)
                {
                    return new(CharacterSaveStatus.Conflict, session.SaveVersion, attempt);
                }
                catch (Exception) when (attempt < WorldRules.CharacterSaveMaxAttempts)
                {
                    await Task.Delay(
                        TimeSpan.FromMilliseconds(WorldRules.CharacterSaveRetryDelay.TotalMilliseconds * attempt),
                        cancellationToken);
                }
                catch
                {
                    return new(CharacterSaveStatus.Failed, session.SaveVersion, attempt);
                }
            }

            return new(CharacterSaveStatus.Failed, session.SaveVersion, WorldRules.CharacterSaveMaxAttempts);
        }
        finally
        {
            gate.Release();
        }
    }
}
