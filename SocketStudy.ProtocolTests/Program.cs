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
await RunHelpCommandTestAsync();
await RunWhereCommandTestAsync();
await RunJoinCommandTestAsync();
await RunInvalidRoomNameCommandTestAsync();

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

sealed class CommandHandlerTestContext : IAsyncDisposable
{
    private readonly NetworkPair pair;

    public ClientConnection Connection { get; }

    public ChatCommandHandler Handler { get; }

    public List<SentMessage> SentMessages { get; } = new();

    public List<string> MovedRooms { get; } = new();

    private CommandHandlerTestContext(NetworkPair pair, string name)
    {
        this.pair = pair;
        Connection = new ClientConnection(name, pair.Client, pair.ClientStream);

        Handler = new ChatCommandHandler(
            SendToClientAsync,
            _ => Task.CompletedTask,
            (_, _) => Task.CompletedTask,
            () => ["alice", "bob"],
            () => ["lobby", "study"],
            roomName => roomName == "study" ? ["alice"] : [],
            (_, _) => false,
            _ => null,
            MoveClientToRoomAsync);
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

    private Task MoveClientToRoomAsync(ClientConnection connection, string roomName)
    {
        MovedRooms.Add(roomName);
        connection.MoveToRoom(roomName);
        return Task.CompletedTask;
    }
}
