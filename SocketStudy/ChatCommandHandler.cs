// slash command 해석과 처리를 담당합니다.
public sealed class ChatCommandHandler
{
    // 방 이름에 허용할 문자 집합입니다.
    // 사용자에게 보여줄 명령 이름 목록입니다.
    private static readonly string[] CommandNames =
    [
        "/help",
        "/commands",
        "/name <nickname>",
        "/rename <nickname>",
        "/whoami",
        "/session",
        "/login <playerId>",
        "/logout",
        "/pos",
        "/map",
        "/warp <mapId> <x> <y>",
        "/move <x> <y>",
        "/nearby",
        "/spawn",
        "/despawn",
        "/users",
        "/rooms",
        "/room-users",
        "/stats",
        "/motd",
        "/version",
        "/join <room>",
        "/leave",
        "/where",
        "/ping",
        "/echo <message>",
        "/time",
        "/uptime",
        "/me <action>",
        "/whisper <nickname> <message>",
        "/quit"
    ];

    // 사용자에게 보여줄 명령 목록입니다.
    private static readonly string CommandList = $"Commands: {string.Join(", ", CommandNames)}";

    // /echo 명령 사용법입니다.
    private const string EchoUsage = "Usage: /echo <message>";

    // /me 명령 사용법입니다.
    private const string MeUsage = "Usage: /me <action>";

    // /whisper 명령 사용법입니다.
    private const string WhisperUsage = "Usage: /whisper <nickname> <message>";

    // /join 명령 사용법입니다.
    private const string JoinUsage = "Usage: /join <room>";

    // /name 명령 사용법입니다.
    private const string NameUsage = "Usage: /name <nickname>";

    // /rename 명령 사용법입니다.
    private const string RenameUsage = "Usage: /rename <nickname>";

    // /login 명령 사용법입니다.
    private const string LoginUsage = "Usage: /login <playerId>";

    // /move 명령 사용법입니다.
    private const string MoveUsage = "Usage: /move <x> <y>";

    // /warp 명령 사용법입니다.
    private const string WarpUsage = "Usage: /warp <mapId> <x> <y>";

    // 클라이언트 한 명에게 메시지를 보내는 함수입니다.
    private readonly Func<ClientConnection, MessageType, string, Task> sendToClientAsync;

    // 전체 클라이언트에게 서버 공지를 보내는 함수입니다.
    private readonly Func<string, Task> broadcastNoticeAsync;

    // 전체 클라이언트에게 채팅 메시지를 보내는 함수입니다.
    private readonly Func<ClientConnection, string, Task> broadcastChatAsync;

    // 주변 클라이언트에게 공지 메시지를 보내는 함수입니다.
    private readonly Func<ClientConnection, string, Task> broadcastNearbyNoticeAsync;

    // 현재 접속자 이름 목록을 가져오는 함수입니다.
    private readonly Func<string[]> getClientNames;

    // 현재 채팅방 이름 목록을 가져오는 함수입니다.
    private readonly Func<string[]> getRoomNames;

    // 특정 채팅방의 접속자 이름 목록을 가져오는 함수입니다.
    private readonly Func<string, string[]> getClientNamesInRoom;

    // 현재 클라이언트 주변의 접속자 이름 목록을 가져오는 함수입니다.
    private readonly Func<ClientConnection, string[]> getNearbyNames;

    // 특정 이름을 다른 클라이언트가 이미 사용 중인지 확인하는 함수입니다.
    private readonly Func<string, ClientConnection, bool> isNameInUse;

    // 이름으로 클라이언트를 찾는 함수입니다.
    private readonly Func<string, ClientConnection?> findClientByName;

    // 클라이언트를 다른 채팅방으로 이동시키는 함수입니다.
    private readonly Func<ClientConnection, string, Task> moveClientToRoomAsync;

    // 현재 서버 시간을 가져오는 함수입니다.
    private readonly Func<DateTimeOffset> getCurrentTime;

    // 서버가 시작된 시각을 가져오는 함수입니다.
    private readonly Func<DateTimeOffset> getServerStartedAt;

