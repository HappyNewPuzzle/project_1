// 주변 플레이어에게 전파할 월드 이벤트입니다.
public sealed record WorldEvent(
    // 이벤트 종류입니다.
    WorldEventType Type,
    // 이벤트를 발생시킨 플레이어 이름입니다.
    string ActorName,
    // 이벤트가 발생한 맵 ID입니다.
    int MapId,
    // 이벤트가 발생한 위치입니다.
    WorldPosition Position)
{
    // 플레이어 스폰 이벤트를 만듭니다.
    public static WorldEvent PlayerSpawned(string actorName, int mapId, WorldPosition position) =>
        new(WorldEventType.PlayerSpawned, actorName, mapId, position);

    // 플레이어 이동 이벤트를 만듭니다.
    public static WorldEvent PlayerMoved(string actorName, int mapId, WorldPosition position) =>
        new(WorldEventType.PlayerMoved, actorName, mapId, position);

    // 플레이어 디스폰 이벤트를 만듭니다.
    public static WorldEvent PlayerDespawned(string actorName, int mapId, WorldPosition position) =>
        new(WorldEventType.PlayerDespawned, actorName, mapId, position);

    // 플레이어가 기존 맵을 떠나는 이벤트를 만듭니다.
    public static WorldEvent PlayerLeftMap(string actorName, int mapId, WorldPosition position) =>
        new(WorldEventType.PlayerLeftMap, actorName, mapId, position);

    // 플레이어가 새 맵에 들어오는 이벤트를 만듭니다.
    public static WorldEvent PlayerEnteredMap(string actorName, int mapId, WorldPosition position) =>
        new(WorldEventType.PlayerEnteredMap, actorName, mapId, position);

    // 현재 텍스트 프로토콜에서 사용할 notice 문장으로 변환합니다.
    public string ToNoticeMessage()
    {
        // 이벤트 종류별로 기존 클라이언트 출력과 같은 문장을 만듭니다.
        return Type switch
        {
            WorldEventType.PlayerSpawned => $"{ActorName} spawned at {Position}",
            WorldEventType.PlayerMoved => $"{ActorName} moved to {Position}",
            WorldEventType.PlayerDespawned => $"{ActorName} despawned from {Position}",
            WorldEventType.PlayerLeftMap => $"{ActorName} left map {MapId} from {Position}",
            WorldEventType.PlayerEnteredMap => $"{ActorName} entered map {MapId} at {Position}",
            _ => throw new InvalidOperationException($"Unsupported world event type: {Type}")
        };
    }
}
