// The movements produced while evaluating monsters during one world tick.
public sealed record MonsterAiTickResult(
    IReadOnlyList<MonsterMovement> Movements,
    IReadOnlyList<MonsterAttack> Attacks);
