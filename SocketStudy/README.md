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

같은 기능을 `/rename`으로도 사용할 수 있습니다.

```text
> /rename bob
```

닉네임은 20자 이하이어야 하며 영문, 숫자, `-`, `_`만 사용할 수 있습니다. 이미 접속 중인 다른 클라이언트와 같은 이름은 사용할 수 없습니다.

내 닉네임과 현재 방을 확인하려면 `/whoami`를 입력합니다.

```text
> /whoami
< [notice] You are alice in room lobby.
```

MMO 확장을 위한 현재 플레이어 세션 상태는 `/session`으로 확인합니다.

```text
> /session
< [notice] Session: player-id=0, state=anonymous, spawn=not-spawned
```

학습용 플레이어 ID를 세션에 연결하려면 `/login`을 입력합니다.
인증된 세션의 플레이어 ID는 다시 로그인하여 바꿀 수 없으며, 스폰 중인 로그인 요청도 거부됩니다.

```text
> /login 1001
< [notice] Logged in as player 1001.
```

플레이어의 학습용 월드 위치는 `/pos`로 확인하고 `/move <sequence> <x> <y>`로 변경합니다.
이동하려면 먼저 `/spawn`으로 월드에 등장한 상태여야 합니다.
한 번의 `/move`는 현재 위치에서 맨해튼 거리 `10` 이하만 허용됩니다.
연속 `/move` 요청은 서버 시간 기준으로 최소 `1초` 간격이 필요합니다.
이동 sequence는 `1`부터 시작해 성공한 요청마다 이전 값보다 커야 합니다.
현재 게임 맵 ID는 `/map`으로 확인합니다.
스폰된 플레이어는 `/warp <mapId> <x> <y>`로 다른 맵의 지정 좌표로 이동할 수 있습니다.

```text
> /pos
< [notice] Position: x=0, y=0

> /map
< [notice] Map: 1

> /spawn
< [notice] Spawned at x=0, y=0

> /move 1 4 6
< [notice] Moved to x=4, y=6

> /warp 2 30 40
< [notice] Warped to map=2, x=30, y=40
```

현재 학습용 월드는 `-100`부터 `100`까지의 x/y 좌표만 허용합니다.

같은 게임 맵에서 시야 거리 안에 있는 플레이어는 `/nearby`로 확인합니다.
요청자와 검색 결과에 포함되는 플레이어는 모두 월드에 스폰된 상태여야 합니다.
채팅방은 메시지 채널이고 게임 맵은 AOI 경계이므로 서로 독립적으로 처리됩니다.

```text
> /nearby
< [notice] Nearby players (1): bob

> /look
< [notice] Nearby snapshots (1): bob[player-id=2002,map=1,x=10, y=10,distance=20]
```

플레이어가 이동하면 주변 플레이어에게만 이동 notice가 전달됩니다.

로그인한 플레이어는 `/spawn`으로 현재 위치에 등장하고 그 사실을 주변 플레이어에게 알릴 수 있습니다.
로그인 전 스폰과 이미 스폰된 상태의 중복 스폰은 서버가 거부합니다.

```text
> /spawn
< [notice] Spawned at x=10, y=20
```

현재 위치에서 플레이어가 사라졌다는 사실은 `/despawn`으로 주변 플레이어에게 알릴 수 있습니다.

```text
> /despawn
< [notice] Despawned from x=10, y=20
```

월드에서 despawn한 뒤 `/logout`으로 인증 정보를 제거하고 익명 세션으로 돌아갈 수 있습니다.
로그아웃하면 학습용 플레이어 ID와 위치가 초기화됩니다.

```text
> /logout
< [notice] Logged out.
```

현재 접속자 목록을 보고 싶으면 `/users`를 입력합니다.

```text
> /users
< [notice] Online users (2): alice, bob
```

사용 가능한 명령 목록을 보고 싶으면 `/help`를 입력합니다.

```text
> /help
< [notice] Commands: /help, /commands, /name <nickname>, /rename <nickname>, /whoami, /session, /login <playerId>, /logout, /pos, /map, /warp <mapId> <x> <y>, /move <sequence> <x> <y>, /nearby, /look, /spawn, /despawn, /users, /rooms, /room-users, /stats, /motd, /version, /join <room>, /leave, /where, /ping, /echo <message>, /time, /uptime, /me <action>, /whisper <nickname> <message>, /quit
```

처음 접속하면 기본 채팅방 `lobby`에 들어갑니다. 다른 방으로 이동하려면 `/join`을 입력합니다.

```text
> /join study
< [notice] Joined room: study
```

방 이름은 20자 이하이며 영문, 숫자, `-`, `_`만 사용할 수 있습니다.

기본 방인 `lobby`로 돌아가려면 `/leave`를 입력합니다.

```text
> /leave
< [notice] Joined room: lobby
```

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

서버 상태 요약은 `/stats`로 확인합니다.

```text
> /stats
< [notice] Stats: users=2, rooms=2, current-room-users=2
```

서버 안내 메시지는 `/motd`로 확인합니다.

```text
> /motd
< [notice] Welcome to SocketStudy. Type /help to see commands.
```

서버 예제 버전은 `/version`으로 확인합니다.

```text
> /version
< [notice] SocketStudy server v1
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

서버가 받은 문장을 그대로 돌려주는지 확인하려면 `/echo`를 입력합니다.

```text
> /echo hello server
< [notice] echo: hello server
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

메시지 본문은 UTF-8 기준 최대 1MB까지 보낼 수 있습니다. 이보다 큰 입력은 클라이언트가 서버로 보내기 전에 안내하고 건너뜁니다.

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
- `ChatCommandHandler.cs`: `/help`, `/join`, `/whisper`, `/motd`, `/version` 같은 slash command 처리
- `ClientConnection.cs`: 클라이언트 한 명의 연결 정보와 전송 lock
- `ClientRegistry.cs`: 서버 전체의 접속자 목록과 방 목록 관리
- `NameRules.cs`: 닉네임과 방 이름에 공통으로 적용되는 문자 규칙
- `ServerInfo.cs`: 서버 이름, 버전, 안내 메시지
- `MessageProtocol.cs`: 4바이트 길이 + UTF-8 본문 protocol
- `SocketStudy.ProtocolTests`: protocol, 명령 처리, registry, option parsing 테스트

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
