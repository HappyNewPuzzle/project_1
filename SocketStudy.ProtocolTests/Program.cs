using System.Net;
using System.Net.Sockets;

await RunProtocolRoundTripTestAsync(MessageType.Chat, "alice: hello");
await RunProtocolRoundTripTestAsync(MessageType.Notice, "Welcome.");
await RunProtocolRoundTripTestAsync(MessageType.Command, "/users");
await RunProtocolRoundTripTestAsync(MessageType.Chat, "");
await RunProtocolRoundTripTestAsync(MessageType.Chat, "한글 메시지와 emoji 🙂");
await RunInvalidMessageTypeTestAsync();
await RunIncompleteBodyTestAsync();
await RunTooLargeLengthTestAsync();
RunMessageSizeLimitTest();
RunNameRulesTest();
RunServerInfoTest();
RunPlayerSessionTest();
await RunPlayerEntityTestAsync();
RunWorldEventTest();
RunMovementTickProcessorTest();
RunMovementRequestQueueTest();
RunWorldRulesTest();
RunWorldGridTest();
RunServerPortParseTest();
RunLocalClientOptionParseTest();
RunRemoteClientOptionParseTest();
RunInvalidClientNicknameOptionParseTest();
await RunClientRegistryTracksCountAndNamesAsync();
await RunClientRegistryFindsNamesCaseInsensitiveAsync();
RunClientRegistryIncludesDefaultRoom();
await RunClientRegistryFiltersRoomsAsync();
await RunClientRegistrySnapshotsRoomsCaseInsensitiveAsync();
await RunClientRegistryFindsSpawnedPlayersByMapAsync();
await RunClientRegistryFindsNearbyNamesAsync();
await RunClientRegistryFindsNearbySnapshotsAsync();
await RunClientRegistryLimitsNearbySnapshotsAsync();
await RunClientRegistryDrainsConnectionsAsync();
await RunHelpCommandTestAsync();
await RunCommandsAliasTestAsync();
await RunWhereCommandTestAsync();
await RunPingCommandTestAsync();
await RunEchoCommandTestAsync();
await RunEmptyEchoCommandTestAsync();
await RunMissingEchoMessageCommandTestAsync();
await RunTimeCommandTestAsync();
await RunUptimeCommandTestAsync();
await RunWhoAmICommandTestAsync();
await RunSessionCommandTestAsync();
await RunLoginCommandTestAsync();
await RunDuplicateLoginCommandTestAsync();
await RunLoginWhileSpawnedCommandTestAsync();
await RunAuthenticatedSessionCommandTestAsync();
await RunInvalidLoginCommandTestAsync();
await RunMissingLoginCommandTestAsync();
await RunLogoutCommandTestAsync();
await RunLogoutWhileSpawnedCommandTestAsync();
await RunLogoutWhenAnonymousCommandTestAsync();
await RunPositionCommandTestAsync();
await RunMapCommandTestAsync();
await RunMapUsersCommandTestAsync();
await RunWarpCommandTestAsync();
await RunWarpRequiresAuthenticationCommandTestAsync();
await RunWarpWhenNotSpawnedCommandTestAsync();
await RunInvalidWarpCommandTestAsync();
await RunInvalidWarpMapCommandTestAsync();
await RunOutOfBoundsWarpCommandTestAsync();
await RunMoveWhenNotSpawnedCommandTestAsync();
await RunMoveCommandTestAsync();
await RunRepeatedMoveSequenceCommandTestAsync();
await RunMoveCooldownCommandTestAsync();
await RunInvalidMoveCommandTestAsync();
await RunOutOfBoundsMoveCommandTestAsync();
await RunTooFarMoveCommandTestAsync();
await RunNearbyWhenNotSpawnedCommandTestAsync();
await RunNearbyCommandTestAsync();
await RunLookWhenNotSpawnedCommandTestAsync();
await RunLookCommandTestAsync();
await RunSpawnRequiresAuthenticationCommandTestAsync();
await RunSpawnCommandTestAsync();
await RunDuplicateSpawnCommandTestAsync();
await RunDespawnCommandTestAsync();
await RunDespawnWhenNotSpawnedCommandTestAsync();
await RunJoinCommandTestAsync();
await RunMissingJoinRoomCommandTestAsync();
await RunLeaveCommandTestAsync();
await RunInvalidRoomNameCommandTestAsync();
await RunRoomUsersCommandTestAsync();
await RunStatsCommandTestAsync();
await RunMotdCommandTestAsync();
await RunVersionCommandTestAsync();
await RunMeCommandTestAsync();
await RunEmptyMeCommandTestAsync();
await RunMissingMeActionCommandTestAsync();
await RunWhisperCommandTestAsync();
await RunWhisperUnknownUserCommandTestAsync();
await RunInvalidWhisperCommandTestAsync();
await RunMissingWhisperPayloadCommandTestAsync();
await RunRenameCommandTestAsync();
await RunMissingNameCommandTestAsync();
await RunMissingRenameCommandTestAsync();
await RunDuplicateNameCommandTestAsync();
await RunInvalidNameCommandTestAsync();

Console.WriteLine("All socket study tests passed.");

static async Task RunProtocolRoundTripTestAsync(MessageType type, string text)
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();

    int port = ((IPEndPoint)listener.LocalEndpoint).Port;

    using var client = new TcpClient();
    Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();

    await client.ConnectAsync(IPAddress.Loopback, port);
    using TcpClient server = await acceptTask;

    await using NetworkStream clientStream = client.GetStream();
    await using NetworkStream serverStream = server.GetStream();

    await MessageProtocol.WriteMessageAsync(clientStream, type, text);
    NetworkMessage? received = await MessageProtocol.ReadMessageAsync(serverStream);

    listener.Stop();

    if (received is null)
    {
        throw new InvalidOperationException("Expected a message, but received null.");
    }

    if (received.Type != type)
    {
        throw new InvalidOperationException($"Expected type {type}, but received {received.Type}.");
    }

    if (received.Text != text)
    {
        throw new InvalidOperationException($"Expected text '{text}', but received '{received.Text}'.");
    }
}

static async Task RunInvalidMessageTypeTestAsync()
{
    await using NetworkPair pair = await NetworkPair.ConnectAsync();

    byte[] invalidHeader = [255, 0, 0, 0, 0];
    await pair.ClientStream.WriteAsync(invalidHeader);
    await pair.ClientStream.FlushAsync();

    await AssertThrowsAsync<IOException>(
        () => MessageProtocol.ReadMessageAsync(pair.ServerStream),
        "Expected invalid message type to throw IOException.");
}

static async Task RunIncompleteBodyTestAsync()
{
    await using NetworkPair pair = await NetworkPair.ConnectAsync();

    byte[] header = [1, 0, 0, 0, 5];
    byte[] partialBody = [65, 66];
    await pair.ClientStream.WriteAsync(header);
    await pair.ClientStream.WriteAsync(partialBody);
    pair.Client.Close();

    await AssertThrowsAsync<IOException>(
        () => MessageProtocol.ReadMessageAsync(pair.ServerStream),
        "Expected incomplete body to throw IOException.");
}

static async Task RunTooLargeLengthTestAsync()
{
    await using NetworkPair pair = await NetworkPair.ConnectAsync();

    byte[] tooLargeHeader = [1, 0, 16, 0, 1];
    await pair.ClientStream.WriteAsync(tooLargeHeader);
    await pair.ClientStream.FlushAsync();

    await AssertThrowsAsync<IOException>(
        () => MessageProtocol.ReadMessageAsync(pair.ServerStream),
        "Expected oversized message length to throw IOException.");
}

static void RunMessageSizeLimitTest()
{
    string allowedMessage = new('a', MessageProtocol.MaxMessageBytes);
    string tooLargeMessage = new('a', MessageProtocol.MaxMessageBytes + 1);

    if (!MessageProtocol.IsWithinMessageSizeLimit(allowedMessage))
    {
        throw new InvalidOperationException("Expected a message at the size limit to be allowed.");
    }

    if (MessageProtocol.IsWithinMessageSizeLimit(tooLargeMessage))
    {
        throw new InvalidOperationException("Expected a message over the size limit to be rejected.");
    }
}