    // 명령 처리에 필요한 서버 기능을 주입받습니다.
    public ChatCommandHandler(
        Func<ClientConnection, MessageType, string, Task> sendToClientAsync,
        Func<string, Task> broadcastNoticeAsync,
        Func<ClientConnection, string, Task> broadcastChatAsync,
        Func<ClientConnection, string, Task> broadcastNearbyNoticeAsync,
        Func<string[]> getClientNames,
        Func<string[]> getRoomNames,
        Func<string, string[]> getClientNamesInRoom,
        Func<ClientConnection, string[]> getNearbyNames,
        Func<string, ClientConnection, bool> isNameInUse,
        Func<string, ClientConnection?> findClientByName,
        Func<ClientConnection, string, Task> moveClientToRoomAsync,
        Func<DateTimeOffset> getCurrentTime,
        Func<DateTimeOffset> getServerStartedAt)
    {
        // 클라이언트 개별 전송 함수를 저장합니다.
        this.sendToClientAsync = sendToClientAsync;
        // 전체 공지 함수를 저장합니다.
        this.broadcastNoticeAsync = broadcastNoticeAsync;
        // 전체 채팅 함수를 저장합니다.
        this.broadcastChatAsync = broadcastChatAsync;
        // 주변 공지 함수를 저장합니다.
        this.broadcastNearbyNoticeAsync = broadcastNearbyNoticeAsync;
        // 접속자 이름 조회 함수를 저장합니다.
        this.getClientNames = getClientNames;
        // 채팅방 이름 조회 함수를 저장합니다.
        this.getRoomNames = getRoomNames;
        // 방별 접속자 이름 조회 함수를 저장합니다.
        this.getClientNamesInRoom = getClientNamesInRoom;
        // 주변 접속자 이름 조회 함수를 저장합니다.
        this.getNearbyNames = getNearbyNames;
        // 이름 중복 확인 함수를 저장합니다.
        this.isNameInUse = isNameInUse;
        // 이름 기반 클라이언트 조회 함수를 저장합니다.
        this.findClientByName = findClientByName;
        // 채팅방 이동 함수를 저장합니다.
        this.moveClientToRoomAsync = moveClientToRoomAsync;
        // 현재 시간 조회 함수를 저장합니다.
        this.getCurrentTime = getCurrentTime;
        // 서버 시작 시각 조회 함수를 저장합니다.
        this.getServerStartedAt = getServerStartedAt;
    }

