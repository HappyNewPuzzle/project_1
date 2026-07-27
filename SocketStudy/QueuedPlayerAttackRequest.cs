// An attack request with an asynchronous completion signal for its network task.
public sealed record QueuedPlayerAttackRequest(PlayerAttackRequest Request)
{
    private readonly TaskCompletionSource<PlayerAttackResult> completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<PlayerAttackResult> Completion => completion.Task;

    public bool TryComplete(PlayerAttackResult result) => completion.TrySetResult(result);
}
