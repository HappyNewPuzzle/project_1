# SocketStudy

C# TCP 소켓 서버를 공부하기 위한 가장 작은 실습 프로젝트입니다.

이 프로젝트는 하나의 콘솔 앱 안에 두 가지 실행 모드를 둡니다.

- `server`: TCP 서버를 열고 클라이언트 접속을 기다립니다.
- `client`: 서버에 접속해서 메시지를 보내고 응답을 받습니다.

## 준비

.NET SDK가 필요합니다. 이 환경에서는 현재 `dotnet` 명령을 찾을 수 없었습니다.

설치 후 새 터미널에서 확인합니다.

```powershell
dotnet --version
```

루트 폴더에서는 앱과 테스트 프로젝트를 한 번에 빌드할 수 있습니다.

```powershell
dotnet build SocketStudy.slnx
```

## 실행

첫 번째 터미널에서 서버를 실행합니다.

```powershell
cd SocketStudy
dotnet run -- server
```

두 번째 터미널에서 클라이언트를 실행합니다.

```powershell
cd SocketStudy
dotnet run -- client
```

세 번째 터미널에서 다른 클라이언트를 하나 더 실행하면 채팅 broadcast를 확인할 수 있습니다.

```powershell
cd SocketStudy
dotnet run -- client 5000 bob
```

닉네임을 지정하고 싶으면 클라이언트 실행 시 포트 뒤에 이름을 붙입니다.

```powershell
dotnet run -- client 5000 alice
```

다른 PC의 서버에 접속하려면 host, port, nickname 순서로 입력합니다.

```powershell
dotnet run -- client 192.168.0.10 5000 alice
```

접속 후에도 `/name` 명령으로 닉네임을 바꿀 수 있습니다.

```text
> /name bob
```

닉네임은 20자 이하이어야 하며, 이미 접속 중인 다른 클라이언트와 같은 이름은 사용할 수 없습니다.

현재 접속자 목록을 보고 싶으면 `/users`를 입력합니다.

```text
> /users
< [notice] Online users (2): alice, bob
```

사용 가능한 명령 목록을 보고 싶으면 `/help`를 입력합니다.

```text
> /help
< [notice] Commands: /help, /name <nickname>, /users, /rooms, /room-users, /join <room>, /where, /ping, /time, /uptime, /me <action>, /whisper <nickname> <message>, /quit
```

처음 접속하면 기본 채팅방 `lobby`에 들어갑니다. 다른 방으로 이동하려면 `/join`을 입력합니다.

```text
> /join study
< [notice] Joined room: study
```

방 이름은 20자 이하이며 영문, 숫자, `-`, `_`만 사용할 수 있습니다.

현재 존재하는 방 목록은 `/rooms`로 확인합니다.

```text
> /rooms
< [notice] Rooms (2): lobby, study
```

현재 방의 접속자 목록은 `/room-users`로 확인합니다.

```text
> /room-users
< [notice] Users in study (2): alice, bob
```

현재 내가 있는 방은 `/where`로 확인합니다.

```text
> /where
< [notice] Current room: study
```

서버가 바로 응답하는지 확인하려면 `/ping`을 입력합니다.

```text
> /ping
< [notice] pong
```

서버 시간을 확인하려면 `/time`을 입력합니다.

```text
> /time
< [notice] Server time: 2026-06-24 10:30:00 +09:00
```

서버가 켜져 있었던 시간을 확인하려면 `/uptime`을 입력합니다.

```text
> /uptime
< [notice] Server uptime: 00:05:07
```

행동 메시지를 보내려면 `/me`를 입력합니다.

```text
> /me waves
< [chat] * alice waves
```

특정 사용자에게만 메시지를 보내려면 `/whisper`를 입력합니다.

```text
> /whisper bob 안녕하세요
< [notice] whisper to bob: 안녕하세요
```

명시적으로 나가고 싶으면 `/quit`을 입력합니다.