static void RunNameRulesTest()
{
    if (NameRules.MaxNameLength != 20)
    {
        throw new InvalidOperationException("NameRules should keep the expected max name length.");
    }

    if (!NameRules.HasOnlyAllowedCharacters("alice_123-test"))
    {
        throw new InvalidOperationException("NameRules should allow letters, numbers, '-' and '_'.");
    }

    if (NameRules.HasOnlyAllowedCharacters("bad name"))
    {
        throw new InvalidOperationException("NameRules should reject spaces.");
    }

    if (NameRules.HasOnlyAllowedCharacters("bad!name"))
    {
        throw new InvalidOperationException("NameRules should reject unsupported punctuation.");
    }
}

static void RunServerInfoTest()
{
    if (ServerInfo.Name != "SocketStudy")
    {
        throw new InvalidOperationException("ServerInfo should keep the expected server name.");
    }

    if (ServerInfo.Version != "v1")
    {
        throw new InvalidOperationException("ServerInfo should keep the expected server version.");
    }

    if (ServerInfo.VersionMessage != "SocketStudy server v1")
    {
        throw new InvalidOperationException("ServerInfo should build the expected version message.");
    }

    if (ServerInfo.MessageOfTheDay != "Welcome to SocketStudy. Type /help to see commands.")
    {
        throw new InvalidOperationException("ServerInfo should keep the expected MOTD message.");
    }
}

static void RunPlayerSessionTest()
{
    var session = new PlayerSession();

    if (session.IsAuthenticated || session.PlayerId != PlayerSession.AnonymousPlayerId)
    {
        throw new InvalidOperationException("New player sessions should start anonymous.");
    }

    if (session.Position != WorldPosition.Origin)
    {
        throw new InvalidOperationException("New player sessions should start at the world origin.");
    }

    if (session.MapId != WorldRules.DefaultMapId)
    {
        throw new InvalidOperationException("New player sessions should start in the default map.");
    }

    if (session.IsSpawned)
    {
        throw new InvalidOperationException("New player sessions should not start spawned.");
    }

    session.Authenticate(1001);

    if (!session.IsAuthenticated || session.PlayerId != 1001)
    {
        throw new InvalidOperationException("Player sessions should store authenticated player ids.");
    }

    try
    {
        session.Authenticate(2002);
        throw new InvalidOperationException("Player sessions should reject repeated authentication.");
    }
    catch (InvalidOperationException exception) when (exception.Message == "Player session is already authenticated.")
    {
    }

    if (session.PlayerId != 1001)
    {
        throw new InvalidOperationException("Repeated authentication should not replace the player id.");
    }

    DateTimeOffset lastMoveAt = DateTimeOffset.UnixEpoch;
    session.MoveTo(new WorldPosition(10, 20), lastMoveAt, sequence: 1);

    if (session.Position != new WorldPosition(10, 20) ||
        session.LastMoveAt != lastMoveAt ||
        session.LastMoveSequence != 1)
    {
        throw new InvalidOperationException("Player sessions should store approved movement state.");
    }

    try
    {
        session.MoveTo(new WorldPosition(11, 20), lastMoveAt.AddSeconds(1), sequence: 1);
        throw new InvalidOperationException("Player sessions should reject repeated move sequences.");
    }
    catch (ArgumentOutOfRangeException exception) when (exception.ParamName == "sequence")
    {
    }

    try
    {
        session.ChangeMap(0);
        throw new InvalidOperationException("Player sessions should reject invalid map ids.");
    }
    catch (ArgumentOutOfRangeException exception) when (exception.ParamName == "mapId")
    {
    }

    if (session.MapId != WorldRules.DefaultMapId)
    {
        throw new InvalidOperationException("Invalid map changes should preserve the current map.");
    }

    session.ChangeMap(2);

    if (session.Position != new WorldPosition(10, 20) ||
        session.MapId != 2 ||
        session.LastMoveAt is not null ||
        session.LastMoveSequence != 0)
    {
        throw new InvalidOperationException("Player sessions should reset old map movement tracking.");
    }

    session.Spawn();

    if (!session.IsSpawned)
    {
        throw new InvalidOperationException("Player sessions should store spawn state.");
    }

    try
    {
        session.ChangeMap(WorldRules.DefaultMapId);
        throw new InvalidOperationException("Player sessions should reject map changes while spawned.");
    }
    catch (InvalidOperationException exception) when (exception.Message == "Spawned player session cannot change maps.")
    {
    }

    try
    {
        session.Logout();
        throw new InvalidOperationException("Player sessions should reject logout while spawned.");
    }
    catch (InvalidOperationException exception) when (exception.Message == "Spawned player session cannot logout.")
    {
    }

    session.Despawn();

    if (session.IsSpawned)
    {
        throw new InvalidOperationException("Player sessions should store despawn state.");
    }

    session.Logout();

    if (session.IsAuthenticated || session.PlayerId != PlayerSession.AnonymousPlayerId ||
        session.Position != WorldPosition.Origin || session.MapId != WorldRules.DefaultMapId)
    {
        throw new InvalidOperationException("Player sessions should reset authentication, position, and map on logout.");
    }
}

static async Task RunPlayerEntityTestAsync()
{
    await using NetworkPair pair = await NetworkPair.ConnectAsync();
    var connection = new ClientConnection("alice", pair.Client, pair.ClientStream);

    connection.Session.Authenticate(1001);
    connection.Session.MoveTo(new WorldPosition(10, 20));
    connection.Session.ChangeMap(2);
    connection.Session.Spawn();

    PlayerEntity entity = PlayerEntity.FromConnection(connection);

    if (entity.EntityId != 1001 ||
        entity.PlayerId != 1001 ||
        entity.Name != "alice" ||
        entity.MapId != 2 ||
        entity.Position != new WorldPosition(10, 20) ||
        !entity.IsSpawned)
    {
        throw new InvalidOperationException("PlayerEntity should copy the expected world-facing player state.");
    }
}

static void RunWorldEventTest()
{
    var position = new WorldPosition(10, 20);

    if (WorldEvent.PlayerSpawned("alice", 1, position).ToNoticeMessage() != "alice spawned at x=10, y=20")
    {
        throw new InvalidOperationException("WorldEvent should format player spawn notices.");
    }

    if (WorldEvent.PlayerMoved("alice", 1, position).ToNoticeMessage() != "alice moved to x=10, y=20")
    {
        throw new InvalidOperationException("WorldEvent should format player move notices.");
    }

    if (WorldEvent.PlayerDespawned("alice", 1, position).ToNoticeMessage() != "alice despawned from x=10, y=20")
    {
        throw new InvalidOperationException("WorldEvent should format player despawn notices.");
    }

    if (WorldEvent.PlayerLeftMap("alice", 2, position).ToNoticeMessage() != "alice left map 2 from x=10, y=20")
    {
        throw new InvalidOperationException("WorldEvent should format player map leave notices.");
    }

    if (WorldEvent.PlayerEnteredMap("alice", 2, position).ToNoticeMessage() != "alice entered map 2 at x=10, y=20")
    {
        throw new InvalidOperationException("WorldEvent should format player map enter notices.");
    }
}

static void RunMovementTickProcessorTest()
{
    var session = new PlayerSession();
    DateTimeOffset firstTick = DateTimeOffset.UnixEpoch;

    MovementTickResult accepted = MovementTickProcessor.Process(
        session,
        new MovementRequest(1, new WorldPosition(4, 6), firstTick));

    if (!accepted.IsAccepted ||
        session.Position != new WorldPosition(4, 6) ||
        session.LastMoveAt != firstTick ||
        session.LastMoveSequence != 1)
    {
        throw new InvalidOperationException("MovementTickProcessor should apply accepted movement requests.");
    }

    MovementTickResult repeated = MovementTickProcessor.Process(
        session,
        new MovementRequest(1, new WorldPosition(5, 6), firstTick.AddSeconds(1)));

    if (repeated.IsAccepted ||
        repeated.RejectionReason != "Move sequence must be greater than 1." ||
        session.Position != new WorldPosition(4, 6))
    {
        throw new InvalidOperationException("MovementTickProcessor should reject repeated movement sequences without changing state.");
    }

    MovementTickResult cooldown = MovementTickProcessor.Process(
        session,
        new MovementRequest(2, new WorldPosition(5, 6), firstTick.AddMilliseconds(500)));

    if (cooldown.IsAccepted ||
        cooldown.RejectionReason != "You must wait 1 second between moves." ||
        session.LastMoveSequence != 1)
    {
        throw new InvalidOperationException("MovementTickProcessor should reject cooldown movement without consuming sequence.");
    }
}

