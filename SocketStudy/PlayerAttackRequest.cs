// A player attack command waiting for authoritative world-tick validation.
public sealed record PlayerAttackRequest(
    PlayerSession Attacker,
    long MonsterId);
