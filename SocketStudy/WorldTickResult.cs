// All movement results produced by one world tick.
public sealed record WorldTickResult(IReadOnlyList<ProcessedMovement> Movements);
