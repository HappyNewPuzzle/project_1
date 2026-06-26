// 접속 중인 클라이언트 목록을 관리합니다.
public sealed class ClientRegistry
{
    // 기본 채팅방 이름입니다.
    public const string DefaultRoomName = "lobby";

    // 접속자 목록을 동시에 읽고 쓸 때 보호하기 위한 lock 객체입니다.
    private readonly object gate = new();

    // 현재 서버에 접속해 있는 클라이언트 목록입니다.
    private readonly List<ClientConnection> clients = new();

    // 현재 클라이언트를 접속자 목록에 추가합니다.
    public int Add(ClientConnection connection)
    {
        // 여러 클라이언트 작업이 동시에 목록을 바꾸지 못하도록 lock으로 보호합니다.
        lock (gate)
        {
            // 접속자 목록에 새 연결을 추가합니다.
            clients.Add(connection);
            // 추가 후 접속자 수를 반환합니다.
            return clients.Count;
        }
    }

    // 현재 클라이언트를 접속자 목록에서 제거합니다.
    public int Remove(ClientConnection connection)
    {
        // 여러 클라이언트 작업이 동시에 목록을 바꾸지 못하도록 lock으로 보호합니다.
        lock (gate)
        {
            // 접속자 목록에서 연결을 제거합니다.
            clients.Remove(connection);
            // 제거 후 접속자 수를 반환합니다.
            return clients.Count;
        }
    }

    // 현재 접속자 수를 가져옵니다.
    public int Count
    {
        get
        {
            // 접속자 목록을 읽는 동안 다른 작업이 목록을 바꾸지 못하도록 lock으로 보호합니다.
            lock (gate)
            {
                // 현재 접속자 목록의 개수를 반환합니다.
                return clients.Count;
            }
        }
    }

    // 현재 접속자 이름 목록을 가져옵니다.
    public string[] GetNames()
    {
        // 접속자 목록을 읽는 동안 다른 작업이 목록을 바꾸지 못하도록 lock으로 보호합니다.
        lock (gate)
        {
            // 현재 접속자 이름만 배열로 복사해서 반환합니다.
            return clients
                .Select(client => client.Name)
                .OrderBy(name => name)
                .ToArray();
        }
    }

    // 현재 존재하는 채팅방 이름 목록을 가져옵니다.
    public string[] GetRoomNames()
    {
        // 접속자 목록을 읽는 동안 다른 작업이 목록을 바꾸지 못하도록 lock으로 보호합니다.
        lock (gate)
        {
            // 현재 접속자들이 속한 방 이름만 중복 없이 정렬해서 반환합니다.
            return clients
                .Select(client => client.RoomName)
                .Append(DefaultRoomName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(roomName => roomName)
                .ToArray();
        }
    }

    // 특정 채팅방에 있는 접속자 이름 목록을 가져옵니다.
    public string[] GetNamesInRoom(string roomName)
    {
        // 접속자 목록을 읽는 동안 다른 작업이 목록을 바꾸지 못하도록 lock으로 보호합니다.
        lock (gate)
        {
            // 같은 방에 있는 접속자 이름만 정렬해서 반환합니다.
            return clients
                .Where(client => string.Equals(client.RoomName, roomName, StringComparison.OrdinalIgnoreCase))
                .Select(client => client.Name)
                .OrderBy(name => name)
                .ToArray();
        }
    }

    // 같은 방 안에서 현재 클라이언트 주변에 있는 접속자 이름 목록을 가져옵니다.
    public string[] GetNearbyNames(ClientConnection center)
    {
        // 접속자 목록을 읽는 동안 다른 작업이 목록을 바꾸지 못하도록 lock으로 보호합니다.
        lock (gate)
        {
            // 같은 방에 있고 시야 거리 안에 있는 다른 클라이언트 이름만 정렬해서 반환합니다.
            return clients
                .Where(client =>
                    client != center &&
                    string.Equals(client.RoomName, center.RoomName, StringComparison.OrdinalIgnoreCase) &&
                    WorldRules.IsNearby(client.Session.Position, center.Session.Position))
                .Select(client => client.Name)
                .OrderBy(name => name)
                .ToArray();
        }
    }

    // 특정 이름을 다른 클라이언트가 이미 사용 중인지 확인합니다.
    public bool IsNameInUse(string name, ClientConnection except)
    {
        // 접속자 목록을 읽는 동안 다른 작업이 목록을 바꾸지 못하도록 lock으로 보호합니다.
        lock (gate)
        {
            // 자기 자신을 제외하고 같은 이름을 쓰는 클라이언트가 있는지 확인합니다.
            return clients.Any(client =>
                client != except &&
                string.Equals(client.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }

    // 이름으로 접속 중인 클라이언트를 찾습니다.
    public ClientConnection? FindByName(string name)
    {
        // 접속자 목록을 읽는 동안 다른 작업이 목록을 바꾸지 못하도록 lock으로 보호합니다.
        lock (gate)
        {
            // 대소문자를 구분하지 않고 이름이 같은 클라이언트를 찾습니다.
            return clients.FirstOrDefault(client =>
                string.Equals(client.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }

    // 현재 접속자 목록의 복사본을 가져옵니다.
    public ClientConnection[] Snapshot(ClientConnection? except = null)
    {
        // 접속자 목록을 복사하는 동안 다른 작업이 목록을 바꾸지 못하도록 lock으로 보호합니다.
        lock (gate)
        {
            // except가 없으면 전체 목록을 복사합니다.
            if (except is null)
            {
                // 현재 접속자 전체를 배열로 반환합니다.
                return clients.ToArray();
            }

            // except로 전달된 클라이언트는 제외하고 복사합니다.
            return clients
                .Where(client => client != except)
                .ToArray();
        }
    }

    // 특정 채팅방에 있는 접속자 목록의 복사본을 가져옵니다.
    public ClientConnection[] SnapshotRoom(string roomName, ClientConnection? except = null)
    {
        // 접속자 목록을 복사하는 동안 다른 작업이 목록을 바꾸지 못하도록 lock으로 보호합니다.
        lock (gate)
        {
            // 같은 방에 있고 제외 대상이 아닌 클라이언트만 복사합니다.
            return clients
                .Where(client =>
                    client != except &&
                    string.Equals(client.RoomName, roomName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
    }

    // 서버가 종료될 때 현재 접속 중인 모든 클라이언트 연결을 분리해 반환합니다.
    public ClientConnection[] Drain()
    {
        // 접속자 목록을 비우는 동안 다른 작업이 목록을 바꾸지 못하도록 lock으로 보호합니다.
        lock (gate)
        {
            // 현재 접속자 목록을 배열로 복사합니다.
            ClientConnection[] snapshot = clients.ToArray();
            // 서버 종료 중에는 목록을 비워서 중복 정리를 줄입니다.
            clients.Clear();
            // 닫을 클라이언트 목록을 반환합니다.
            return snapshot;
        }
    }
}
