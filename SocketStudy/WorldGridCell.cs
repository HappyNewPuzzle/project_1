// AOI 탐색에서 사용할 맵 안의 격자 셀 좌표입니다.
public readonly record struct WorldGridCell(
    // 셀이 속한 게임 맵 ID입니다.
    int MapId,
    // 맵 안에서의 가로 셀 번호입니다.
    int X,
    // 맵 안에서의 세로 셀 번호입니다.
    int Y);
