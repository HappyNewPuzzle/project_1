using System.Net;
using System.Net.Sockets;
using System.Text;

const int DefaultPort = 5000;

if (args.Length == 0)
{
    PrintUsage();
    return;
}

switch (args[0].ToLowerInvariant())
{
    case "server":
        await RunServerAsync(DefaultPort);
        break;

    case "client":
        await RunClientAsync("127.0.0.1", DefaultPort);
        break;

    default:
        PrintUsage();
        break;
}

static async Task RunServerAsync(int port)
{
    var listener = new TcpListener(IPAddress.Any, port);
    listener.Start();

    Console.WriteLine($"[server] Listening on 0.0.0.0:{port}");
    Console.WriteLine("[server] Open another terminal and run: dotnet run -- client");

    while (true)
    {
        TcpClient client = await listener.AcceptTcpClientAsync();
        _ = HandleClientAsync(client);
    }
}

static async Task HandleClientAsync(TcpClient client)
{
    IPEndPoint? remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
    Console.WriteLine($"[server] Client connected: {remoteEndPoint}");

    await using NetworkStream stream = client.GetStream();
    using var reader = new StreamReader(stream, Encoding.UTF8);
    await using var writer = new StreamWriter(stream, Encoding.UTF8)
    {
        AutoFlush = true
    };

    try
    {
        while (true)
        {
            string? message = await reader.ReadLineAsync();
            if (message is null)
            {
                break;
            }

            Console.WriteLine($"[server] Received: {message}");
            await writer.WriteLineAsync($"echo: {message}");
        }
    }
    catch (IOException ex)
    {
        Console.WriteLine($"[server] Connection error: {ex.Message}");
    }
    finally
    {
        client.Close();
        Console.WriteLine($"[server] Client disconnected: {remoteEndPoint}");
    }
}

static async Task RunClientAsync(string host, int port)
{
    using var client = new TcpClient();
    await client.ConnectAsync(host, port);

    Console.WriteLine($"[client] Connected to {host}:{port}");
    Console.WriteLine("[client] Type a message and press Enter. Empty line exits.");

    await using NetworkStream stream = client.GetStream();
    using var reader = new StreamReader(stream, Encoding.UTF8);
    await using var writer = new StreamWriter(stream, Encoding.UTF8)
    {
        AutoFlush = true
    };

    while (true)
    {
        Console.Write("> ");
        string? input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
        {
            break;
        }

        await writer.WriteLineAsync(input);

        string? response = await reader.ReadLineAsync();
        Console.WriteLine($"< {response}");
    }
}

static void PrintUsage()
{
    Console.WriteLine("SocketStudy");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run -- server");
    Console.WriteLine("  dotnet run -- client");
}