    // 서버에서 처리해야 하는 slash command인지 확인하고 처리합니다.
    public async Task<bool> TryHandleAsync(ClientConnection connection, NetworkMessage message)
    {
        // command 타입이 아니고 slash로 시작하지 않으면 일반 채팅 메시지입니다.
        if (message.Type != MessageType.Command && !message.Text.StartsWith('/'))
        {
            // 명령이 아니라고 호출자에게 알려줍니다.
            return false;
        }

        // /name 명령은 클라이언트의 표시 이름을 바꿉니다.
        if (message.Text.StartsWith("/name ", StringComparison.OrdinalIgnoreCase))
        {
            // 명령 뒤쪽의 닉네임 부분만 잘라냅니다.
            string requestedName = message.Text["/name ".Length..].Trim();
            // 닉네임 변경을 처리합니다.
            await ChangeClientNameAsync(connection, requestedName);
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /name 명령에 닉네임이 빠지면 사용법을 안내합니다.
        if (await SendUsageIfExactCommandAsync(connection, message.Text, "/name", NameUsage))
        {
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /rename 명령은 /name과 같은 닉네임 변경 별칭입니다.
        if (message.Text.StartsWith("/rename ", StringComparison.OrdinalIgnoreCase))
        {
            // 명령 뒤쪽의 닉네임 부분만 잘라냅니다.
            string requestedName = message.Text["/rename ".Length..].Trim();
            // 닉네임 변경을 처리합니다.
            await ChangeClientNameAsync(connection, requestedName);
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /rename 명령에 닉네임이 빠지면 사용법을 안내합니다.
        if (await SendUsageIfExactCommandAsync(connection, message.Text, "/rename", RenameUsage))
        {
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /help 명령은 사용할 수 있는 명령 목록을 보여줍니다.
        if (message.Text.Equals("/help", StringComparison.OrdinalIgnoreCase) ||
            message.Text.Equals("/commands", StringComparison.OrdinalIgnoreCase))
        {
            // 보낸 사람에게만 명령 목록을 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, CommandList);
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /users 명령은 현재 접속 중인 클라이언트 이름 목록을 보여줍니다.
        if (message.Text.Equals("/users", StringComparison.OrdinalIgnoreCase))
        {
            // 현재 접속자 이름 목록을 가져옵니다.
            string[] names = getClientNames();
            // 접속자 목록을 보낸 사람에게만 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, $"Online users ({names.Length}): {string.Join(", ", names)}");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /whoami 명령은 내 닉네임과 현재 방을 보여줍니다.
        if (message.Text.Equals("/whoami", StringComparison.OrdinalIgnoreCase))
        {
            // 보낸 사람에게만 현재 연결 상태를 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, $"You are {connection.Name} in room {connection.RoomName}.");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /session 명령은 MMO 확장을 위한 플레이어 세션 상태를 보여줍니다.
        if (message.Text.Equals("/session", StringComparison.OrdinalIgnoreCase))
        {
            // 인증 상태를 읽기 쉬운 문자열로 바꿉니다.
            string authState = connection.Session.IsAuthenticated ? "authenticated" : "anonymous";
            // 스폰 상태를 읽기 쉬운 문자열로 바꿉니다.
            string spawnState = connection.Session.IsSpawned ? "spawned" : "not-spawned";
            // 보낸 사람에게만 세션 상태를 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, $"Session: player-id={connection.Session.PlayerId}, state={authState}, spawn={spawnState}");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /login 명령은 학습용으로 플레이어 ID를 세션에 연결합니다.
        if (message.Text.StartsWith("/login ", StringComparison.OrdinalIgnoreCase))
        {
            // 월드에 스폰된 세션은 플레이어 정체성을 바꿀 수 없습니다.
            if (connection.Session.IsSpawned)
            {
                // 보낸 사람에게만 스폰 중에는 로그인할 수 없다고 알려줍니다.
                await sendToClientAsync(connection, MessageType.Notice, "You cannot login while spawned.");
                // 명령을 처리했다고 호출자에게 알려줍니다.
                return true;
            }

            // 이미 로그인한 세션은 다른 플레이어 ID로 다시 로그인할 수 없습니다.
            if (connection.Session.IsAuthenticated)
            {
                // 보낸 사람에게만 현재 로그인된 플레이어 ID를 알려줍니다.
                await sendToClientAsync(connection, MessageType.Notice, $"You are already logged in as player {connection.Session.PlayerId}.");
                // 명령을 처리했다고 호출자에게 알려줍니다.
                return true;
            }

            // 명령 뒤쪽의 플레이어 ID 부분만 잘라냅니다.
            string rawPlayerId = message.Text["/login ".Length..].Trim();
            // 숫자 ID인지 확인합니다.
            if (!long.TryParse(rawPlayerId, out long playerId) || playerId <= PlayerSession.AnonymousPlayerId)
            {
                // 보낸 사람에게만 실패 이유를 알려줍니다.
                await sendToClientAsync(connection, MessageType.Notice, "Player id must be a positive number.");
                // 명령을 처리했다고 호출자에게 알려줍니다.
                return true;
            }

            // 세션에 플레이어 ID를 연결합니다.
            connection.Session.Authenticate(playerId);
            // 보낸 사람에게만 로그인 상태를 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, $"Logged in as player {playerId}.");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /login 명령에 플레이어 ID가 빠지면 사용법을 안내합니다.
        if (await SendUsageIfExactCommandAsync(connection, message.Text, "/login", LoginUsage))
        {
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /logout 명령은 인증 정보를 제거하고 익명 세션으로 돌아갑니다.
        if (message.Text.Equals("/logout", StringComparison.OrdinalIgnoreCase))
        {
            // 월드에 스폰된 세션은 먼저 despawn해야 합니다.
            if (connection.Session.IsSpawned)
            {
                // 보낸 사람에게만 despawn이 먼저 필요하다고 알려줍니다.
                await sendToClientAsync(connection, MessageType.Notice, "You must despawn before logging out.");
                // 명령을 처리했다고 호출자에게 알려줍니다.
                return true;
            }

            // 로그인하지 않은 세션은 로그아웃할 인증 정보가 없습니다.
            if (!connection.Session.IsAuthenticated)
            {
                // 보낸 사람에게만 현재 로그인 상태가 아니라고 알려줍니다.
                await sendToClientAsync(connection, MessageType.Notice, "You are not logged in.");
                // 명령을 처리했다고 호출자에게 알려줍니다.
                return true;
            }

            // 인증 정보와 학습용 월드 위치를 초기화합니다.
            connection.Session.Logout();
            // 보낸 사람에게만 로그아웃 완료를 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, "Logged out.");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /pos 명령은 플레이어 세션의 현재 월드 위치를 보여줍니다.
        if (message.Text.Equals("/pos", StringComparison.OrdinalIgnoreCase))
        {
            // 보낸 사람에게만 현재 위치를 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, $"Position: {connection.Session.Position}");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /map 명령은 플레이어 세션의 현재 게임 맵 ID를 보여줍니다.
        if (message.Text.Equals("/map", StringComparison.OrdinalIgnoreCase))
        {
            // 보낸 사람에게만 현재 맵 ID를 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, $"Map: {connection.Session.MapId}");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /warp 명령은 스폰된 플레이어를 다른 맵과 위치로 전환합니다.
        if (message.Text.StartsWith("/warp ", StringComparison.OrdinalIgnoreCase))
        {
            // 로그인하지 않은 세션은 게임 맵을 이동할 수 없습니다.
            if (!connection.Session.IsAuthenticated)
            {
                // 보낸 사람에게만 로그인이 필요하다고 알려줍니다.
                await sendToClientAsync(connection, MessageType.Notice, "You must login before warping.");
                // 명령을 처리했다고 호출자에게 알려줍니다.
                return true;
            }

            // 월드에 스폰되지 않은 세션은 맵 전환을 시작할 수 없습니다.
            if (!connection.Session.IsSpawned)
            {
                // 보낸 사람에게만 스폰이 필요하다고 알려줍니다.
                await sendToClientAsync(connection, MessageType.Notice, "You must spawn before warping.");
                // 명령을 처리했다고 호출자에게 알려줍니다.
                return true;
            }

            // 명령 뒤쪽의 맵 ID와 좌표 두 개를 공백 기준으로 나눕니다.
            string[] parts = message.Text["/warp ".Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // mapId, x, y 세 값이 모두 정수인지 확인합니다.
            if (parts.Length != 3 ||
                !int.TryParse(parts[0], out int mapId) ||
                !int.TryParse(parts[1], out int x) ||
                !int.TryParse(parts[2], out int y))
            {
                // 보낸 사람에게만 사용법을 알려줍니다.
                await sendToClientAsync(connection, MessageType.Notice, WarpUsage);
                // 명령을 처리했다고 호출자에게 알려줍니다.
                return true;
            }

            // 맵 ID는 양수만 허용합니다.
            if (mapId <= 0)
            {
                // 보낸 사람에게만 실패 이유를 알려줍니다.
                await sendToClientAsync(connection, MessageType.Notice, "Map id must be positive.");
                // 명령을 처리했다고 호출자에게 알려줍니다.
                return true;
            }

            // 서버가 허용하는 월드 경계 안의 위치인지 확인합니다.
            var nextPosition = new WorldPosition(x, y);
            if (!WorldRules.IsInsideWorld(nextPosition))
            {
                // 보낸 사람에게만 실패 이유를 알려줍니다.
                await sendToClientAsync(connection, MessageType.Notice, $"Position must be between {WorldRules.MinCoordinate} and {WorldRules.MaxCoordinate}.");
                // 명령을 처리했다고 호출자에게 알려줍니다.
                return true;
            }

            // 기존 맵의 주변 플레이어에게 퇴장을 먼저 알립니다.
            await broadcastNearbyNoticeAsync(connection, $"{connection.Name} left map {connection.Session.MapId} from {connection.Session.Position}");
            // 기존 맵 AOI에서 플레이어를 제거합니다.
            connection.Session.Despawn();
            // despawn 상태에서 목적지 맵으로 변경합니다.
            connection.Session.ChangeMap(mapId);
            // 목적지 좌표로 위치를 변경합니다.
            connection.Session.MoveTo(nextPosition);
            // 새 맵 AOI에 플레이어를 추가합니다.
            connection.Session.Spawn();
            // 새 맵의 주변 플레이어에게 입장을 알립니다.
            await broadcastNearbyNoticeAsync(connection, $"{connection.Name} entered map {connection.Session.MapId} at {connection.Session.Position}");
            // 보낸 사람에게만 맵 전환 결과를 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, $"Warped to map={connection.Session.MapId}, {connection.Session.Position}");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /warp 명령에 인자가 빠지면 사용법을 안내합니다.
        if (await SendUsageIfExactCommandAsync(connection, message.Text, "/warp", WarpUsage))
        {
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /move 명령은 학습용으로 플레이어 위치를 변경합니다.
        if (message.Text.StartsWith("/move ", StringComparison.OrdinalIgnoreCase))
        {
            // 월드에 스폰되지 않은 세션은 위치를 변경할 수 없습니다.
            if (!connection.Session.IsSpawned)
            {
                // 보낸 사람에게만 이동이 거부된 이유를 알려줍니다.
                await sendToClientAsync(connection, MessageType.Notice, "You must spawn before moving.");
                // 명령을 처리했다고 호출자에게 알려줍니다.
                return true;
            }

            // 명령 뒤쪽의 좌표 두 개를 공백 기준으로 나눕니다.
            string[] parts = message.Text["/move ".Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // x, y 두 값이 있고 둘 다 정수인지 확인합니다.
            if (parts.Length != 2 || !int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y))
            {
                // 보낸 사람에게만 사용법을 알려줍니다.
                await sendToClientAsync(connection, MessageType.Notice, MoveUsage);
                // 명령을 처리했다고 호출자에게 알려줍니다.
                return true;
            }

            // 서버가 허용하는 월드 경계 안의 위치인지 확인합니다.
            var nextPosition = new WorldPosition(x, y);
            if (!WorldRules.IsInsideWorld(nextPosition))
            {
                // 보낸 사람에게만 실패 이유를 알려줍니다.
                await sendToClientAsync(connection, MessageType.Notice, $"Position must be between {WorldRules.MinCoordinate} and {WorldRules.MaxCoordinate}.");
                // 명령을 처리했다고 호출자에게 알려줍니다.
                return true;
            }

            // 세션 위치를 변경합니다.
            connection.Session.MoveTo(nextPosition);
            // 주변 플레이어에게 이동을 알립니다.
            await broadcastNearbyNoticeAsync(connection, $"{connection.Name} moved to {connection.Session.Position}");
            // 보낸 사람에게만 새 위치를 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, $"Moved to {connection.Session.Position}");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /move 명령에 좌표가 빠지면 사용법을 안내합니다.
        if (await SendUsageIfExactCommandAsync(connection, message.Text, "/move", MoveUsage))
        {
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /nearby 명령은 같은 게임 맵에서 시야 거리 안의 플레이어를 보여줍니다.
        if (message.Text.Equals("/nearby", StringComparison.OrdinalIgnoreCase))
        {
            // 월드에 스폰되지 않은 세션은 주변 플레이어를 탐색할 수 없습니다.
            if (!connection.Session.IsSpawned)
            {
                // 보낸 사람에게만 탐색이 거부된 이유를 알려줍니다.
                await sendToClientAsync(connection, MessageType.Notice, "You must spawn before checking nearby players.");
                // 명령을 처리했다고 호출자에게 알려줍니다.
                return true;
            }

            // 현재 클라이언트 주변의 이름 목록을 가져옵니다.
            string[] nearbyNames = getNearbyNames(connection);
            // 아무도 없으면 읽기 쉬운 표시를 사용합니다.
            string displayNames = nearbyNames.Length == 0 ? "(none)" : string.Join(", ", nearbyNames);
            // 보낸 사람에게만 주변 플레이어 목록을 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, $"Nearby players ({nearbyNames.Length}): {displayNames}");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /spawn 명령은 현재 위치에 플레이어가 나타났다는 사실을 주변 플레이어에게 알립니다.
        if (message.Text.Equals("/spawn", StringComparison.OrdinalIgnoreCase))
        {
            // 로그인하지 않은 세션은 월드에 스폰할 수 없습니다.
            if (!connection.Session.IsAuthenticated)
            {
                // 보낸 사람에게만 로그인이 필요하다고 알려줍니다.
                await sendToClientAsync(connection, MessageType.Notice, "You must login before spawning.");
                // 명령을 처리했다고 호출자에게 알려줍니다.
                return true;
            }

            // 이미 스폰된 세션은 같은 상태 전이를 반복할 수 없습니다.
            if (connection.Session.IsSpawned)
            {
                // 보낸 사람에게만 이미 스폰된 상태라고 알려줍니다.
                await sendToClientAsync(connection, MessageType.Notice, "You are already spawned.");
                // 명령을 처리했다고 호출자에게 알려줍니다.
                return true;
            }

            // 현재 세션을 스폰 상태로 변경합니다.
            connection.Session.Spawn();
            // 주변 플레이어에게 스폰 알림을 보냅니다.
            await broadcastNearbyNoticeAsync(connection, $"{connection.Name} spawned at {connection.Session.Position}");
            // 보낸 사람에게만 현재 스폰 위치를 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, $"Spawned at {connection.Session.Position}");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /despawn 명령은 현재 플레이어가 월드에서 사라졌다는 사실을 주변 플레이어에게 알립니다.
        if (message.Text.Equals("/despawn", StringComparison.OrdinalIgnoreCase))
        {
            // 이미 월드에 나타나 있지 않다면 상태를 바꾸지 않습니다.
            if (!connection.Session.IsSpawned)
            {
                // 보낸 사람에게만 현재 상태를 알려줍니다.
                await sendToClientAsync(connection, MessageType.Notice, "You are not spawned.");
                // 명령을 처리했다고 호출자에게 알려줍니다.
                return true;
            }

            // 주변 알림에 사용할 사라지기 전 위치를 보관합니다.
            WorldPosition despawnPosition = connection.Session.Position;
            // 현재 세션을 despawn 상태로 변경합니다.
            connection.Session.Despawn();
            // 주변 플레이어에게 despawn 알림을 보냅니다.
            await broadcastNearbyNoticeAsync(connection, $"{connection.Name} despawned from {despawnPosition}");
            // 보낸 사람에게만 despawn 완료 위치를 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, $"Despawned from {despawnPosition}");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /rooms 명령은 현재 존재하는 채팅방 목록을 보여줍니다.
        if (message.Text.Equals("/rooms", StringComparison.OrdinalIgnoreCase))
        {
            // 현재 방 이름 목록을 가져옵니다.
            string[] roomNames = getRoomNames();
            // 방 목록을 보낸 사람에게만 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, $"Rooms ({roomNames.Length}): {string.Join(", ", roomNames)}");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /room-users 명령은 현재 방에 있는 클라이언트 이름 목록을 보여줍니다.
        if (message.Text.Equals("/room-users", StringComparison.OrdinalIgnoreCase))
        {
            // 현재 방의 접속자 이름 목록을 가져옵니다.
            string[] names = getClientNamesInRoom(connection.RoomName);
            // 방 접속자 목록을 보낸 사람에게만 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, $"Users in {connection.RoomName} ({names.Length}): {string.Join(", ", names)}");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /stats 명령은 서버와 현재 방의 간단한 상태를 보여줍니다.
        if (message.Text.Equals("/stats", StringComparison.OrdinalIgnoreCase))
        {
            // 전체 접속자 수를 가져옵니다.
            int userCount = getClientNames().Length;
            // 현재 방 수를 가져옵니다.
            int roomCount = getRoomNames().Length;
            // 현재 방의 접속자 수를 가져옵니다.
            int currentRoomUserCount = getClientNamesInRoom(connection.RoomName).Length;
            // 요약 정보를 보낸 사람에게만 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, $"Stats: users={userCount}, rooms={roomCount}, current-room-users={currentRoomUserCount}");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /motd 명령은 서버 안내 메시지를 보여줍니다.
        if (message.Text.Equals("/motd", StringComparison.OrdinalIgnoreCase))
        {
            // 보낸 사람에게만 서버 안내 메시지를 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, ServerInfo.MessageOfTheDay);
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /version 명령은 서버 예제 버전을 보여줍니다.
        if (message.Text.Equals("/version", StringComparison.OrdinalIgnoreCase))
        {
            // 보낸 사람에게만 서버 버전을 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, ServerInfo.VersionMessage);
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /join 명령은 클라이언트를 다른 채팅방으로 이동시킵니다.
        if (message.Text.StartsWith("/join ", StringComparison.OrdinalIgnoreCase))
        {
            // 명령 뒤쪽의 방 이름만 잘라냅니다.
            string roomName = message.Text["/join ".Length..].Trim();
            // 채팅방 이동을 처리합니다.
            await JoinRoomAsync(connection, roomName);
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /join 명령에 방 이름이 빠지면 사용법을 안내합니다.
        if (await SendUsageIfExactCommandAsync(connection, message.Text, "/join", JoinUsage))
        {
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /leave 명령은 기본 방인 lobby로 돌아갑니다.
        if (message.Text.Equals("/leave", StringComparison.OrdinalIgnoreCase))
        {
            // lobby로 이동하는 동작은 /join과 같은 메서드를 재사용합니다.
            await JoinRoomAsync(connection, ClientRegistry.DefaultRoomName);
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /where 명령은 현재 내가 속한 채팅방을 보여줍니다.
        if (message.Text.Equals("/where", StringComparison.OrdinalIgnoreCase))
        {
            // 보낸 사람에게만 현재 방 이름을 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, $"Current room: {connection.RoomName}");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /ping 명령은 서버가 응답 가능한 상태인지 확인합니다.
        if (message.Text.Equals("/ping", StringComparison.OrdinalIgnoreCase))
        {
            // 보낸 사람에게만 짧은 응답을 돌려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, "pong");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /echo 명령은 서버가 받은 문장을 그대로 돌려줍니다.
        if (message.Text.StartsWith("/echo ", StringComparison.OrdinalIgnoreCase))
        {
            // 명령 뒤쪽의 본문만 잘라냅니다.
            string echoText = message.Text["/echo ".Length..].Trim();
            // 본문이 비어 있으면 사용법을 안내합니다.
            if (string.IsNullOrWhiteSpace(echoText))
            {
                // 보낸 사람에게만 사용법을 알려줍니다.
                await sendToClientAsync(connection, MessageType.Notice, EchoUsage);
                // 명령을 처리했다고 호출자에게 알려줍니다.
                return true;
            }

            // 보낸 사람에게만 echo 응답을 돌려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, $"echo: {echoText}");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /echo 명령에 본문이 빠지면 사용법을 안내합니다.
        if (await SendUsageIfExactCommandAsync(connection, message.Text, "/echo", EchoUsage))
        {
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /time 명령은 서버의 현재 시간을 보여줍니다.
        if (message.Text.Equals("/time", StringComparison.OrdinalIgnoreCase))
        {
            // 서버 시간을 ISO-8601 형식으로 만들어 보낸 사람에게만 알려줍니다.
            string serverTime = getCurrentTime().ToString("yyyy-MM-dd HH:mm:ss zzz");
            // 서버 시간을 notice 메시지로 전송합니다.
            await sendToClientAsync(connection, MessageType.Notice, $"Server time: {serverTime}");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /uptime 명령은 서버가 켜져 있었던 시간을 보여줍니다.
        if (message.Text.Equals("/uptime", StringComparison.OrdinalIgnoreCase))
        {
            // 현재 시간과 서버 시작 시간의 차이를 계산합니다.
            TimeSpan uptime = getCurrentTime() - getServerStartedAt();
            // 보기 쉬운 문자열로 바꾸어 보낸 사람에게만 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, $"Server uptime: {FormatUptime(uptime)}");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /me 명령은 행동 메시지를 전체 채팅으로 보냅니다.
        if (message.Text.StartsWith("/me ", StringComparison.OrdinalIgnoreCase))
        {
            // 행동 메시지 본문만 잘라냅니다.
            string action = message.Text["/me ".Length..].Trim();
            // 행동이 비어 있으면 사용법을 안내합니다.
            if (string.IsNullOrWhiteSpace(action))
            {
                // 보낸 사람에게만 사용법을 알려줍니다.
                await sendToClientAsync(connection, MessageType.Notice, MeUsage);
                // 명령을 처리했다고 호출자에게 알려줍니다.
                return true;
            }

            // 전체 클라이언트에게 행동 메시지를 보냅니다.
            await broadcastChatAsync(connection, $"* {connection.Name} {action}");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /me 명령에 행동 본문이 빠지면 사용법을 안내합니다.
        if (await SendUsageIfExactCommandAsync(connection, message.Text, "/me", MeUsage))
        {
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /whisper 명령은 특정 클라이언트에게만 메시지를 보냅니다.
        if (message.Text.StartsWith("/whisper ", StringComparison.OrdinalIgnoreCase))
        {
            // 개인 메시지 명령을 처리합니다.
            await SendWhisperAsync(connection, message.Text);
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /whisper 명령에 대상이나 본문이 빠지면 사용법을 안내합니다.
        if (await SendUsageIfExactCommandAsync(connection, message.Text, "/whisper", WhisperUsage))
        {
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /quit 명령은 클라이언트가 서버에 연결 종료 의사를 명확히 전달하는 명령입니다.
        if (message.Text.Equals("/quit", StringComparison.OrdinalIgnoreCase))
        {
            // 보낸 사람에게 연결을 종료한다는 안내를 보냅니다.
            await sendToClientAsync(connection, MessageType.Notice, "Goodbye.");
            // 소켓을 닫아서 해당 클라이언트 처리 루프가 끝나도록 만듭니다.
            connection.Close();
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // 알 수 없는 명령은 보낸 사람에게만 안내합니다.
        await sendToClientAsync(connection, MessageType.Notice, $"Unknown command: {message.Text}");
        // 명령을 처리했다고 호출자에게 알려줍니다.
        return true;
    }

    // 클라이언트를 다른 채팅방으로 이동시킵니다.
    private async Task JoinRoomAsync(ClientConnection connection, string roomName)
    {
        // 방 이름이 비어 있으면 이동하지 않습니다.
        if (string.IsNullOrWhiteSpace(roomName))
        {
            // 보낸 사람에게만 실패 이유를 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, "Room name cannot be empty.");
            // 메서드를 종료합니다.
            return;
        }

        // 너무 긴 방 이름은 화면을 어지럽히므로 20자로 제한합니다.
        if (roomName.Length > NameRules.MaxNameLength)
        {
            // 보낸 사람에게만 실패 이유를 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, $"Room name must be {NameRules.MaxNameLength} characters or fewer.");
            // 메서드를 종료합니다.
            return;
        }

        // 방 이름은 명령 파싱이 쉬운 문자만 허용합니다.
        if (!IsValidRoomName(roomName))
        {
            // 보낸 사람에게만 실패 이유를 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, NameRules.RoomNameCharacterRuleMessage);
            // 메서드를 종료합니다.
            return;
        }

        // 서버에게 실제 이동을 요청합니다.
        await moveClientToRoomAsync(connection, roomName);
    }

    // 방 이름이 허용된 문자로만 이루어졌는지 확인합니다.
    private static bool IsValidRoomName(string roomName)
    {
        // 모든 문자가 허용된 문자 집합 안에 있는지 확인합니다.
        return NameRules.HasOnlyAllowedCharacters(roomName);
    }

    // 인자 없이 명령만 입력된 경우 사용법을 보냅니다.
    private async Task<bool> SendUsageIfExactCommandAsync(
        ClientConnection connection,
        string text,
        string command,
        string usage)
    {
        // 정확히 해당 명령만 입력된 경우가 아니면 처리하지 않습니다.
        if (!text.Equals(command, StringComparison.OrdinalIgnoreCase))
        {
            // 호출자가 다음 명령 처리를 계속하도록 false를 반환합니다.
            return false;
        }

        // 보낸 사람에게만 사용법을 알려줍니다.
        await sendToClientAsync(connection, MessageType.Notice, usage);
        // 명령을 처리했다고 호출자에게 알려줍니다.
        return true;
    }

    // 서버 실행 시간을 화면에 보여주기 좋은 문자열로 바꿉니다.
    private static string FormatUptime(TimeSpan uptime)
    {
        // 시계 차이 등으로 음수가 나오면 0초로 보정합니다.
        if (uptime < TimeSpan.Zero)
        {
            uptime = TimeSpan.Zero;
        }

        // 하루 이상이면 일 단위를 앞에 붙입니다.
        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays}d {uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
        }

        // 하루 미만이면 HH:mm:ss 형식으로 보여줍니다.
        return $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
    }

    // 특정 클라이언트에게만 개인 메시지를 보냅니다.
    private async Task SendWhisperAsync(ClientConnection sender, string commandText)
    {
        // "/whisper " 뒤쪽의 대상 이름과 메시지 본문을 가져옵니다.
        string payload = commandText["/whisper ".Length..].Trim();
        // 첫 번째 공백을 기준으로 대상 닉네임과 본문을 나눕니다.
        int separatorIndex = payload.IndexOf(' ');
        // 대상 또는 본문이 빠졌으면 사용법을 안내합니다.
        if (separatorIndex <= 0 || separatorIndex == payload.Length - 1)
        {
            // 보낸 사람에게만 사용법을 알려줍니다.
            await sendToClientAsync(sender, MessageType.Notice, WhisperUsage);
            // 메서드를 종료합니다.
            return;
        }

        // 개인 메시지를 받을 대상 닉네임입니다.
        string targetName = payload[..separatorIndex].Trim();
        // 개인 메시지 본문입니다.
        string whisperText = payload[(separatorIndex + 1)..].Trim();
        // 대상 닉네임으로 현재 접속자를 찾습니다.
        ClientConnection? target = findClientByName(targetName);
        // 대상이 없으면 보낸 사람에게 실패를 알려줍니다.
        if (target is null)
        {
            // 대상 사용자를 찾지 못했다는 안내를 보냅니다.
            await sendToClientAsync(sender, MessageType.Notice, $"User not found: {targetName}");
            // 메서드를 종료합니다.
            return;
        }

        // 대상 클라이언트에게 개인 메시지를 보냅니다.
        await sendToClientAsync(target, MessageType.Notice, $"whisper from {sender.Name}: {whisperText}");
        // 보낸 사람에게도 전송 완료를 보여줍니다.
        await sendToClientAsync(sender, MessageType.Notice, $"whisper to {target.Name}: {whisperText}");
    }

    // 클라이언트 닉네임을 변경합니다.
    private async Task ChangeClientNameAsync(ClientConnection connection, string requestedName)
    {
        // 닉네임이 비어 있으면 변경하지 않습니다.
        if (string.IsNullOrWhiteSpace(requestedName))
        {
            // 보낸 사람에게만 실패 이유를 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, "Nickname cannot be empty.");
            // 메서드를 종료합니다.
            return;
        }

        // 너무 긴 닉네임은 화면을 어지럽히므로 20자로 제한합니다.
        if (requestedName.Length > NameRules.MaxNameLength)
        {
            // 보낸 사람에게만 실패 이유를 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, $"Nickname must be {NameRules.MaxNameLength} characters or fewer.");
            // 메서드를 종료합니다.
            return;
        }

        // 닉네임은 명령 파싱이 쉬운 문자만 허용합니다.
        if (!NameRules.HasOnlyAllowedCharacters(requestedName))
        {
            // 보낸 사람에게만 실패 이유를 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, NameRules.NicknameCharacterRuleMessage);
            // 메서드를 종료합니다.
            return;
        }

        // 이미 다른 클라이언트가 쓰고 있는 닉네임이면 변경하지 않습니다.
        if (isNameInUse(requestedName, connection))
        {
            // 보낸 사람에게만 실패 이유를 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, $"Nickname is already in use: {requestedName}");
            // 메서드를 종료합니다.
            return;
        }

        // 이전 이름을 공지에 쓰기 위해 보관합니다.
        string oldName = connection.Name;
        // 연결 객체의 이름을 새 닉네임으로 바꿉니다.
        connection.Rename(requestedName);
        // 서버 콘솔에 닉네임 변경을 기록합니다.
        AppLogger.Info($"[server] {oldName} is now {connection.Name}");
        // 모든 클라이언트에게 닉네임 변경을 공지합니다.
        await broadcastNoticeAsync($"{oldName} is now {connection.Name}");
    }
}
