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
await MessageProtocol.WriteMessageAsync(stream, message);
```

현재 protocol은 아래 순서로 데이터를 보냅니다.

1. 메시지 본문 길이 4바이트
2. UTF-8로 인코딩한 메시지 본문

받는 쪽은 먼저 4바이트 길이를 읽고, 그 길이만큼 본문을 다시 읽습니다.

## 다음 학습 단계

1. 연결 종료, 예외 처리, cancellation token 추가하기
