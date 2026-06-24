using System.Net;
using System.Net.Sockets;

// TCP 채팅 서버의 실행과 클라이언트 관리를 담당합니다.
sealed class ChatServer
{
    // 접속자 목록 관리를 전담하는 registry입니다.
    private readonly ClientRegistry clients = new();

    // slash command 처리를 전담하는 handler입니다.
    private readonly ChatCommandHandler commandHandler;

    // 채팅 서버 객체를 초기화합니다.
    public ChatServer()
    {
        // uptime 계산에 사용할 서버 시작 시각을 저장합니다.
        DateTimeOffset serverStartedAt = DateTimeOffset.Now;

        // command handler가 필요한 서버 기능을 함수 형태로 전달합니다.
        commandHandler = new ChatCommandHandler(
            SendToClientAsync,
            message => BroadcastServerNoticeAsync(message),
            BroadcastActionMessageAsync,
            clients.GetNames,
            clients.GetRoomNames,
            clients.GetNamesInRoom,
            clients.IsNameInUse,
            clients.FindByName,
            MoveClientToRoomAsync,
            () => DateTimeOffset.Now,
            () => serverStartedAt);
    }

    // TCP 서버를 실행하는 비동기 메서드입니다.
    public async Task RunAsync(int port, CancellationToken cancellationToken)
    {
        // IPAddress.Any는 이 PC의 모든 네트워크 인터페이스에서 접속을 받겠다는 뜻입니다.
        var listener = new TcpListener(IPAddress.Any, port);

        // 서버 종료 시 listener를 반드시 닫기 위해 try/finally를 사용합니다.
        try
        {
            // 지정한 포트에서 클라이언트 접속을 받을 준비를 시작합니다.
            listener.Start();

            // 서버가 어떤 주소와 포트에서 대기 중인지 콘솔에 출력합니다.
            AppLogger.Info($"[server] Listening on 0.0.0.0:{port}");
            // 실습자가 다음에 실행할 클라이언트 명령을 안내합니다.
            AppLogger.Info($"[server] Open another terminal and run: dotnet run -- client {port}");
            // 종료 방법을 안내합니다.
            AppLogger.Info("[server] Press Ctrl+C to stop the server.");

            // 서버는 종료 요청이 오기 전까지 계속 접속을 기다립니다.
            while (!cancellationToken.IsCancellationRequested)
            {
                // 클라이언트가 접속할 때까지 비동기로 기다렸다가, 접속하면 TcpClient 객체를 받습니다.
                TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
                // 각 클라이언트를 별도 작업으로 처리해서 다음 클라이언트 접속도 계속 받을 수 있게 합니다.
                _ = HandleClientAsync(client, cancellationToken);
            }
        }
        // Ctrl+C로 cancellationToken이 취소되면 accept 대기가 OperationCanceledException을 던질 수 있습니다.
        catch (OperationCanceledException)
        {
            // 정상 종료 흐름이므로 에러로 취급하지 않습니다.
            AppLogger.Info("[server] Accept loop stopped.");
        }
        // 성공/실패와 관계없이 서버 socket을 닫고 접속 중인 클라이언트를 정리합니다.
        finally
        {
            // 더 이상 새 접속을 받지 않도록 listener를 닫습니다.
            listener.Stop();
            // 현재 접속 중인 모든 클라이언트 연결을 닫습니다.
            CloseAllClients();
            // 서버 종료 완료를 콘솔에 출력합니다.
            AppLogger.Info("[server] Server stopped.");
        }
    }

    // 접속한 클라이언트 한 명과 메시지를 주고받는 비동기 메서드입니다.
    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        // 접속한 클라이언트의 IP와 포트 정보를 로그로 남기기 위해 가져옵니다.
        IPEndPoint? remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
        // 클라이언트 이름으로 사용할 문자열을 만듭니다.
        string clientName = remoteEndPoint?.ToString() ?? "unknown-client";
        // 클라이언트가 접속했다는 사실을 서버 콘솔에 출력합니다.
        AppLogger.Info($"[server] Client connected: {clientName}");

        // TcpClient에서 실제 데이터를 읽고 쓰는 NetworkStream을 가져옵니다.
        await using NetworkStream stream = client.GetStream();

        // 서버가 접속자 목록에서 관리할 클라이언트 연결 정보를 만듭니다.
        var connection = new ClientConnection(clientName, client, stream);
        // 현재 클라이언트를 서버의 접속자 목록에 추가합니다.
        AddClient(connection);
        // 새로 접속한 클라이언트 본인에게 환영 메시지와 현재 접속자 수를 알려줍니다.
        await SendToClientAsync(connection, MessageType.Notice, $"Welcome, {clientName}. Room: {connection.RoomName}. Online clients: {clients.Count}");
        // 기존 클라이언트들에게 새 클라이언트가 들어왔다는 서버 공지를 보냅니다.
        await BroadcastRoomNoticeAsync(connection.RoomName, $"{clientName} joined {connection.RoomName}. Online clients: {clients.Count}", except: connection);

