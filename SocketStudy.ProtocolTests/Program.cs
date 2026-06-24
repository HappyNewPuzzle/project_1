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
RunServerPortParseTest();
RunLocalClientOptionParseTest();
RunRemoteClientOptionParseTest();
await RunClientRegistryTracksCountAndNamesAsync();
await RunClientRegistryFindsNamesCaseInsensitiveAsync();
await RunClientRegistryFiltersRoomsAsync();
await RunClientRegistryDrainsConnectionsAsync();
await RunHelpCommandTestAsync();
await RunWhereCommandTestAsync();
await RunPingCommandTestAsync();
await RunTimeCommandTestAsync();
await RunUptimeCommandTestAsync();
await RunJoinCommandTestAsync();
await RunInvalidRoomNameCommandTestAsync();
await RunRoomUsersCommandTestAsync();
await RunMeCommandTestAsync();
await RunWhisperCommandTestAsync();
await RunDuplicateNameCommandTestAsync();

Console.WriteLine("All protocol tests passed.");

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
    if (sent.Type != MessageType.Notice || !sent.Text.Contains("/join <room>"))
    {
        throw new InvalidOperationException("/help output did not include expected command list.");
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
            _ => Task.CompletedTask,
            BroadcastChatAsync,
            () => ["alice", "bob"],
            () => ["lobby", "study"],
            roomName => roomName == "study" ? ["alice"] : [],
            IsNameInUse,
            FindClientByName,
            MoveClientToRoomAsync,
            () => CurrentTime,
            () => ServerStartedAt);
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

    private Task MoveClientToRoomAsync(ClientConnection connection, string roomName)
    {
        MovedRooms.Add(roomName);
        connection.MoveToRoom(roomName);
        return Task.CompletedTask;
    }
}