static void RunMovementRequestQueueTest()
{
    var queue = new MovementRequestQueue();
    var firstSession = new PlayerSession();
    var secondSession = new PlayerSession();
    var first = new QueuedMovementRequest(
        firstSession,
        new MovementRequest(1, new WorldPosition(1, 0), DateTimeOffset.UnixEpoch));
    var second = new QueuedMovementRequest(
        secondSession,
        new MovementRequest(2, new WorldPosition(2, 0), DateTimeOffset.UnixEpoch));

    queue.Enqueue(first);
    queue.Enqueue(second);

    if (queue.Count != 2 || !queue.TryDequeue(out QueuedMovementRequest? dequeuedFirst) || dequeuedFirst != first)
    {
        throw new InvalidOperationException("MovementRequestQueue should dequeue the oldest request first.");
    }

    if (!queue.TryDequeue(out QueuedMovementRequest? dequeuedSecond) || dequeuedSecond != second)
    {
        throw new InvalidOperationException("MovementRequestQueue should preserve FIFO order.");
    }

    if (queue.Count != 0 || queue.TryDequeue(out _))
    {
        throw new InvalidOperationException("MovementRequestQueue should report an empty queue after draining.");
    }
}

static void RunWorldRulesTest()
{
    if (!WorldRules.IsInsideWorld(WorldPosition.Origin))
    {
        throw new InvalidOperationException("WorldRules should allow the origin.");
    }

    if (!WorldRules.IsInsideWorld(new WorldPosition(WorldRules.MinCoordinate, WorldRules.MaxCoordinate)))
    {
        throw new InvalidOperationException("WorldRules should allow positions on the boundary.");
    }

    if (WorldRules.IsInsideWorld(new WorldPosition(WorldRules.MaxCoordinate + 1, 0)))
    {
        throw new InvalidOperationException("WorldRules should reject positions outside the boundary.");
    }

    if (!WorldRules.IsNearby(WorldPosition.Origin, new WorldPosition(10, 10)))
    {
        throw new InvalidOperationException("WorldRules should treat close positions as nearby.");
    }

    if (WorldRules.GetDistance(WorldPosition.Origin, new WorldPosition(10, -5)) != 15)
    {
        throw new InvalidOperationException("WorldRules should calculate Manhattan distance.");
    }

    if (WorldRules.IsNearby(WorldPosition.Origin, new WorldPosition(30, 0)))
    {
        throw new InvalidOperationException("WorldRules should reject positions outside view distance.");
    }

    if (WorldRules.MaxNearbySnapshotCount != 10)
    {
        throw new InvalidOperationException("WorldRules should keep the expected nearby snapshot limit.");
    }

    if (!WorldRules.IsWithinMoveDistance(WorldPosition.Origin, new WorldPosition(4, 6)))
    {
        throw new InvalidOperationException("WorldRules should allow movement at the maximum move distance.");
    }

    if (WorldRules.IsWithinMoveDistance(WorldPosition.Origin, new WorldPosition(11, 0)))
    {
        throw new InvalidOperationException("WorldRules should reject movement beyond the maximum move distance.");
    }

    DateTimeOffset movedAt = DateTimeOffset.UnixEpoch;
    if (WorldRules.IsMoveCooldownElapsed(movedAt, movedAt.AddMilliseconds(999)))
    {
        throw new InvalidOperationException("WorldRules should reject movement before the cooldown elapses.");
    }

    if (!WorldRules.IsMoveCooldownElapsed(movedAt, movedAt.AddSeconds(1)))
    {
        throw new InvalidOperationException("WorldRules should allow movement when the cooldown elapses.");
    }
}

static void RunWorldGridTest()
{
    WorldGridCell originCell = WorldGrid.GetCell(WorldRules.DefaultMapId, WorldPosition.Origin);

    if (originCell != new WorldGridCell(WorldRules.DefaultMapId, 0, 0))
    {
        throw new InvalidOperationException("WorldGrid should place the origin in the first cell.");
    }

    if (WorldGrid.GetCell(WorldRules.DefaultMapId, new WorldPosition(WorldRules.GridCellSize, 0)) !=
        new WorldGridCell(WorldRules.DefaultMapId, 1, 0))
    {
        throw new InvalidOperationException("WorldGrid should move to the next cell at the cell boundary.");
    }

    if (WorldGrid.GetCell(WorldRules.DefaultMapId, new WorldPosition(-1, -1)) !=
        new WorldGridCell(WorldRules.DefaultMapId, -1, -1))
    {
        throw new InvalidOperationException("WorldGrid should floor negative coordinates into negative cells.");
    }

    WorldGridCell[] neighborCells = WorldGrid.GetNeighborCells(originCell);

    if (neighborCells.Length != 9 ||
        !neighborCells.Contains(originCell) ||
        !neighborCells.Contains(new WorldGridCell(WorldRules.DefaultMapId, -1, -1)) ||
        !neighborCells.Contains(new WorldGridCell(WorldRules.DefaultMapId, 1, 1)) ||
        neighborCells.Contains(new WorldGridCell(2, 0, 0)))
    {
        throw new InvalidOperationException("WorldGrid should return the 3x3 neighbor cells on the same map.");
    }
}

static void RunServerPortParseTest()
{
    bool parsed = CommandLineOptions.TryReadServerPort(["server", "6500"], out int port);

    if (!parsed || port != 6500)
    {
        throw new InvalidOperationException($"Expected server port 6500, but received {port}.");
    }
}

static void RunLocalClientOptionParseTest()
{
    bool parsed = CommandLineOptions.TryReadClientOptions(
        ["client", "6500", "alice"],
        out string host,
        out int port,
        out string? nickname);

    if (!parsed || host != "127.0.0.1" || port != 6500 || nickname != "alice")
    {
        throw new InvalidOperationException("Local client options were not parsed correctly.");
    }
}

static void RunRemoteClientOptionParseTest()
{
    bool parsed = CommandLineOptions.TryReadClientOptions(
        ["client", "192.168.0.10", "6500", "bob"],
        out string host,
        out int port,
        out string? nickname);

    if (!parsed || host != "192.168.0.10" || port != 6500 || nickname != "bob")
    {
        throw new InvalidOperationException("Remote client options were not parsed correctly.");
    }
}

static void RunInvalidClientNicknameOptionParseTest()
{
    TextWriter originalOutput = Console.Out;
    using var capturedOutput = new StringWriter();

    try
    {
        Console.SetOut(capturedOutput);

        bool parsed = CommandLineOptions.TryReadClientOptions(
            ["client", "6500", "bad name"],
            out _,
            out _,
            out _);

        if (parsed)
        {
            throw new InvalidOperationException("Invalid client nickname option should not be parsed successfully.");
        }
    }
    finally
    {
        Console.SetOut(originalOutput);
    }

    if (!capturedOutput.ToString().Contains(NameRules.NicknameCharacterRuleMessage))
    {
        throw new InvalidOperationException("Invalid client nickname option did not print the expected message.");
    }
}

static async Task RunClientRegistryTracksCountAndNamesAsync()
{
    var registry = new ClientRegistry();
    await using NetworkPair alicePair = await NetworkPair.ConnectAsync();
    await using NetworkPair bobPair = await NetworkPair.ConnectAsync();
    var alice = new ClientConnection("alice", alicePair.Client, alicePair.ClientStream);
    var bob = new ClientConnection("bob", bobPair.Client, bobPair.ClientStream);

    int firstCount = registry.Add(bob);
    int secondCount = registry.Add(alice);

    if (firstCount != 1 || secondCount != 2 || registry.Count != 2)
    {
        throw new InvalidOperationException("ClientRegistry did not track add counts correctly.");
    }

    if (!registry.GetNames().SequenceEqual(["alice", "bob"]))
    {
        throw new InvalidOperationException("ClientRegistry did not return sorted client names.");
    }

    int remainingCount = registry.Remove(alice);
    if (remainingCount != 1 || registry.Snapshot().Single() != bob)
    {
        throw new InvalidOperationException("ClientRegistry did not remove the expected client.");
    }
}

static async Task RunClientRegistryFindsNamesCaseInsensitiveAsync()
{
    var registry = new ClientRegistry();
    await using NetworkPair alicePair = await NetworkPair.ConnectAsync();
    await using NetworkPair bobPair = await NetworkPair.ConnectAsync();
    var alice = new ClientConnection("alice", alicePair.Client, alicePair.ClientStream);
    var bob = new ClientConnection("bob", bobPair.Client, bobPair.ClientStream);

    registry.Add(alice);
    registry.Add(bob);

    if (registry.FindByName("ALICE") != alice)
    {
        throw new InvalidOperationException("ClientRegistry did not find a client name case-insensitively.");
    }

    if (!registry.IsNameInUse("BOB", alice))
    {
        throw new InvalidOperationException("ClientRegistry did not detect a duplicate name.");
    }

    if (registry.IsNameInUse("ALICE", alice))
    {
        throw new InvalidOperationException("ClientRegistry should ignore the current connection when checking names.");
    }
}

