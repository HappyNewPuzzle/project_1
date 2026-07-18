// A movement request paired with the player session that owns it.
public sealed record QueuedMovementRequest(
    PlayerSession Session,
    MovementRequest Request);
