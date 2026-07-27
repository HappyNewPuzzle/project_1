// A movement request paired with the player session that owns it.
public sealed record QueuedMovementRequest(
    PlayerSession Session,
    MovementRequest Request)
{
    private readonly TaskCompletionSource<MovementTickResult> completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<MovementTickResult> Completion => completion.Task;

    public bool TryComplete(MovementTickResult result) => completion.TrySetResult(result);

    public bool TryCancel(CancellationToken cancellationToken) => completion.TrySetCanceled(cancellationToken);
}