static void RunClientRegistryIncludesDefaultRoom()
{
    var registry = new ClientRegistry();

    if (!registry.GetRoomNames().SequenceEqual([ClientRegistry.DefaultRoomName]))
    {
        throw new InvalidOperationException("ClientRegistry should always include the default room.");
    }
}

static async Task RunClientRegistryFiltersRoomsAsync()
{
    var registry = new ClientRegistry();
    await using NetworkPair alicePair = await NetworkPair.ConnectAsync();
    await using NetworkPair bobPair = await NetworkPair.ConnectAsync();
    await using NetworkPair claraPair = await NetworkPair.ConnectAsync();
    var alice = new ClientConnection("alice", alicePair.Client, alicePair.ClientStream);
    var bob = new ClientConnection("bob", bobPair.Client, bobPair.ClientStream);
    var clara = new ClientConnection("clara", claraPair.Client, claraPair.ClientStream);

    alice.MoveToRoom("study");
    clara.MoveToRoom("study");
    registry.Add(alice);
    registry.Add(bob);
    registry.Add(clara);

    if (!registry.GetRoomNames().SequenceEqual(["lobby", "study"]))
    {
        throw new InvalidOperationException("ClientRegistry did not return sorted room names.");
    }

    if (!registry.GetNamesInRoom("STUDY").SequenceEqual(["alice", "clara"]))
    {
        throw new InvalidOperationException("ClientRegistry did not filter room users case-insensitively.");
    }

    if (registry.SnapshotRoom("study", alice).Single() != clara)
    {
        throw new InvalidOperationException("ClientRegistry did not snapshot a room with the expected exclusion.");
    }
}

static async Task RunClientRegistrySnapshotsRoomsCaseInsensitiveAsync()
{
    var registry = new ClientRegistry();
    await using NetworkPair alicePair = await NetworkPair.ConnectAsync();
    await using NetworkPair bobPair = await NetworkPair.ConnectAsync();
    var alice = new ClientConnection("alice", alicePair.Client, alicePair.ClientStream);
    var bob = new ClientConnection("bob", bobPair.Client, bobPair.ClientStream);

    alice.MoveToRoom("study");
    bob.MoveToRoom("study");
    registry.Add(alice);
    registry.Add(bob);

    ClientConnection[] roomSnapshot = registry.SnapshotRoom("STUDY", except: alice);

    if (roomSnapshot.Single() != bob)
    {
        throw new InvalidOperationException("ClientRegistry room snapshots should ignore room name casing.");
    }
}

static async Task RunClientRegistryFindsSpawnedPlayersByMapAsync()
{
    var registry = new ClientRegistry();
    await using NetworkPair alicePair = await NetworkPair.ConnectAsync();
    await using NetworkPair bobPair = await NetworkPair.ConnectAsync();
    await using NetworkPair claraPair = await NetworkPair.ConnectAsync();
    var alice = new ClientConnection("alice", alicePair.Client, alicePair.ClientStream);
    var bob = new ClientConnection("bob", bobPair.Client, bobPair.ClientStream);
    var clara = new ClientConnection("clara", claraPair.Client, claraPair.ClientStream);

    alice.Session.Spawn();
    bob.Session.Spawn();
    clara.Session.ChangeMap(2);
    clara.Session.Spawn();
    registry.Add(bob);
    registry.Add(clara);
    registry.Add(alice);

    if (!registry.GetSpawnedPlayerNamesInMap(WorldRules.DefaultMapId).SequenceEqual(["alice", "bob"]))
    {
        throw new InvalidOperationException("ClientRegistry should return spawned players in the requested map.");
    }

    bob.Session.Despawn();

    if (!registry.GetSpawnedPlayerNamesInMap(WorldRules.DefaultMapId).SequenceEqual(["alice"]))
    {
        throw new InvalidOperationException("ClientRegistry should exclude despawned players from map player lists.");
    }
}

static async Task RunClientRegistryFindsNearbyNamesAsync()
{
    var registry = new ClientRegistry();
    await using NetworkPair alicePair = await NetworkPair.ConnectAsync();
    await using NetworkPair bobPair = await NetworkPair.ConnectAsync();
    await using NetworkPair claraPair = await NetworkPair.ConnectAsync();
    var alice = new ClientConnection("alice", alicePair.Client, alicePair.ClientStream);
    var bob = new ClientConnection("bob", bobPair.Client, bobPair.ClientStream);
    var clara = new ClientConnection("clara", claraPair.Client, claraPair.ClientStream);

    alice.Session.Spawn();
    bob.MoveToRoom("trade");
    bob.Session.MoveTo(new WorldPosition(10, 10));
    bob.Session.Spawn();
    clara.Session.ChangeMap(2);
    clara.Session.MoveTo(new WorldPosition(5, 5));
    clara.Session.Spawn();
    registry.Add(alice);
    registry.Add(bob);
    registry.Add(clara);

    if (!registry.GetNearbyNames(alice).SequenceEqual(["bob"]))
    {
        throw new InvalidOperationException("ClientRegistry did not return the expected nearby names.");
    }

    if (registry.SnapshotNearby(alice).Single() != bob)
    {
        throw new InvalidOperationException("ClientRegistry did not return the expected nearby snapshot.");
    }

    bob.Session.Despawn();

    if (registry.GetNearbyNames(alice).Length != 0 || registry.SnapshotNearby(alice).Length != 0)
    {
        throw new InvalidOperationException("ClientRegistry should exclude nearby clients that are not spawned.");
    }
}

static async Task RunClientRegistryFindsNearbySnapshotsAsync()
{
    var registry = new ClientRegistry();
    await using NetworkPair alicePair = await NetworkPair.ConnectAsync();
    await using NetworkPair bobPair = await NetworkPair.ConnectAsync();
    await using NetworkPair claraPair = await NetworkPair.ConnectAsync();
    await using NetworkPair dylanPair = await NetworkPair.ConnectAsync();
    var alice = new ClientConnection("alice", alicePair.Client, alicePair.ClientStream);
    var bob = new ClientConnection("bob", bobPair.Client, bobPair.ClientStream);
    var clara = new ClientConnection("clara", claraPair.Client, claraPair.ClientStream);
    var dylan = new ClientConnection("dylan", dylanPair.Client, dylanPair.ClientStream);

    alice.Session.Spawn();
    bob.Session.Authenticate(2002);
    bob.Session.MoveTo(new WorldPosition(10, 10));
    bob.Session.Spawn();
    clara.Session.Authenticate(3003);
    clara.Session.MoveTo(new WorldPosition(2, 2));
    clara.Session.Spawn();
    dylan.Session.Authenticate(4004);
    dylan.Session.ChangeMap(2);
    dylan.Session.MoveTo(new WorldPosition(5, 5));
    dylan.Session.Spawn();
    registry.Add(alice);
    registry.Add(bob);
    registry.Add(clara);
    registry.Add(dylan);

    NearbySnapshotResult snapshotResult = registry.GetNearbySnapshots(alice);
    NearbyPlayerSnapshot[] snapshots = snapshotResult.Snapshots;

    if (snapshotResult.TotalCount != 2 ||
        snapshotResult.HiddenCount != 0 ||
        snapshots.Length != 2 ||
        snapshots[0].Name != "clara" ||
        snapshots[0].PlayerId != 3003 ||
        snapshots[0].MapId != WorldRules.DefaultMapId ||
        snapshots[0].Position != new WorldPosition(2, 2) ||
        snapshots[0].Distance != 4 ||
        snapshots[1].Name != "bob" ||
        snapshots[1].PlayerId != 2002 ||
        snapshots[1].MapId != WorldRules.DefaultMapId ||
        snapshots[1].Position != new WorldPosition(10, 10) ||
        snapshots[1].Distance != 20)
    {
        throw new InvalidOperationException("ClientRegistry did not return nearby player snapshots ordered by distance.");
    }

    clara.Session.Despawn();
    bob.Session.Despawn();

    NearbySnapshotResult emptySnapshotResult = registry.GetNearbySnapshots(alice);
    if (emptySnapshotResult.TotalCount != 0 ||
        emptySnapshotResult.HiddenCount != 0 ||
        emptySnapshotResult.Snapshots.Length != 0)
    {
        throw new InvalidOperationException("ClientRegistry should exclude despawned players from nearby snapshots.");
    }
}

