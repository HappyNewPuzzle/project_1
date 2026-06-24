# C# SocketStudy 학습 노트

작성일: 2026-06-23

이 문서는 오늘 만든 `SocketStudy` 프로젝트를 공부하기 위한 정리 노트입니다. 단순히 최종 코드만 보는 것이 아니라, 어떤 순서로 기능이 커졌고 각 단계에서 어떤 개념을 배워야 하는지 따라갈 수 있게 정리했습니다.

## 1. 프로젝트 목표

처음 목표는 C#으로 TCP 소켓 서버를 직접 만들어보는 것이었습니다.

최종적으로는 아래 기능을 가진 작은 채팅 서버가 되었습니다.

- TCP 서버와 TCP 클라이언트 실행
- 여러 클라이언트 동시 접속
- 접속/종료 공지
- 전체 채팅 broadcast
- 4바이트 길이 기반 protocol
- 메시지 타입 분리
- 닉네임 설정
- `/help`, `/name`, `/users`, `/quit` 명령
- graceful shutdown
- 서버 로그 파일 기록
- protocol 테스트 프로젝트
- 서버/클라이언트/프로토콜/옵션 파싱 코드 분리

## 2. 현재 실행 방법

루트 폴더에서 전체 빌드:

```powershell
dotnet build SocketStudy.slnx
```

서버 실행:

```powershell
cd SocketStudy
dotnet run -- server 5000
```

클라이언트 실행:

```powershell
cd SocketStudy
dotnet run -- client 5000 alice
```

다른 PC의 서버에 접속:

```powershell
dotnet run -- client 192.168.0.10 5000 alice
```

테스트 실행:

```powershell
dotnet run --project SocketStudy.ProtocolTests\SocketStudy.ProtocolTests.csproj
```

## 3. 현재 파일 구조

```text
SocketStudy/
  Program.cs
  ChatServer.cs
  ChatClient.cs
  ClientConnection.cs
  ServerState.cs
  MessageProtocol.cs
  MessageType.cs
  NetworkMessage.cs
  CommandLineOptions.cs
  AppLogger.cs
  README.md

SocketStudy.ProtocolTests/
  Program.cs
  SocketStudy.ProtocolTests.csproj

SocketStudy.slnx
```

각 파일의 역할은 아래와 같습니다.

| 파일 | 역할 |
| --- | --- |
| `Program.cs` | 실행 인자를 보고 server/client 모드를 선택하는 입구 |
| `ChatServer.cs` | TCP 서버 실행, 클라이언트 접속 처리, 채팅 명령 처리 |
| `ChatClient.cs` | 서버 접속, 사용자 입력, 서버 메시지 수신 |
| `ClientConnection.cs` | 클라이언트 한 명의 연결 정보와 전송 lock |
| `ServerState.cs` | 현재 접속자 목록과 동기화 lock |
| `MessageProtocol.cs` | TCP 바이트 흐름 위에 메시지 단위를 만드는 protocol |
| `MessageType.cs` | Chat, Notice, Command 메시지 타입 |
| `NetworkMessage.cs` | 타입과 본문을 가진 네트워크 메시지 모델 |
| `CommandLineOptions.cs` | 실행 인자 parsing |
| `AppLogger.cs` | 콘솔과 파일에 서버 로그 기록 |

## 4. 오늘 만든 기능 흐름

### Step 1. 가장 작은 echo 서버

처음에는 `TcpListener`와 `TcpClient`를 사용해 서버와 클라이언트를 만들었습니다.

핵심 개념:

- `TcpListener`: 서버 소켓 역할
- `AcceptTcpClientAsync()`: 클라이언트 접속 대기
- `TcpClient`: 연결된 클라이언트
- `NetworkStream`: 실제 데이터가 오가는 바이트 흐름

처음 구조는 클라이언트가 보낸 문자열을 서버가 다시 돌려주는 echo 서버였습니다.

공부 포인트:

- 서버는 보통 계속 실행되며 접속을 기다립니다.
- 클라이언트가 접속하면 서버는 그 클라이언트와 별도로 통신합니다.
- TCP는 연결 기반입니다.

### Step 2. 포트 번호 인자 받기

처음에는 포트가 `5000`으로 고정되어 있었습니다. 이후 아래처럼 포트를 지정할 수 있게 바꿨습니다.

```powershell
dotnet run -- server 6000
dotnet run -- client 6000
```

공부 포인트:

