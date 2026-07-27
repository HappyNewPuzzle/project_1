// The level transition produced by one server-owned experience grant.
public sealed record ExperienceGainResult(
    int ExperienceAwarded,
    long TotalExperience,
    int PreviousLevel,
    int CurrentLevel)
{
    public bool LeveledUp => CurrentLevel > PreviousLevel;
}