static async Task RunClientRegistryLimitsNearbySnapshotsAsync()
{
    var registry = new ClientRegistry();
    var pairs = new List<NetworkPair>();

    try
    {
        NetworkPair alicePair = await NetworkPair.ConnectAsync();
        pairs.Add(alicePair);
        var alice = new ClientConnection("alice", alicePair.Client, alicePair.ClientStream);
        alice.Session.Spawn();
        registry.Add(alice);

        for (int index = 1; index <= WorldRules.MaxNearbySnapshotCount + 2; index++)
        {
            NetworkPair pair = await NetworkPair.ConnectAsync();
            pairs.Add(pair);
            var client = new ClientConnection($"player{index:D2}", pair.Client, pair.ClientStream);
            client.Session.Authenticate(1000 + index);
            client.Session.MoveTo(new WorldPosition(index, 0));
            client.Session.Spawn();
            registry.Add(client);
        }

        NearbySnapshotResult snapshotResult = registry.GetNearbySnapshots(alice);
        NearbyPlayerSnapshot[] snapshots = snapshotResult.Snapshots;

        if (snapshotResult.TotalCount != WorldRules.MaxNearbySnapshotCount + 2 ||
            snapshotResult.HiddenCount != 2 ||
            snapshots.Length != WorldRules.MaxNearbySnapshotCount)
        {
            throw new InvalidOperationException("ClientRegistry should limit nearby snapshot count.");
        }

        if (snapshots[0].Name != "player01" ||
            snapshots[0].Distance != 1 ||
            snapshots[^1].Name != "player10" ||
            snapshots[^1].Distance != 10)
        {
            throw new InvalidOperationException("ClientRegistry should keep the nearest nearby snapshots first.");
        }
    }
    finally
    {
        foreach (NetworkPair pair in pairs)
        {
            await pair.DisposeAsync();
        }
    }
}

static async Task RunClientRegistryDrainsConnectionsAsync()
{
    var registry = new ClientRegistry();
    await using NetworkPair alicePair = await NetworkPair.ConnectAsync();
    await using NetworkPair bobPair = await NetworkPair.ConnectAsync();
    var alice = new ClientConnection("alice", alicePair.Client, alicePair.ClientStream);
    var bob = new ClientConnection("bob", bobPair.Client, bobPair.ClientStream);

    registry.Add(alice);
    registry.Add(bob);

    ClientConnection[] drained = registry.Drain();

    if (drained.Length != 2 || registry.Count != 0)
    {
        throw new InvalidOperationException("ClientRegistry did not drain all connections.");
    }

    if (!drained.Contains(alice) || !drained.Contains(bob))
    {
        throw new InvalidOperationException("ClientRegistry drain did not return the original connections.");
    }
}

static async Task RunHelpCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/help"));

    if (!handled || context.SentMessages.Count != 1)
    {
        throw new InvalidOperationException("Expected /help to send one notice message.");
    }

    SentMessage sent = context.SentMessages[0];
    if (sent.Type != MessageType.Notice ||
        !sent.Text.Contains("/join <room>") ||
        !sent.Text.Contains("/motd") ||
        !sent.Text.Contains("/echo <message>"))
    {
        throw new InvalidOperationException("/help output did not include expected command list.");
    }
}

static async Task RunCommandsAliasTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/commands"));

    if (!handled || !context.SentMessages.Single().Text.Contains("/commands"))
    {
        throw new InvalidOperationException("/commands did not return the command list.");
    }
}

static async Task RunWhereCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.MoveToRoom("study");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/where"));

    if (!handled || context.SentMessages.Single().Text != "Current room: study")
    {
        throw new InvalidOperationException("/where did not report the current room.");
    }
}

static async Task RunPingCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/ping"));

    if (!handled || context.SentMessages.Single().Text != "pong")
    {
        throw new InvalidOperationException("/ping did not return pong.");
    }
}

static async Task RunEchoCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/echo hello server"));

    if (!handled || context.SentMessages.Single().Text != "echo: hello server")
    {
        throw new InvalidOperationException("/echo did not return the expected message.");
    }
}

static async Task RunEmptyEchoCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/echo   "));

    if (!handled || context.SentMessages.Single().Text != "Usage: /echo <message>")
    {
        throw new InvalidOperationException("Empty /echo did not return the expected usage notice.");
    }
}

static async Task RunMissingEchoMessageCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/echo"));

    if (!handled || context.SentMessages.Single().Text != "Usage: /echo <message>")
    {
        throw new InvalidOperationException("Missing /echo message did not return the expected usage notice.");
    }
}

static async Task RunTimeCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.CurrentTime = new DateTimeOffset(2026, 6, 24, 10, 30, 0, TimeSpan.FromHours(9));

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/time"));

    if (!handled || context.SentMessages.Single().Text != "Server time: 2026-06-24 10:30:00 +09:00")
    {
        throw new InvalidOperationException("/time did not return the injected server time.");
    }
}

static async Task RunUptimeCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.ServerStartedAt = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.FromHours(9));
    context.CurrentTime = new DateTimeOffset(2026, 6, 24, 10, 5, 7, TimeSpan.FromHours(9));

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/uptime"));

    if (!handled || context.SentMessages.Single().Text != "Server uptime: 00:05:07")
    {
        throw new InvalidOperationException("/uptime did not return the expected elapsed time.");
    }
}

static async Task RunWhoAmICommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.MoveToRoom("study");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/whoami"));

    if (!handled || context.SentMessages.Single().Text != "You are alice in room study.")
    {
        throw new InvalidOperationException("/whoami did not return the current client identity.");
    }
}

static async Task RunSessionCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/session"));

    if (!handled || context.SentMessages.Single().Text != "Session: player-id=0, state=anonymous, spawn=not-spawned")
    {
        throw new InvalidOperationException("/session did not return the expected anonymous session state.");
    }
}

static async Task RunLoginCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/login 1001"));

    if (!handled || context.Connection.Session.PlayerId != 1001 || !context.Connection.Session.IsAuthenticated)
    {
        throw new InvalidOperationException("/login did not authenticate the player session.");
    }

    if (context.SentMessages.Single().Text != "Logged in as player 1001.")
    {
        throw new InvalidOperationException("/login did not return the expected notice.");
    }
}

static async Task RunAuthenticatedSessionCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/session"));

    if (!handled || context.SentMessages.Single().Text != "Session: player-id=1001, state=authenticated, spawn=not-spawned")
    {
        throw new InvalidOperationException("/session did not return the expected authenticated session state.");
    }
}

static async Task RunDuplicateLoginCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/login 2002"));

    if (!handled || context.SentMessages.Single().Text != "You are already logged in as player 1001.")
    {
        throw new InvalidOperationException("Duplicate /login did not return the expected notice.");
    }

    if (context.Connection.Session.PlayerId != 1001)
    {
        throw new InvalidOperationException("Duplicate /login should not replace the authenticated player id.");
    }
}

static async Task RunLoginWhileSpawnedCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/login 2002"));

    if (!handled || context.SentMessages.Single().Text != "You cannot login while spawned.")
    {
        throw new InvalidOperationException("Spawned /login did not return the expected notice.");
    }

    if (context.Connection.Session.PlayerId != 1001 || !context.Connection.Session.IsSpawned)
    {
        throw new InvalidOperationException("Spawned /login should not change player identity or spawn state.");
    }
}

static async Task RunInvalidLoginCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/login abc"));

    if (!handled || context.Connection.Session.IsAuthenticated)
    {
        throw new InvalidOperationException("Invalid /login should not authenticate the player session.");
    }

    if (context.SentMessages.Single().Text != "Player id must be a positive number.")
    {
        throw new InvalidOperationException("Invalid /login did not return the expected notice.");
    }
}

static async Task RunMissingLoginCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/login"));

    if (!handled || context.SentMessages.Single().Text != "Usage: /login <playerId>")
    {
        throw new InvalidOperationException("Missing /login player id did not return the expected usage notice.");
    }
}

static async Task RunLogoutCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);
    context.Connection.Session.MoveTo(new WorldPosition(10, 20));

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/logout"));

    if (!handled || context.SentMessages.Single().Text != "Logged out.")
    {
        throw new InvalidOperationException("/logout did not return the expected notice.");
    }

    if (context.Connection.Session.IsAuthenticated ||
        context.Connection.Session.PlayerId != PlayerSession.AnonymousPlayerId ||
        context.Connection.Session.Position != WorldPosition.Origin)
    {
        throw new InvalidOperationException("/logout did not reset the player session.");
    }
}

static async Task RunLogoutWhileSpawnedCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);
    context.Connection.Session.MoveTo(new WorldPosition(10, 20));
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/logout"));

    if (!handled || context.SentMessages.Single().Text != "You must despawn before logging out.")
    {
        throw new InvalidOperationException("Spawned /logout did not return the expected notice.");
    }

    if (context.Connection.Session.PlayerId != 1001 ||
        !context.Connection.Session.IsAuthenticated ||
        !context.Connection.Session.IsSpawned ||
        context.Connection.Session.Position != new WorldPosition(10, 20))
    {
        throw new InvalidOperationException("Spawned /logout should preserve the player session.");
    }
}

static async Task RunLogoutWhenAnonymousCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/logout"));

    if (!handled || context.SentMessages.Single().Text != "You are not logged in.")
    {
        throw new InvalidOperationException("Anonymous /logout did not return the expected notice.");
    }
}

static async Task RunPositionCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/pos"));

    if (!handled || context.SentMessages.Single().Text != "Position: x=0, y=0")
    {
        throw new InvalidOperationException("/pos did not return the expected default position.");
    }
}

static async Task RunMapCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.ChangeMap(2);

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/map"));

    if (!handled || context.SentMessages.Single().Text != "Map: 2")
    {
        throw new InvalidOperationException("/map did not return the current game map id.");
    }
}

static async Task RunMapUsersCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/map-users"));

    if (!handled || context.SentMessages.Single().Text != "Players in map 1 (2): alice, bob")
    {
        throw new InvalidOperationException("/map-users did not return the current map player list.");
    }
}

static async Task RunWarpCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);
    context.Connection.Session.MoveTo(new WorldPosition(5, 6));
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/warp 2 30 40"));

    if (!handled || context.SentMessages.Single().Text != "Warped to map=2, x=30, y=40")
    {
        throw new InvalidOperationException("/warp did not return the expected notice.");
    }

    if (context.Connection.Session.MapId != 2 ||
        context.Connection.Session.Position != new WorldPosition(30, 40) ||
        !context.Connection.Session.IsSpawned)
    {
        throw new InvalidOperationException("/warp did not update the player world state.");
    }

    string[] expectedNotices =
    [
        "alice left map 1 from x=5, y=6",
        "alice entered map 2 at x=30, y=40"
    ];

    if (!context.NearbyNotices.SequenceEqual(expectedNotices))
    {
        throw new InvalidOperationException("/warp did not notify the old and new map in order.");
    }
}

static async Task RunWarpRequiresAuthenticationCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/warp 2 30 40"));

    if (!handled || context.SentMessages.Single().Text != "You must login before warping.")
    {
        throw new InvalidOperationException("Anonymous /warp did not return the expected notice.");
    }

    if (context.Connection.Session.MapId != WorldRules.DefaultMapId ||
        context.Connection.Session.Position != WorldPosition.Origin ||
        context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("Anonymous /warp should not change or broadcast world state.");
    }
}

static async Task RunWarpWhenNotSpawnedCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/warp 2 30 40"));

    if (!handled || context.SentMessages.Single().Text != "You must spawn before warping.")
    {
        throw new InvalidOperationException("Unspawned /warp did not return the expected notice.");
    }

    if (context.Connection.Session.MapId != WorldRules.DefaultMapId ||
        context.Connection.Session.Position != WorldPosition.Origin ||
        context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("Unspawned /warp should not change or broadcast world state.");
    }
}

static async Task RunInvalidWarpCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/warp second-map"));

    if (!handled || context.SentMessages.Single().Text != "Usage: /warp <mapId> <x> <y>")
    {
        throw new InvalidOperationException("Invalid /warp did not return the expected usage notice.");
    }

    if (context.Connection.Session.MapId != WorldRules.DefaultMapId ||
        context.Connection.Session.Position != WorldPosition.Origin ||
        !context.Connection.Session.IsSpawned ||
        context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("Invalid /warp should preserve world state.");
    }
}

static async Task RunInvalidWarpMapCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/warp 0 30 40"));

    if (!handled || context.SentMessages.Single().Text != "Map id must be positive.")
    {
        throw new InvalidOperationException("Invalid /warp map did not return the expected notice.");
    }

    if (context.Connection.Session.MapId != WorldRules.DefaultMapId ||
        !context.Connection.Session.IsSpawned ||
        context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("Invalid /warp map should preserve world state.");
    }
}

static async Task RunOutOfBoundsWarpCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/warp 2 101 0"));

    if (!handled || context.SentMessages.Single().Text != "Position must be between -100 and 100.")
    {
        throw new InvalidOperationException("Out-of-bounds /warp did not return the expected notice.");
    }

    if (context.Connection.Session.MapId != WorldRules.DefaultMapId ||
        context.Connection.Session.Position != WorldPosition.Origin ||
        !context.Connection.Session.IsSpawned ||
        context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("Out-of-bounds /warp should preserve world state.");
    }
}

static async Task RunMoveCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/move 1 4 6"));

    if (!handled || context.Connection.Session.Position != new WorldPosition(4, 6))
    {
        throw new InvalidOperationException("/move did not update the player session position.");
    }

    if (context.SentMessages.Single().Text != "Moved to x=4, y=6")
    {
        throw new InvalidOperationException("/move did not return the expected notice.");
    }

    if (context.NearbyNotices.Single() != "alice moved to x=4, y=6")
    {
        throw new InvalidOperationException("/move did not notify nearby players.");
    }

    if (context.Connection.Session.LastMoveSequence != 1)
    {
        throw new InvalidOperationException("/move did not store the movement sequence.");
    }
}

static async Task RunMoveWhenNotSpawnedCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/move 1 10 20"));

    if (!handled || context.Connection.Session.Position != WorldPosition.Origin)
    {
        throw new InvalidOperationException("/move should not update an unspawned player position.");
    }

    if (context.SentMessages.Single().Text != "You must spawn before moving.")
    {
        throw new InvalidOperationException("/move did not explain that the player must spawn first.");
    }

    if (context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("/move should not notify nearby players when movement is rejected.");
    }
}

static async Task RunRepeatedMoveSequenceCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Spawn();
    DateTimeOffset firstMoveAt = new(2026, 7, 5, 10, 0, 0, TimeSpan.FromHours(9));
    context.CurrentTime = firstMoveAt;

    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/move 10 4 0"));

    context.SentMessages.Clear();
    context.NearbyNotices.Clear();
    context.CurrentTime = firstMoveAt.AddSeconds(2);

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/move 10 8 0"));

    if (!handled || context.SentMessages.Single().Text != "Move sequence must be greater than 10.")
    {
        throw new InvalidOperationException("Repeated move sequence did not return the expected notice.");
    }

    if (context.Connection.Session.Position != new WorldPosition(4, 0) ||
        context.Connection.Session.LastMoveAt != firstMoveAt ||
        context.Connection.Session.LastMoveSequence != 10 ||
        context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("Repeated move sequence should preserve movement state.");
    }
}

static async Task RunMoveCooldownCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Spawn();
    DateTimeOffset firstMoveAt = new(2026, 6, 30, 10, 0, 0, TimeSpan.FromHours(9));
    context.CurrentTime = firstMoveAt;

    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/move 1 4 0"));

    context.SentMessages.Clear();
    context.NearbyNotices.Clear();
    context.CurrentTime = firstMoveAt.AddMilliseconds(500);

    bool rejected = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/move 2 8 0"));

    if (!rejected || context.SentMessages.Single().Text != "You must wait 1 second between moves.")
    {
        throw new InvalidOperationException("Early repeated /move did not return the expected cooldown notice.");
    }

    if (context.Connection.Session.Position != new WorldPosition(4, 0) ||
        context.Connection.Session.LastMoveAt != firstMoveAt ||
        context.Connection.Session.LastMoveSequence != 1 ||
        context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("Rejected cooldown /move should preserve movement state.");
    }

    context.SentMessages.Clear();
    context.CurrentTime = firstMoveAt.AddSeconds(1);

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/move 2 8 0"));

    if (!handled || context.SentMessages.Single().Text != "Moved to x=8, y=0")
    {
        throw new InvalidOperationException("/move should succeed when the cooldown has elapsed.");
    }

    if (context.Connection.Session.Position != new WorldPosition(8, 0) ||
        context.Connection.Session.LastMoveAt != context.CurrentTime ||
        context.Connection.Session.LastMoveSequence != 2 ||
        context.NearbyNotices.Single() != "alice moved to x=8, y=0")
    {
        throw new InvalidOperationException("Successful cooldown /move did not update movement state.");
    }
}

