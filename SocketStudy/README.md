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

클라이언트에서 글을 입력하면 서버가 같은 내용을 `echo:` prefix와 함께 돌려줍니다.

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

`NetworkStream`은 소켓으로 주고받는 바이트 흐름입니다. 여기서는 공부하기 쉽게 문자열 한 줄 단위로 읽고 씁니다.

```csharp
await writer.WriteLineAsync($"echo: {message}");
```

## 다음 학습 단계

1. 포트 번호를 실행 인자로 받기
2. 여러 클라이언트에게 서버 공지 보내기
3. 채팅방처럼 클라이언트 메시지를 전체 broadcast 하기
4. 메시지 길이 기반 protocol 직접 만들기
5. 연결 종료, 예외 처리, cancellation token 추가하기
