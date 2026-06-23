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