static async Task RunInvalidMoveCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/move north"));

    if (!handled || context.Connection.Session.Position != WorldPosition.Origin)
    {
        throw new InvalidOperationException("Invalid /move should not update the player session position.");
    }

    if (context.SentMessages.Single().Text != "Usage: /move <sequence> <x> <y>")
    {
        throw new InvalidOperationException("Invalid /move did not return the expected usage notice.");
    }
}

static async Task RunOutOfBoundsMoveCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/move 1 101 0"));

    if (!handled || context.Connection.Session.Position != WorldPosition.Origin)
    {
        throw new InvalidOperationException("Out-of-bounds /move should not update the player session position.");
    }

    if (context.SentMessages.Single().Text != "Position must be between -100 and 100.")
    {
        throw new InvalidOperationException("Out-of-bounds /move did not return the expected notice.");
    }
}

static async Task RunTooFarMoveCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/move 1 11 0"));

    if (!handled || context.Connection.Session.Position != WorldPosition.Origin)
    {
        throw new InvalidOperationException("Too-far /move should not update the player session position.");
    }

    if (context.SentMessages.Single().Text != "Move distance must be 10 or less.")
    {
        throw new InvalidOperationException("Too-far /move did not return the expected notice.");
    }

    if (context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("Too-far /move should not notify nearby players.");
    }
}

static async Task RunNearbyCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Spawn();
    context.TargetConnection.Session.MoveTo(new WorldPosition(10, 10));
    context.TargetConnection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/nearby"));

    if (!handled || context.SentMessages.Single().Text != "Nearby players (1): bob")
    {
        throw new InvalidOperationException("/nearby did not return the expected nearby player list.");
    }
}

static async Task RunNearbyWhenNotSpawnedCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.TargetConnection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/nearby"));

    if (!handled || context.SentMessages.Single().Text != "You must spawn before checking nearby players.")
    {
        throw new InvalidOperationException("/nearby should explain that the player must spawn first.");
    }
}

static async Task RunLookCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Spawn();
    context.TargetConnection.Session.Authenticate(2002);
    context.TargetConnection.Session.MoveTo(new WorldPosition(10, 10));
    context.TargetConnection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/look"));

    if (!handled || context.SentMessages.Single().Text != "Nearby snapshots (1/1, hidden=0): bob[player-id=2002,map=1,x=10, y=10,distance=20]")
    {
        throw new InvalidOperationException("/look did not return the expected nearby player snapshot.");
    }
}

static async Task RunLookWhenNotSpawnedCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.TargetConnection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/look"));

    if (!handled || context.SentMessages.Single().Text != "You must spawn before looking around.")
    {
        throw new InvalidOperationException("/look should explain that the player must spawn first.");
    }
}

static async Task RunSpawnCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);
    context.Connection.Session.MoveTo(new WorldPosition(10, 20));

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/spawn"));

    if (!handled || context.SentMessages.Single().Text != "Spawned at x=10, y=20")
    {
        throw new InvalidOperationException("/spawn did not return the expected notice.");
    }

    if (!context.Connection.Session.IsSpawned)
    {
        throw new InvalidOperationException("/spawn did not update the player session spawn state.");
    }

    if (context.NearbyNotices.Single() != "alice spawned at x=10, y=20")
    {
        throw new InvalidOperationException("/spawn did not notify nearby players.");
    }
}

static async Task RunSpawnRequiresAuthenticationCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/spawn"));

    if (!handled || context.SentMessages.Single().Text != "You must login before spawning.")
    {
        throw new InvalidOperationException("/spawn should explain that the player must login first.");
    }

    if (context.Connection.Session.IsSpawned || context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("Rejected anonymous /spawn should not change or broadcast spawn state.");
    }
}

static async Task RunDuplicateSpawnCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/spawn"));

    if (!handled || context.SentMessages.Single().Text != "You are already spawned.")
    {
        throw new InvalidOperationException("Duplicate /spawn should explain that the player is already spawned.");
    }

    if (!context.Connection.Session.IsSpawned || context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("Duplicate /spawn should keep state without broadcasting.");
    }
}

static async Task RunDespawnCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.MoveTo(new WorldPosition(10, 20));
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/despawn"));

    if (!handled || context.SentMessages.Single().Text != "Despawned from x=10, y=20")
    {
        throw new InvalidOperationException("/despawn did not return the expected notice.");
    }

    if (context.Connection.Session.IsSpawned)
    {
        throw new InvalidOperationException("/despawn did not update the player session spawn state.");
    }

    if (context.NearbyNotices.Single() != "alice despawned from x=10, y=20")
    {
        throw new InvalidOperationException("/despawn did not notify nearby players.");
    }
}

static async Task RunDespawnWhenNotSpawnedCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/despawn"));

    if (!handled || context.SentMessages.Single().Text != "You are not spawned.")
    {
        throw new InvalidOperationException("/despawn should explain when the player is not spawned.");
    }

    if (context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("/despawn should not notify nearby players when already not spawned.");
    }
}

static async Task RunJoinCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/join study"));

    if (!handled || context.MovedRooms.Single() != "study")
    {
        throw new InvalidOperationException("/join did not request a room move.");
    }
}

static async Task RunMissingJoinRoomCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/join"));

    if (!handled || context.SentMessages.Single().Text != "Usage: /join <room>")
    {
        throw new InvalidOperationException("Missing /join room did not return the expected usage notice.");
    }
}

static async Task RunLeaveCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.MoveToRoom("study");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/leave"));

    if (!handled || context.MovedRooms.Single() != "lobby")
    {
        throw new InvalidOperationException("/leave did not request a move to lobby.");
    }
}

static async Task RunInvalidRoomNameCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/join bad room"));

    if (!handled || context.MovedRooms.Count != 0)
    {
        throw new InvalidOperationException("Invalid room name should not move the client.");
    }

    if (!context.SentMessages.Single().Text.Contains("Room name can contain only"))
    {
        throw new InvalidOperationException("Invalid room name did not return the expected notice.");
    }
}

static async Task RunRoomUsersCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.MoveToRoom("study");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/room-users"));

    if (!handled || context.SentMessages.Single().Text != "Users in study (1): alice")
    {
        throw new InvalidOperationException("/room-users did not report users in the current room.");
    }
}

static async Task RunStatsCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.MoveToRoom("study");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/stats"));

    if (!handled || context.SentMessages.Single().Text != "Stats: users=2, rooms=2, current-room-users=1")
    {
        throw new InvalidOperationException("/stats did not return the expected summary.");
    }
}

static async Task RunMotdCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/motd"));

    if (!handled || context.SentMessages.Single().Text != ServerInfo.MessageOfTheDay)
    {
        throw new InvalidOperationException("/motd did not return the expected message.");
    }
}

static async Task RunVersionCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/version"));

    if (!handled || context.SentMessages.Single().Text != ServerInfo.VersionMessage)
    {
        throw new InvalidOperationException("/version did not return the expected message.");
    }
}

static async Task RunMeCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/me waves"));

    if (!handled || context.BroadcastMessages.Single().Text != "* alice waves")
    {
        throw new InvalidOperationException("/me did not broadcast the expected action message.");
    }
}

static async Task RunEmptyMeCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/me   "));

    if (!handled || context.SentMessages.Single().Text != "Usage: /me <action>")
    {
        throw new InvalidOperationException("Empty /me did not return the expected usage notice.");
    }
}

static async Task RunMissingMeActionCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/me"));

    if (!handled || context.SentMessages.Single().Text != "Usage: /me <action>")
    {
        throw new InvalidOperationException("Missing /me action did not return the expected usage notice.");
    }
}

static async Task RunWhisperCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/whisper bob hello"));

    if (!handled || context.SentMessages.Count != 2)
    {
        throw new InvalidOperationException("/whisper should send one notice to the target and one to the sender.");
    }

    if (context.SentMessages[0].Connection != context.TargetConnection ||
        context.SentMessages[0].Text != "whisper from alice: hello")
    {
        throw new InvalidOperationException("/whisper did not send the expected target notice.");
    }

    if (context.SentMessages[1].Connection != context.Connection ||
        context.SentMessages[1].Text != "whisper to bob: hello")
    {
        throw new InvalidOperationException("/whisper did not send the expected sender confirmation.");
    }
}

static async Task RunWhisperUnknownUserCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/whisper clara hello"));

    if (!handled || context.SentMessages.Single().Text != "User not found: clara")
    {
        throw new InvalidOperationException("/whisper did not report an unknown target user.");
    }
}

static async Task RunInvalidWhisperCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/whisper bob"));

    if (!handled || context.SentMessages.Single().Text != "Usage: /whisper <nickname> <message>")
    {
        throw new InvalidOperationException("Invalid /whisper did not return the expected usage notice.");
    }
}

static async Task RunMissingWhisperPayloadCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/whisper"));

    if (!handled || context.SentMessages.Single().Text != "Usage: /whisper <nickname> <message>")
    {
        throw new InvalidOperationException("Missing /whisper payload did not return the expected usage notice.");
    }
}

static async Task RunRenameCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    TextWriter originalOutput = Console.Out;
    using var capturedOutput = new StringWriter();

    bool handled;
    try
    {
        Console.SetOut(capturedOutput);
        handled = await context.Handler.TryHandleAsync(
            context.Connection,
            new NetworkMessage(MessageType.Command, "/rename clara"));
    }
    finally
    {
        Console.SetOut(originalOutput);
    }

    if (!handled || context.Connection.Name != "clara")
    {
        throw new InvalidOperationException("/rename did not rename the client.");
    }

    if (context.BroadcastNotices.Single() != "alice is now clara")
    {
        throw new InvalidOperationException("/rename did not broadcast the expected notice.");
    }

    if (!capturedOutput.ToString().Contains("[server] alice is now clara"))
    {
        throw new InvalidOperationException("/rename did not log the expected rename message.");
    }
}

static async Task RunMissingNameCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/name"));

    if (!handled || context.SentMessages.Single().Text != "Usage: /name <nickname>")
    {
        throw new InvalidOperationException("Missing /name nickname did not return the expected usage notice.");
    }
}

static async Task RunMissingRenameCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/rename"));

    if (!handled || context.SentMessages.Single().Text != "Usage: /rename <nickname>")
    {
        throw new InvalidOperationException("Missing /rename nickname did not return the expected usage notice.");
    }
}

static async Task RunDuplicateNameCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.DuplicateName = "bob";

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/name bob"));

    if (!handled || context.Connection.Name != "alice")
    {
        throw new InvalidOperationException("Duplicate /name should not rename the client.");
    }

    if (context.SentMessages.Single().Text != "Nickname is already in use: bob")
    {
        throw new InvalidOperationException("Duplicate /name did not return the expected notice.");
    }
}

static async Task RunInvalidNameCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/name bad name"));

    if (!handled || context.Connection.Name != "alice")
    {
        throw new InvalidOperationException("Invalid /name should not rename the client.");
    }

    if (context.SentMessages.Single().Text != "Nickname can contain only letters, numbers, '-' and '_'.")
    {
        throw new InvalidOperationException("Invalid /name did not return the expected notice.");
    }
}

static async Task AssertThrowsAsync<TException>(Func<Task> action, string failureMessage)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(failureMessage);
}

sealed class NetworkPair : IAsyncDisposable
{
    private readonly TcpListener listener;

    public TcpClient Client { get; }

    public TcpClient Server { get; }

    public NetworkStream ClientStream { get; }

    public NetworkStream ServerStream { get; }

    private NetworkPair(TcpListener listener, TcpClient client, TcpClient server)
    {
        this.listener = listener;
        Client = client;
        Server = server;
        ClientStream = client.GetStream();
        ServerStream = server.GetStream();
    }

    public static async Task<NetworkPair> ConnectAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var client = new TcpClient();
        Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();

        await client.ConnectAsync(IPAddress.Loopback, port);
        TcpClient server = await acceptTask;

        return new NetworkPair(listener, client, server);
    }

    public async ValueTask DisposeAsync()
    {
        await ClientStream.DisposeAsync();
        await ServerStream.DisposeAsync();
        Client.Dispose();
        Server.Dispose();
        listener.Stop();
    }
}

sealed record SentMessage(ClientConnection Connection, MessageType Type, string Text);

sealed record BroadcastMessage(ClientConnection Connection, string Text);

sealed class CommandHandlerTestContext : IAsyncDisposable
{
    private readonly NetworkPair pair;

    public ClientConnection Connection { get; }

    public ClientConnection TargetConnection { get; }

    public ChatCommandHandler Handler { get; }

    public List<SentMessage> SentMessages { get; } = new();

    public List<BroadcastMessage> BroadcastMessages { get; } = new();

    public List<string> BroadcastNotices { get; } = new();

    public List<string> NearbyNotices { get; } = new();

    public List<string> MovedRooms { get; } = new();

    public string? DuplicateName { get; set; }

    public DateTimeOffset CurrentTime { get; set; } = DateTimeOffset.UnixEpoch;

    public DateTimeOffset ServerStartedAt { get; set; } = DateTimeOffset.UnixEpoch;

    private CommandHandlerTestContext(NetworkPair pair, string name)
    {
        this.pair = pair;
        Connection = new ClientConnection(name, pair.Client, pair.ClientStream);
        TargetConnection = new ClientConnection("bob", pair.Server, pair.ServerStream);

        Handler = new ChatCommandHandler(
            SendToClientAsync,
            BroadcastNoticeAsync,
            BroadcastChatAsync,
            BroadcastNearbyNoticeAsync,
            () => ["alice", "bob"],
            () => ["lobby", "study"],
            roomName => roomName == "study" ? ["alice"] : [],
            _ => ["alice", "bob"],
            GetNearbyNames,
            GetNearbySnapshots,
            IsNameInUse,
            FindClientByName,
            MoveClientToRoomAsync,
            () => CurrentTime,
            () => ServerStartedAt,
            new MovementRequestQueue());
    }

    public static async Task<CommandHandlerTestContext> CreateAsync(string name)
    {
        NetworkPair pair = await NetworkPair.ConnectAsync();
        return new CommandHandlerTestContext(pair, name);
    }

    public async ValueTask DisposeAsync()
    {
        await pair.DisposeAsync();
    }

    private Task SendToClientAsync(ClientConnection connection, MessageType type, string text)
    {
        SentMessages.Add(new SentMessage(connection, type, text));
        return Task.CompletedTask;
    }

    private Task BroadcastChatAsync(ClientConnection connection, string text)
    {
        BroadcastMessages.Add(new BroadcastMessage(connection, text));
        return Task.CompletedTask;
    }

    private Task BroadcastNoticeAsync(string text)
    {
        BroadcastNotices.Add(text);
        return Task.CompletedTask;
    }

    private Task BroadcastNearbyNoticeAsync(ClientConnection connection, string text)
    {
        NearbyNotices.Add(text);
        return Task.CompletedTask;
    }

    private bool IsNameInUse(string name, ClientConnection currentConnection)
    {
        return string.Equals(name, DuplicateName, StringComparison.OrdinalIgnoreCase);
    }

    private ClientConnection? FindClientByName(string name)
    {
        return string.Equals(name, TargetConnection.Name, StringComparison.OrdinalIgnoreCase)
            ? TargetConnection
            : null;
    }

    private string[] GetNearbyNames(ClientConnection connection)
    {
        return TargetConnection.Session.IsSpawned &&
            connection.Session.MapId == TargetConnection.Session.MapId &&
            WorldRules.IsNearby(connection.Session.Position, TargetConnection.Session.Position)
            ? [TargetConnection.Name]
            : [];
    }

    private NearbySnapshotResult GetNearbySnapshots(ClientConnection connection)
    {
        NearbyPlayerSnapshot[] snapshots = TargetConnection.Session.IsSpawned &&
            connection.Session.MapId == TargetConnection.Session.MapId &&
            WorldRules.IsNearby(connection.Session.Position, TargetConnection.Session.Position)
            ? [new NearbyPlayerSnapshot(
                TargetConnection.Name,
                TargetConnection.Session.PlayerId,
                TargetConnection.Session.MapId,
                TargetConnection.Session.Position,
                WorldRules.GetDistance(connection.Session.Position, TargetConnection.Session.Position))]
            : [];

        return new NearbySnapshotResult(snapshots, snapshots.Length);
    }

    private Task MoveClientToRoomAsync(ClientConnection connection, string roomName)
    {
        MovedRooms.Add(roomName);
        connection.MoveToRoom(roomName);
        return Task.CompletedTask;
    }
}
