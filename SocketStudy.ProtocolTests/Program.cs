using System.Net;
using System.Net.Sockets;

await RunProtocolRoundTripTestAsync(MessageType.Chat, "alice: hello");
await RunProtocolRoundTripTestAsync(MessageType.Notice, "Welcome.");
await RunProtocolRoundTripTestAsync(MessageType.Command, "/users");

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