```text
> /quit
< [notice] Goodbye.
```

서버는 `Ctrl+C`를 누르면 새 접속 받기를 멈추고 현재 클라이언트 연결을 닫은 뒤 종료합니다.

서버 로그는 콘솔과 실행 파일 기준 `logs/socket-study.log` 파일에 함께 기록됩니다.

포트 번호를 바꾸고 싶으면 `server`와 `client` 뒤에 같은 포트 번호를 붙입니다.

```powershell
dotnet run -- server 6000
dotnet run -- client 6000
```

클라이언트에서 글을 입력하면 서버가 모든 클라이언트에게 `[chat]` 메시지로 전달합니다.

```text
< [chat] 127.0.0.1:53210: hello
```

클라이언트를 두 개 이상 실행하면 새 클라이언트가 들어오거나 나갈 때 기존 클라이언트에게 `[notice]` 메시지가 전달됩니다.

```text
< [notice] 127.0.0.1:53210 joined. Online clients: 2
```

## 실습 시나리오

터미널 3개를 열고 아래 순서대로 따라 해봅니다.

1. 서버 실행: `dotnet run -- server 5000`
2. 첫 번째 클라이언트 실행: `dotnet run -- client 5000 alice`
3. 두 번째 클라이언트 실행: `dotnet run -- client 5000 bob`
4. `alice` 터미널에서 `hello` 입력
5. `bob` 터미널에도 `[chat] [lobby] alice: hello`가 보이는지 확인
6. 아무 클라이언트에서 `/users` 입력
7. 한 클라이언트에서 `/quit` 입력
8. 서버 터미널에서 `Ctrl+C`로 종료

## 코드에서 먼저 볼 부분

파일은 역할별로 나눠져 있습니다.

- `Program.cs`: 실행 모드 선택
- `ChatServer.cs`: 서버 accept loop, 클라이언트 처리, broadcast
- `ChatClient.cs`: 서버 접속, 사용자 입력, 서버 메시지 수신
- `ChatCommandHandler.cs`: `/help`, `/name`, `/users`, `/quit` 명령 처리
- `ClientConnection.cs`: 클라이언트 한 명의 연결 정보와 전송 lock
- `ClientRegistry.cs`: 서버 전체의 접속자 목록 관리
- `MessageProtocol.cs`: 4바이트 길이 + UTF-8 본문 protocol

`TcpListener`는 서버 소켓 역할을 합니다.

```csharp
var listener = new TcpListener(IPAddress.Any, port);
listener.Start();
```

`AcceptTcpClientAsync()`는 클라이언트가 접속할 때까지 기다립니다.

```csharp
TcpClient client = await listener.AcceptTcpClientAsync();
```

각 클라이언트는 별도 작업으로 처리합니다.

```csharp
_ = HandleClientAsync(client);
```

`NetworkStream`은 소켓으로 주고받는 바이트 흐름입니다. TCP에는 메시지 경계가 없기 때문에 이 프로젝트는 직접 메시지 protocol을 만듭니다.

```csharp
await MessageProtocol.WriteMessageAsync(stream, MessageType.Chat, message);
```

현재 protocol은 아래 순서로 데이터를 보냅니다.

1. 메시지 타입 1바이트
2. 메시지 본문 길이 4바이트
3. UTF-8로 인코딩한 메시지 본문

받는 쪽은 먼저 타입과 길이를 읽고, 그 길이만큼 본문을 다시 읽습니다.

## 테스트

protocol round-trip 테스트는 별도 콘솔 프로젝트로 실행합니다.

```powershell
dotnet run --project ../SocketStudy.ProtocolTests/SocketStudy.ProtocolTests.csproj
```

## 다음 학습 단계

1. 서버와 클라이언트 로직을 클래스로 더 분리하기
2. 접속자 이름 중복 방지하기
3. 채팅방 room 개념 추가하기
