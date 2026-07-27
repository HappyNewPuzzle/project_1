// One server-authoritative monster movement produced by an AI tick.
public sealed record MonsterMovement(
    long MonsterId,
    long? TargetPlayerId,
    WorldPosition PreviousPosition,
    WorldPosition NextPosition);
