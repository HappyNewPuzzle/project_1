using System.Net;
using System.Net.Sockets;

// TCP 채팅 서버의 실행과 클라이언트 관리를 담당합니다.
sealed class ChatServer
{
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
        await SendToClientAsync(connection, MessageType.Notice, $"Welcome, {clientName}. Online clients: {GetClientCount()}");
        // 기존 클라이언트들에게 새 클라이언트가 들어왔다는 서버 공지를 보냅니다.
        await BroadcastServerNoticeAsync($"{clientName} joined. Online clients: {GetClientCount()}", except: connection);

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
                if (await TryHandleServerCommandAsync(connection, message))
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
            await BroadcastServerNoticeAsync($"{connection.Name} left. Online clients: {GetClientCount()}");
            // 클라이언트 연결이 종료되었다는 사실을 서버 콘솔에 출력합니다.
            AppLogger.Info($"[server] Client disconnected: {connection.Name}");
        }
    }

    // 서버에서 처리해야 하는 slash command인지 확인하고 처리하는 메서드입니다.
    private async Task<bool> TryHandleServerCommandAsync(ClientConnection connection, NetworkMessage message)
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

        // /help 명령은 사용할 수 있는 명령 목록을 보여줍니다.
        if (message.Text.Equals("/help", StringComparison.OrdinalIgnoreCase))
        {
            // 보낸 사람에게만 명령 목록을 알려줍니다.
            await SendToClientAsync(connection, MessageType.Notice, "Commands: /help, /name <nickname>, /users, /quit");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /users 명령은 현재 접속 중인 클라이언트 이름 목록을 보여줍니다.
        if (message.Text.Equals("/users", StringComparison.OrdinalIgnoreCase))
        {
            // 현재 접속자 이름 목록을 가져옵니다.
            string[] names = GetClientNames();
            // 접속자 목록을 보낸 사람에게만 알려줍니다.
            await SendToClientAsync(connection, MessageType.Notice, $"Online users ({names.Length}): {string.Join(", ", names)}");
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // /quit 명령은 클라이언트가 서버에 연결 종료 의사를 명확히 전달하는 명령입니다.
        if (message.Text.Equals("/quit", StringComparison.OrdinalIgnoreCase))
        {
            // 보낸 사람에게 연결을 종료한다는 안내를 보냅니다.
            await SendToClientAsync(connection, MessageType.Notice, "Goodbye.");
            // 소켓을 닫아서 해당 클라이언트 처리 루프가 끝나도록 만듭니다.
            connection.Close();
            // 명령을 처리했다고 호출자에게 알려줍니다.
            return true;
        }

        // 알 수 없는 명령은 보낸 사람에게만 안내합니다.
        await SendToClientAsync(connection, MessageType.Notice, $"Unknown command: {message.Text}");
        // 명령을 처리했다고 호출자에게 알려줍니다.
        return true;
    }

    // 클라이언트 닉네임을 변경하는 메서드입니다.
    private async Task ChangeClientNameAsync(ClientConnection connection, string requestedName)
    {
        // 닉네임이 비어 있으면 변경하지 않습니다.
        if (string.IsNullOrWhiteSpace(requestedName))
        {
            // 보낸 사람에게만 실패 이유를 알려줍니다.
            await SendToClientAsync(connection, MessageType.Notice, "Nickname cannot be empty.");
            // 메서드를 종료합니다.
            return;
        }

        // 너무 긴 닉네임은 화면을 어지럽히므로 20자로 제한합니다.
        if (requestedName.Length > 20)
        {
            // 보낸 사람에게만 실패 이유를 알려줍니다.
            await SendToClientAsync(connection, MessageType.Notice, "Nickname must be 20 characters or fewer.");
            // 메서드를 종료합니다.
            return;
        }

        // 이미 다른 클라이언트가 쓰고 있는 닉네임이면 변경하지 않습니다.
        if (IsClientNameInUse(requestedName, except: connection))
        {
            // 보낸 사람에게만 실패 이유를 알려줍니다.
            await SendToClientAsync(connection, MessageType.Notice, $"Nickname is already in use: {requestedName}");
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
        await BroadcastServerNoticeAsync($"{oldName} is now {connection.Name}");
    }

    // 채팅 메시지를 접속 중인 모든 클라이언트에게 보내는 메서드입니다.
    private async Task BroadcastChatMessageAsync(ClientConnection sender, string message)
    {
        // lock 안에서 await를 하지 않기 위해 먼저 보낼 대상 목록의 복사본을 만듭니다.
        ClientConnection[] clients;

        // 접속자 목록을 복사하는 동안 다른 작업이 목록을 바꾸지 못하도록 lock으로 보호합니다.
        lock (ServerState.Gate)
        {
            // 현재 접속해 있는 모든 클라이언트를 전송 대상으로 복사합니다.
            clients = ServerState.Clients.ToArray();
        }

        // 클라이언트 화면에 표시할 채팅 메시지 형식을 만듭니다.
        string chatMessage = $"{sender.Name}: {message}";

        // 복사해 둔 클라이언트 목록을 돌면서 채팅 메시지를 보냅니다.
        foreach (ClientConnection client in clients)
        {
            // 보낸 사람을 포함한 모든 접속자에게 같은 채팅 메시지를 전달합니다.
            await SendToClientAsync(client, MessageType.Chat, chatMessage);
        }
    }

    // 현재 클라이언트를 접속자 목록에 추가하는 메서드입니다.
    private void AddClient(ClientConnection connection)
    {
        // 여러 클라이언트 작업이 동시에 목록을 바꾸지 못하도록 lock으로 보호합니다.
        lock (ServerState.Gate)
        {
            // 접속자 목록에 새 연결을 추가합니다.
            ServerState.Clients.Add(connection);
        }

        // 서버 콘솔에 현재 접속자 수를 출력합니다.
        AppLogger.Info($"[server] Online clients: {GetClientCount()}");
    }

    // 현재 클라이언트를 접속자 목록에서 제거하는 메서드입니다.
    private void RemoveClient(ClientConnection connection)
    {
        // 여러 클라이언트 작업이 동시에 목록을 바꾸지 못하도록 lock으로 보호합니다.
        lock (ServerState.Gate)
        {
            // 접속자 목록에서 연결을 제거합니다.
            ServerState.Clients.Remove(connection);
        }

        // 서버 콘솔에 현재 접속자 수를 출력합니다.
        AppLogger.Info($"[server] Online clients: {GetClientCount()}");
    }

    // 현재 접속자 수를 가져오는 메서드입니다.
    private int GetClientCount()
    {
        // 접속자 목록을 읽는 동안 다른 작업이 목록을 바꾸지 못하도록 lock으로 보호합니다.
        lock (ServerState.Gate)
        {
            // 현재 접속자 목록의 개수를 반환합니다.
            return ServerState.Clients.Count;
        }
    }

    // 현재 접속자 이름 목록을 가져오는 메서드입니다.
    private string[] GetClientNames()
    {
        // 접속자 목록을 읽는 동안 다른 작업이 목록을 바꾸지 못하도록 lock으로 보호합니다.
        lock (ServerState.Gate)
        {
            // 현재 접속자 이름만 배열로 복사해서 반환합니다.
            return ServerState.Clients
                .Select(client => client.Name)
                .OrderBy(name => name)
                .ToArray();
        }
    }

    // 특정 이름을 다른 클라이언트가 이미 사용 중인지 확인하는 메서드입니다.
    private bool IsClientNameInUse(string name, ClientConnection except)
    {
        // 접속자 목록을 읽는 동안 다른 작업이 목록을 바꾸지 못하도록 lock으로 보호합니다.
        lock (ServerState.Gate)
        {
            // 자기 자신을 제외하고 같은 이름을 쓰는 클라이언트가 있는지 확인합니다.
            return ServerState.Clients.Any(client =>
                client != except &&
                string.Equals(client.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }

    // 서버 공지를 여러 클라이언트에게 보내는 메서드입니다.
    private async Task BroadcastServerNoticeAsync(string message, ClientConnection? except = null)
    {
        // lock 안에서 await를 하지 않기 위해 먼저 보낼 대상 목록의 복사본을 만듭니다.
        ClientConnection[] clients;

        // 접속자 목록을 복사하는 동안 다른 작업이 목록을 바꾸지 못하도록 lock으로 보호합니다.
        lock (ServerState.Gate)
        {
            // except로 전달된 클라이언트는 공지 대상에서 제외합니다.
            clients = ServerState.Clients
                .Where(client => client != except)
                .ToArray();
        }

        // 복사해 둔 클라이언트 목록을 돌면서 공지 메시지를 보냅니다.
        foreach (ClientConnection client in clients)
        {
            // notice prefix를 붙여서 일반 chat 메시지와 구분합니다.
            await SendToClientAsync(client, MessageType.Notice, message);
        }
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
        ClientConnection[] clients;

        // 접속자 목록을 복사하는 동안 다른 작업이 목록을 바꾸지 못하도록 lock으로 보호합니다.
        lock (ServerState.Gate)
        {
            // 현재 접속자 목록을 배열로 복사합니다.
            clients = ServerState.Clients.ToArray();
            // 서버 종료 중에는 목록을 비워서 중복 정리를 줄입니다.
            ServerState.Clients.Clear();
        }

        // 복사해 둔 클라이언트 목록을 돌면서 연결을 닫습니다.
        foreach (ClientConnection client in clients)
        {
            // 각 클라이언트 소켓을 닫아 pending read/write를 깨웁니다.
            client.Close();
        }
    }
}
