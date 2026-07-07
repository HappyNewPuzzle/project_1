// 주변 플레이어 스냅샷 목록과 제한 때문에 생략된 수를 함께 담습니다.
public sealed record NearbySnapshotResult(
    // 실제로 클라이언트에 보여줄 주변 플레이어 스냅샷입니다.
    NearbyPlayerSnapshot[] Snapshots,
    // AOI 조건에 맞는 전체 주변 플레이어 수입니다.
    int TotalCount)
{
    // 제한 때문에 응답에서 생략된 주변 플레이어 수입니다.
    public int HiddenCount => Math.Max(0, TotalCount - Snapshots.Length);
}
