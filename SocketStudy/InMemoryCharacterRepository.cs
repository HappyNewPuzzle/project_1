public sealed class InMemoryCharacterRepository : ICharacterRepository
{
    private readonly Dictionary<long, CharacterSaveData> characters = new();

    public Task SaveAsync(CharacterSaveData character, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        characters[character.PlayerId] = character;
        return Task.CompletedTask;
    }

    public Task<CharacterSaveData?> LoadAsync(long playerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(characters.GetValueOrDefault(playerId));
    }
}
