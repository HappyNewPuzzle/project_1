public sealed class WorldSessionService
{
    private readonly CharacterSaveService saves;
    public WorldSessionService(CharacterSaveService saves) => this.saves = saves;
    public async Task<CharacterSaveOutcome> SaveAndLogoutAsync(PlayerSession session,
        CancellationToken cancellationToken = default)
    {
        CharacterSaveOutcome outcome = await saves.SaveIfDirtyAsync(session, cancellationToken);
        if (outcome.Status is not (CharacterSaveStatus.Conflict or CharacterSaveStatus.Failed))
            session.Logout();
        return outcome;
    }
}