- 콘솔 프로그램은 `args`로 실행 인자를 받습니다.
- 포트 번호는 `1~65535` 범위여야 합니다.
- 잘못된 입력을 검증하고 사용자에게 사용법을 보여주는 것이 중요합니다.

### Step 3. 여러 클라이언트 관리

서버가 여러 클라이언트를 관리하려면 접속자 목록이 필요합니다.

현재는 `ServerState.Clients`가 접속자 목록입니다.

```csharp
public static readonly List<ClientConnection> Clients = new();
```

여러 클라이언트가 동시에 접속/종료할 수 있기 때문에 lock을 사용합니다.

```csharp
lock (ServerState.Gate)
{
    ServerState.Clients.Add(connection);
}
```

공부 포인트:

- 서버는 여러 작업이 동시에 같은 목록을 읽고 쓸 수 있습니다.
- 이런 공유 데이터는 동기화가 필요합니다.
- lock 안에서는 오래 걸리는 작업이나 `await`를 피하는 것이 좋습니다.

### Step 4. 서버 공지 broadcast

클라이언트가 접속하거나 나가면 다른 클라이언트에게 공지를 보냅니다.

예:

```text
< [notice] alice joined. Online clients: 2
```

공부 포인트:

- broadcast는 여러 클라이언트에게 같은 메시지를 보내는 것입니다.
- 접속자 목록을 복사한 뒤, 복사본에 대해 전송합니다.
- lock 안에서 네트워크 전송을 하지 않는 것이 안전합니다.

### Step 5. 채팅 broadcast

echo 서버에서 채팅 서버로 바뀐 단계입니다.

이전:

```text
client -> server -> same client
```

현재:

```text
client -> server -> all clients
```

채팅 메시지는 모든 클라이언트에게 전달됩니다.

```text
< [chat] alice: hello
```

공부 포인트:

- 서버는 메시지를 받은 뒤 목적지를 결정합니다.
- 채팅방에서는 보낸 사람도 자기 메시지를 다시 받는 방식이 흔합니다.
- 화면에 표시하는 형식과 네트워크로 보내는 데이터 형식은 분리할 수 있습니다.

## 5. TCP에서 메시지 경계 문제

처음에는 `ReadLineAsync()`와 `WriteLineAsync()`를 사용했습니다. 이 방식은 줄바꿈이 메시지의 끝입니다.

하지만 TCP 자체는 메시지 단위가 없습니다. TCP는 바이트 흐름입니다.

즉, 아래처럼 보냈다고 해서:

```text
hello
world
```

받는 쪽에서 반드시 두 번으로 나뉘어 도착한다는 보장이 없습니다. 그래서 직접 protocol을 만들었습니다.

현재 protocol:

```text
[1바이트 메시지 타입][4바이트 본문 길이][UTF-8 본문]
```

예를 들어 채팅 메시지 `"hello"`를 보낸다면:

```text
type: 1 byte
length: 4 bytes
body: 5 bytes
```

공부 포인트:

- TCP는 stream입니다.
- stream 위에 message 개념을 만들려면 protocol이 필요합니다.
- 길이를 먼저 보내면 받는 쪽이 정확히 몇 바이트를 읽어야 하는지 알 수 있습니다.

## 6. MessageProtocol 이해하기

`MessageProtocol.WriteMessageAsync()`는 문자열을 바이트로 바꿔 보냅니다.

흐름:

1. 문자열을 UTF-8 바이트 배열로 변환
2. 메시지 타입을 1바이트로 기록
3. 본문 길이를 4바이트 big-endian으로 기록
4. header 전송
5. body 전송

`MessageProtocol.ReadMessageAsync()`는 반대로 읽습니다.

흐름:

1. header 5바이트 읽기
2. 첫 바이트를 `MessageType`으로 해석
3. 다음 4바이트를 본문 길이로 해석
4. 길이만큼 body 읽기
5. UTF-8 문자열로 변환
6. `NetworkMessage` 반환

중요한 메서드:

```csharp
private static async Task<bool> ReadExactOrEndAsync(...)
```

이 메서드는 TCP stream에서 원하는 크기만큼 정확히 읽기 위해 필요합니다.

공부 포인트:

- `ReadAsync()`는 요청한 바이트 수보다 적게 읽을 수 있습니다.
- 그래서 원하는 길이를 다 채울 때까지 반복해야 합니다.
- 일부만 읽고 연결이 끊기면 protocol 오류입니다.

## 7. 메시지 타입 분리

