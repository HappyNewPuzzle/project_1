// 월드에서 주변 플레이어에게 알려야 하는 이벤트 종류입니다.
public enum WorldEventType
{
    // 플레이어가 현재 맵에 등장했습니다.
    PlayerSpawned,
    // 플레이어가 위치를 이동했습니다.
    PlayerMoved,
    // 플레이어가 현재 맵에서 사라졌습니다.
    PlayerDespawned,
    // 플레이어가 맵 이동을 위해 기존 맵을 떠났습니다.
    PlayerLeftMap,
    // 플레이어가 맵 이동 후 새 맵에 들어왔습니다.
    PlayerEnteredMap
}
