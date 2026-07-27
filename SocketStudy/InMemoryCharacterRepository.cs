public sealed class InMemoryCharacterRepository : ICharacterRepository
{
    private readonly Dictionary<long, CharacterSaveData> characters = new();

    public Task<CharacterSaveData> SaveAsync(CharacterSaveData character, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CharacterSaveData? current = characters.GetValueOrDefault(character.PlayerId);
        if ((current?.Version ?? 0) != character.Version)
        {
            throw new CharacterConcurrencyException(character.PlayerId);
        }

        CharacterSaveData saved = character with { Version = character.Version + 1 };
        characters[character.PlayerId] = saved;
        return Task.FromResult(saved);
    }

    public Task<CharacterSaveData?> LoadAsync(long playerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(characters.GetValueOrDefault(playerId));
    }
}