문자열 앞에 `[chat]`, `[notice]`를 붙이는 방식에서 벗어나 `MessageType`을 추가했습니다.

```csharp
public enum MessageType : byte
{
    Chat = 1,
    Notice = 2,
    Command = 3
}
```

그리고 실제 네트워크 메시지는 아래 모델로 표현합니다.

```csharp
public sealed record NetworkMessage(MessageType Type, string Text);
```

공부 포인트:

- 사람이 보는 표시 형식과 protocol 타입은 다릅니다.
- 타입을 분리하면 나중에 파일 전송, ping, room 이동 같은 기능을 추가하기 쉽습니다.
- enum을 byte로 보내면 protocol 크기가 작고 명확합니다.

## 8. 클라이언트 명령

현재 지원하는 명령:

| 명령 | 설명 |
| --- | --- |
| `/help` | 사용 가능한 명령 목록 보기 |
| `/name <nickname>` | 닉네임 변경 |
| `/users` | 현재 접속자 목록 보기 |
| `/rooms` | 현재 존재하는 채팅방 목록 보기 |
| `/room-users` | 현재 채팅방의 접속자 목록 보기 |
| `/join <room>` | 다른 채팅방으로 이동 |
| `/where` | 현재 내가 속한 채팅방 보기 |
| `/time` | 서버 현재 시간 보기 |
| `/me <action>` | 행동 메시지를 전체 채팅으로 보내기 |
| `/whisper <nickname> <message>` | 특정 사용자에게만 메시지 보내기 |
| `/quit` | 서버에 종료 의사를 보내고 연결 종료 |

명령은 `MessageType.Command`로 전송됩니다.

클라이언트 쪽:

```csharp
MessageType type = input.StartsWith('/') ? MessageType.Command : MessageType.Chat;
```

서버 쪽:

```csharp
if (await TryHandleServerCommandAsync(connection, message))
{
    continue;
}
```

공부 포인트:

- 채팅 메시지와 명령 메시지는 성격이 다릅니다.
- 명령은 broadcast하지 않고 서버가 해석합니다.
- 명령 처리 후에는 일반 채팅 처리로 흘러가지 않도록 `continue`합니다.

## 9. 닉네임 처리

처음 클라이언트 이름은 접속한 IP/포트였습니다.

예:

```text
127.0.0.1:53210
```

이후 `/name` 명령과 실행 인자를 통해 닉네임을 설정할 수 있게 했습니다.

```powershell
dotnet run -- client 5000 alice
```

또한 같은 닉네임을 중복으로 사용할 수 없게 했습니다.

공부 포인트:

- 사용자 표시 이름은 서버 상태입니다.
- 서버가 모든 클라이언트 이름을 알고 있어야 `/users`와 중복 검사도 가능합니다.
- 대소문자만 다른 이름도 같은 이름으로 처리하기 위해 `StringComparison.OrdinalIgnoreCase`를 사용했습니다.

## 10. Graceful Shutdown

`Ctrl+C`를 누르면 바로 프로세스를 죽이지 않고 cancellation token을 취소합니다.

```csharp
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    appCancellation.Cancel();
};
```

서버는 token이 취소되면:

1. accept loop 중단
2. listener 닫기
3. 접속 중인 클라이언트 닫기
4. 서버 종료 로그 출력

공부 포인트:

- 서버는 종료도 하나의 기능입니다.
- 갑자기 죽는 것보다 직접 정리하고 종료하는 편이 좋습니다.
- `CancellationToken`은 비동기 작업을 중단시키는 표준 방식입니다.

## 11. 로그 파일

`AppLogger`는 콘솔과 파일에 동시에 로그를 남깁니다.

로그 파일 위치:

```text
bin/Debug/net8.0/logs/socket-study.log
```

공부 포인트:

- 콘솔 로그는 실시간 확인에 좋습니다.
- 파일 로그는 나중에 문제를 되짚어보기 좋습니다.
- 여러 작업이 동시에 로그를 쓸 수 있으므로 lock으로 보호합니다.

## 12. 테스트 프로젝트

`SocketStudy.ProtocolTests`는 별도 콘솔 프로젝트입니다.

테스트하는 것:

- Chat 메시지 round-trip
- Notice 메시지 round-trip
- Command 메시지 round-trip
- 빈 메시지
- 한글/emoji UTF-8 메시지
- 잘못된 메시지 타입
- 본문이 중간에 끊긴 메시지
- 너무 큰 본문 길이
- command-line option parsing

실행:

