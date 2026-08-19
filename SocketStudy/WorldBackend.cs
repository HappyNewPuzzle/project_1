public sealed record WorldBackend(string ServerId, string Host, int Port,
    int MapId, int ActivePlayers, int Capacity, DateTimeOffset LastHeartbeatAt)
{
    public bool CanAccept(DateTimeOffset now, TimeSpan heartbeatTimeout) =>
        ActivePlayers < Capacity && now - LastHeartbeatAt <= heartbeatTimeout;
}
