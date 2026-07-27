public sealed class CharacterConcurrencyException : Exception
{
    public CharacterConcurrencyException(long playerId)
        : base($"Character {playerId} was changed by another save operation.")
    {
    }
}
