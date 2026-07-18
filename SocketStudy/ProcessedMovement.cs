// The result of applying one queued movement request during a world tick.
public sealed record ProcessedMovement(
    QueuedMovementRequest QueuedRequest,
    MovementTickResult Result);
