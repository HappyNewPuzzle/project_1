public sealed record CharacterSaveOutcome(
    CharacterSaveStatus Status,
    long Version,
    int Attempts);