        // 네트워크 연결은 중간에 끊길 수 있으므로 예외 처리를 준비합니다.
        try
        {
            // 클라이언트가 연결을 유지하는 동안 메시지를 계속 읽습니다.
            while (true)
            {
                // 클라이언트가 보낸 protocol 메시지 하나를 비동기로 읽습니다.
                NetworkMessage? message = await MessageProtocol.ReadMessageAsync(stream, cancellationToken);
                // null은 상대방이 메시지 시작 전에 연결을 정상 종료했다는 뜻입니다.
                if (message is null)
                {
                    // 메시지 읽기 반복을 종료합니다.
                    break;
                }

                // 서버 명령이면 채팅 broadcast 대신 명령을 처리합니다.
                if (await commandHandler.TryHandleAsync(connection, message))
                {
                    // 명령 처리가 끝났으므로 다음 메시지를 기다립니다.
                    continue;
                }

                // 서버 콘솔에 누가 어떤 채팅 메시지를 보냈는지 기록합니다.
                AppLogger.Info($"[server] Chat from {connection.Name}: {message.Text}");
                // 받은 메시지를 접속 중인 모든 클라이언트에게 채팅 메시지로 보냅니다.
                await BroadcastChatMessageAsync(connection, message.Text);
            }
        }
        // 네트워크 입출력 중 연결 끊김 같은 문제가 발생하면 IOException이 날 수 있습니다.
        catch (IOException ex)
        {
            // 서버가 죽지 않도록 에러 내용을 로그로만 남깁니다.
            AppLogger.Error($"[server] Connection error: {ex.Message}");
        }
        // /quit 명령이나 서버 정리 과정에서 stream이 먼저 닫힐 수 있습니다.
        catch (ObjectDisposedException)
        {
            // 이미 닫힌 연결이므로 정상적인 연결 종료 흐름으로 처리합니다.
            AppLogger.Info($"[server] Connection closed: {connection.Name}");
        }
        // 서버 종료 요청 때문에 읽기 작업이 취소될 수 있습니다.
        catch (OperationCanceledException)
        {
            // 정상 종료 흐름이므로 에러로 취급하지 않습니다.
            AppLogger.Info($"[server] Connection canceled: {clientName}");
        }
        // 성공/실패와 관계없이 마지막 정리 작업을 수행합니다.
        finally
        {
            // 현재 클라이언트를 서버의 접속자 목록에서 제거합니다.
            RemoveClient(connection);
            // 클라이언트 소켓을 닫아서 운영체제 리소스를 반납합니다.
            client.Close();
            // 남아 있는 클라이언트들에게 이 클라이언트가 나갔다는 서버 공지를 보냅니다.
            await BroadcastRoomNoticeAsync(connection.RoomName, $"{connection.Name} left {connection.RoomName}. Online clients: {clients.Count}");
            // 클라이언트 연결이 종료되었다는 사실을 서버 콘솔에 출력합니다.
            AppLogger.Info($"[server] Client disconnected: {connection.Name}");
        }
    }

    // 채팅 메시지를 접속 중인 모든 클라이언트에게 보내는 메서드입니다.
    private async Task BroadcastChatMessageAsync(ClientConnection sender, string message)
    {
        // lock 안에서 await를 하지 않기 위해 먼저 보낼 대상 목록의 복사본을 만듭니다.
        ClientConnection[] targets = clients.SnapshotRoom(sender.RoomName);

        // 클라이언트 화면에 표시할 채팅 메시지 형식을 만듭니다.
        string chatMessage = $"[{sender.RoomName}] {sender.Name}: {message}";

        // 복사해 둔 클라이언트 목록을 돌면서 채팅 메시지를 보냅니다.
        foreach (ClientConnection client in targets)
        {
            // 보낸 사람을 포함한 모든 접속자에게 같은 채팅 메시지를 전달합니다.
            await SendToClientAsync(client, MessageType.Chat, chatMessage);
        }
    }

    // 행동 메시지를 접속 중인 모든 클라이언트에게 보내는 메서드입니다.
    private async Task BroadcastActionMessageAsync(ClientConnection sender, string message)
    {
        // lock 안에서 await를 하지 않기 위해 먼저 보낼 대상 목록의 복사본을 만듭니다.
        ClientConnection[] targets = clients.SnapshotRoom(sender.RoomName);

        // 복사해 둔 클라이언트 목록을 돌면서 행동 메시지를 보냅니다.
        foreach (ClientConnection client in targets)
        {
            // 행동 메시지는 이미 화면에 표시할 문장을 갖고 있으므로 그대로 전달합니다.
            await SendToClientAsync(client, MessageType.Chat, message);
        }
    }

    // 현재 클라이언트를 접속자 목록에 추가하는 메서드입니다.
    private void AddClient(ClientConnection connection)
    {
        // 접속자 목록에 새 연결을 추가하고 현재 인원 수를 받습니다.
        int count = clients.Add(connection);

        // 서버 콘솔에 현재 접속자 수를 출력합니다.
        AppLogger.Info($"[server] Online clients: {count}");
    }

    // 현재 클라이언트를 접속자 목록에서 제거하는 메서드입니다.
    private void RemoveClient(ClientConnection connection)
    {
        // 접속자 목록에서 연결을 제거하고 현재 인원 수를 받습니다.
        int count = clients.Remove(connection);

        // 서버 콘솔에 현재 접속자 수를 출력합니다.
        AppLogger.Info($"[server] Online clients: {count}");
    }

    // 서버 공지를 여러 클라이언트에게 보내는 메서드입니다.
    private async Task BroadcastServerNoticeAsync(string message, ClientConnection? except = null)
    {
        // lock 안에서 await를 하지 않기 위해 먼저 보낼 대상 목록의 복사본을 만듭니다.
        ClientConnection[] targets = clients.Snapshot(except);

        // 복사해 둔 클라이언트 목록을 돌면서 공지 메시지를 보냅니다.
        foreach (ClientConnection client in targets)
        {
            // notice prefix를 붙여서 일반 chat 메시지와 구분합니다.
            await SendToClientAsync(client, MessageType.Notice, message);
        }
    }

    // 특정 채팅방에 서버 공지를 보내는 메서드입니다.
    private async Task BroadcastRoomNoticeAsync(string roomName, string message, ClientConnection? except = null)
    {
        // 해당 방의 접속자 목록 복사본을 가져옵니다.
        ClientConnection[] targets = clients.SnapshotRoom(roomName, except);

        // 복사해 둔 클라이언트 목록을 돌면서 공지 메시지를 보냅니다.
        foreach (ClientConnection client in targets)
        {
            // notice 메시지로 방 공지를 전달합니다.
            await SendToClientAsync(client, MessageType.Notice, message);
        }
    }

    // 클라이언트를 다른 채팅방으로 이동시키는 메서드입니다.
    private async Task MoveClientToRoomAsync(ClientConnection connection, string roomName)
    {
        // 이전 방 이름을 보관합니다.
        string oldRoomName = connection.RoomName;
        // 이미 같은 방이면 이동하지 않습니다.
        if (string.Equals(oldRoomName, roomName, StringComparison.OrdinalIgnoreCase))
        {
            // 보낸 사람에게만 현재 방 안내를 보냅니다.
            await SendToClientAsync(connection, MessageType.Notice, $"You are already in {connection.RoomName}.");
            // 메서드를 종료합니다.
            return;
        }

        // 이전 방에 퇴장 공지를 보냅니다.
        await BroadcastRoomNoticeAsync(oldRoomName, $"{connection.Name} left {oldRoomName}.", except: connection);
        // 연결 객체의 방 이름을 변경합니다.
        connection.MoveToRoom(roomName);
        // 새 방에 입장 공지를 보냅니다.
        await BroadcastRoomNoticeAsync(connection.RoomName, $"{connection.Name} joined {connection.RoomName}.", except: connection);
        // 이동한 본인에게 현재 방을 알려줍니다.
        await SendToClientAsync(connection, MessageType.Notice, $"Joined room: {connection.RoomName}");
    }

    // 클라이언트 한 명에게 메시지를 안전하게 보내는 메서드입니다.
    private async Task SendToClientAsync(ClientConnection connection, MessageType type, string message)
    {
        // 네트워크 전송은 실패할 수 있으므로 예외 처리를 준비합니다.
        try
        {
            // 한 클라이언트에게 여러 작업이 동시에 쓰는 일을 막기 위해 연결 객체의 전송 메서드를 사용합니다.
            await connection.SendAsync(type, message);
        }
        // 클라이언트 연결이 이미 끊긴 경우에는 IOException이 발생할 수 있습니다.
        catch (IOException ex)
        {
            // 서버 전체가 멈추지 않도록 전송 실패만 로그로 남깁니다.
            AppLogger.Error($"[server] Failed to send to {connection.Name}: {ex.Message}");
        }
        // writer나 socket이 이미 정리된 경우에는 ObjectDisposedException이 발생할 수 있습니다.
        catch (ObjectDisposedException)
        {
            // 이미 닫힌 연결이므로 별도 복구 없이 로그만 남깁니다.
            AppLogger.Error($"[server] Failed to send to {connection.Name}: connection closed");
        }
    }

    // 서버가 종료될 때 현재 접속 중인 모든 클라이언트 연결을 닫는 메서드입니다.
    private void CloseAllClients()
    {
        // lock 안에서 오래 작업하지 않기 위해 먼저 접속자 목록 복사본을 만듭니다.
        ClientConnection[] targets = clients.Drain();

        // 복사해 둔 클라이언트 목록을 돌면서 연결을 닫습니다.
        foreach (ClientConnection client in targets)
        {
            // 각 클라이언트 소켓을 닫아 pending read/write를 깨웁니다.
            client.Close();
        }
    }
}
