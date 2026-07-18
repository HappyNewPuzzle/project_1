// Indexes spawned clients by AOI grid cell for fast nearby candidate lookup.
public sealed class WorldGridIndex
{
    private readonly Dictionary<WorldGridCell, HashSet<ClientConnection>> clientsByCell = new();
    private readonly Dictionary<ClientConnection, WorldGridCell> cellByClient = new();

    public int Count => cellByClient.Count;

    public void Refresh(ClientConnection client)
    {
        Remove(client);

        if (!client.Session.IsSpawned)
        {
            return;
        }

        WorldGridCell cell = WorldGrid.GetCell(client.Session.MapId, client.Session.Position);
        if (!clientsByCell.TryGetValue(cell, out HashSet<ClientConnection>? cellClients))
        {
            cellClients = new HashSet<ClientConnection>();
            clientsByCell.Add(cell, cellClients);
        }

        cellClients.Add(client);
        cellByClient.Add(client, cell);
    }

    public void Remove(ClientConnection client)
    {
        if (!cellByClient.Remove(client, out WorldGridCell cell))
        {
            return;
        }

        HashSet<ClientConnection> cellClients = clientsByCell[cell];
        cellClients.Remove(client);
        if (cellClients.Count == 0)
        {
            clientsByCell.Remove(cell);
        }
    }

    public ClientConnection[] SnapshotCandidates(WorldGridCell centerCell)
    {
        return WorldGrid.GetNeighborCells(centerCell)
            .Where(clientsByCell.ContainsKey)
            .SelectMany(cell => clientsByCell[cell])
            .Distinct()
            .ToArray();
    }

    public void Clear()
    {
        clientsByCell.Clear();
        cellByClient.Clear();
    }
}
