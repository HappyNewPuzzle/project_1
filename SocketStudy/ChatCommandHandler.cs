// slash command 해석과 처리를 담당합니다.
sealed class ChatCommandHandler
{
    // 클라이언트 한 명에게 메시지를 보내는 함수입니다.
    private readonly Func<ClientConnection, MessageType, string, Task> sendToClientAsync;

    // 전체 클라이언트에게 서버 공지를 보내는 함수입니다.
    private readonly Func<string, Task> broadcastNoticeAsync;

    // 현재 접속자 이름 목록을 가져오는 함수입니다.
    private readonly Func<string[]> getClientNames;

    // 특정 이름을 다른 클라이언트가 이미 사용 중인지 확인하는 함수입니다.
    private readonly Func<string, ClientConnection, bool> isNameInUse;

    // 이름으로 클라이언트를 찾는 함수입니다.
    private readonly Func<string, ClientConnection?> findClientByName;

    // 명령 처리에 필요한 서버 기능을 주입받습니다.
    public ChatCommandHandler(
        Func<ClientConnection, MessageType, string, Task> sendToClientAsync,
        Func<string, Task> broadcastNoticeAsync,
        Func<string[]> getClientNames,
        Func<string, ClientConnection, bool> isNameInUse,
        Func<string, ClientConnection?> findClientByName)
    {
        // 클라이언트 개별 전송 함수를 저장합니다.
        this.sendToClientAsync = sendToClientAsync;
        // 전체 공지 함수를 저장합니다.
        this.broadcastNoticeAsync = broadcastNoticeAsync;
        // 접속자 이름 조회 함수를 저장합니다.
        this.getClientNames = getClientNames;
        // 이름 중복 확인 함수를 저장합니다.
        this.isNameInUse = isNameInUse;
        // 이름 기반 클라이언트 조회 함수를 저장합니다.
        this.findClientByName = findClientByName;
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

        // /help 명령은 사용할 수 있는 명령 목록을 보여줍니다.
        if (message.Text.Equals("/help", StringComparison.OrdinalIgnoreCase))
        {
            // 보낸 사람에게만 명령 목록을 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, "Commands: /help, /name <nickname>, /users, /time, /whisper <nickname> <message>, /quit");
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

        // /time 명령은 서버의 현재 시간을 보여줍니다.
        if (message.Text.Equals("/time", StringComparison.OrdinalIgnoreCase))
        {
            // 서버 시간을 ISO-8601 형식으로 만들어 보낸 사람에게만 알려줍니다.
            string serverTime = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz");
            // 서버 시간을 notice 메시지로 전송합니다.
            await sendToClientAsync(connection, MessageType.Notice, $"Server time: {serverTime}");
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
            await sendToClientAsync(sender, MessageType.Notice, "Usage: /whisper <nickname> <message>");
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
        if (requestedName.Length > 20)
        {
            // 보낸 사람에게만 실패 이유를 알려줍니다.
            await sendToClientAsync(connection, MessageType.Notice, "Nickname must be 20 characters or fewer.");
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
