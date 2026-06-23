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

현재 접속자 목록을 보고 싶으면 `/users`를 입력합니다.

```text
> /users
< [notice] Online users (2): alice, bob
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

## 코드에서 먼저 볼 부분

파일은 역할별로 나눠져 있습니다.

- `Program.cs`: 실행 모드 선택, 서버/클라이언트 루프, 채팅 명령 처리
- `ClientConnection.cs`: 클라이언트 한 명의 연결 정보와 전송 lock
- `ServerState.cs`: 서버 전체의 접속자 목록
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

1. protocol 오류 케이스 테스트 추가하기
2. 서버와 클라이언트 host 인자 분리하기