```powershell
dotnet run --project SocketStudy.ProtocolTests\SocketStudy.ProtocolTests.csproj
```

공부 포인트:

- 네트워크 코드는 정상 케이스뿐 아니라 실패 케이스도 테스트해야 합니다.
- UTF-8은 글자 수와 바이트 수가 다를 수 있습니다.
- protocol 테스트는 실제 서버 전체를 띄우지 않고도 핵심 규칙을 검증할 수 있습니다.

## 13. 구조 분리

처음에는 `Program.cs`에 거의 모든 코드가 있었습니다.

현재는 역할별로 나눴습니다.

```text
Program.cs
  실행 모드 선택

ChatServer.cs
  서버 실행
  접속자 관리
  명령 처리
  broadcast

ChatClient.cs
  서버 접속
  사용자 입력
  서버 메시지 출력

MessageProtocol.cs
  네트워크 protocol

CommandLineOptions.cs
  실행 인자 parsing
```

공부 포인트:

- 파일 분리는 기능이 아니라 이해를 돕는 구조입니다.
- `Program.cs`는 가능한 얇은 입구로 두면 좋습니다.
- 서버와 클라이언트 로직을 분리하면 이후 기능 확장이 쉬워집니다.

## 14. 오늘 커밋 흐름

아래 순서대로 프로젝트가 발전했습니다.

```text
6e18783 Initial socket study project
96338ee Add explanatory comments to socket study
150aa7a Allow custom socket port
a89d540 Broadcast server notices to clients
162c558 Broadcast chat messages to clients
a636c41 Use length-prefixed message protocol
8c863b6 Add graceful shutdown cancellation
83b8ba6 Add client nicknames
74438a3 Add chat user commands
d2b210f Split socket server support classes
9ae2fa0 Add typed network messages
58c0c9a Write server logs to file
9a14882 Add protocol test project
eb949b4 Add protocol error tests
8b3f984 Allow client host argument
c98b872 Add solution file
32800c8 Expand protocol test coverage
0aff66a Extract command line option parsing
6802650 Add command line option tests
0f93e51 Document chat practice scenario
c0af33b Prevent duplicate nicknames
15a9f80 Add help chat command
6acd390 Extract ChatServer class
294e961 Extract ChatClient class
```

커밋을 공부할 때는 아래 명령이 유용합니다.

```powershell
git show 150aa7a
git show a636c41
git show 9ae2fa0
git show 6acd390
```

추천해서 볼 커밋:

- `150aa7a`: 실행 인자 처리 시작
- `162c558`: echo 서버에서 채팅 broadcast로 바뀐 지점
- `a636c41`: line-based에서 length-prefixed protocol로 바뀐 지점
- `9ae2fa0`: 메시지 타입이 protocol에 들어간 지점
- `6acd390`: 서버 로직이 `ChatServer`로 분리된 지점
- `294e961`: 클라이언트 로직이 `ChatClient`로 분리된 지점

## 15. 직접 해볼 과제

아래 과제를 순서대로 해보면 이해가 빨라집니다.

1. `MessageProtocol.ReadMessageAsync()`에 breakpoint를 걸고 메시지 하나가 어떻게 읽히는지 보기
2. 클라이언트 두 개를 띄우고 `/users` 결과 확인하기
3. 같은 닉네임을 두 번 설정해보고 거부되는지 확인하기
4. `MessageType`에 `Ping = 4`를 추가해보기
5. `/time` 명령이 어느 파일에서 처리되는지 찾아보기
6. `AppLogger` 로그 파일을 열어서 접속/퇴장 기록 확인하기
7. `SocketStudy.ProtocolTests`에 `/help` 명령 parsing 테스트 추가하기

## 16. 다음에 진행하기 좋은 기능

다음 기능으로는 아래가 자연스럽습니다.

1. `ClientRegistry` 클래스로 접속자 목록 관리 분리
2. `ChatCommandHandler` 클래스로 slash command 처리 분리
3. JSON 기반 message body로 protocol 확장
4. 테스트 프로젝트를 xUnit으로 전환
5. 서버 자동 통합 테스트 추가

추천 다음 step은 `ClientRegistry` 분리입니다. 지금 `ChatServer`가 아직 접속자 목록 lock과 검색을 직접 들고 있어서, 이 부분을 클래스로 빼면 서버 코드가 더 읽기 쉬워집니다.

추가로 방 이름은 명령 파싱을 단순하게 유지하기 위해 영문, 숫자, `-`, `_`만 허용하도록 정리했습니다.
