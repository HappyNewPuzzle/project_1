public interface ICharacterRepository
{
    Task SaveAsync(CharacterSaveData character, CancellationToken cancellationToken = default);

    Task<CharacterSaveData?> LoadAsync(long playerId, CancellationToken cancellationToken = default);
}
