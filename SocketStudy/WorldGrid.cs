// 월드 좌표를 AOI 탐색용 격자로 변환합니다.
public static class WorldGrid
{
    // 특정 맵과 위치가 어느 AOI 셀에 속하는지 계산합니다.
    public static WorldGridCell GetCell(int mapId, WorldPosition position)
    {
        // 음수 좌표도 왼쪽/아래 셀로 자연스럽게 내려가도록 floor 나눗셈을 사용합니다.
        int cellX = (int)Math.Floor((double)position.X / WorldRules.GridCellSize);
        // y 좌표도 x와 같은 방식으로 셀 번호를 계산합니다.
        int cellY = (int)Math.Floor((double)position.Y / WorldRules.GridCellSize);
        // 계산된 맵/셀 좌표를 값 객체로 반환합니다.
        return new WorldGridCell(mapId, cellX, cellY);
    }

    // 중심 셀과 바로 주변 8개 셀을 함께 반환합니다.
    public static WorldGridCell[] GetNeighborCells(WorldGridCell center)
    {
        // 시야 거리가 셀 크기 이하이므로 인접 3x3 셀만 검사하면 nearby 후보를 모두 포함할 수 있습니다.
        var cells = new List<WorldGridCell>(capacity: 9);

        // 왼쪽, 가운데, 오른쪽 셀을 차례로 훑습니다.
        for (int x = center.X - 1; x <= center.X + 1; x++)
        {
            // 아래, 가운데, 위 셀을 차례로 훑습니다.
            for (int y = center.Y - 1; y <= center.Y + 1; y++)
            {
                // 같은 맵 안의 주변 셀만 후보로 넣습니다.
                cells.Add(new WorldGridCell(center.MapId, x, y));
            }
        }

        // 외부에서 수정할 수 없도록 배열로 복사해 반환합니다.
        return cells.ToArray();
    }
}
