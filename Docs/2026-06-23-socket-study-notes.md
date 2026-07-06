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

## 17. 2026-06-24 이어서 진행한 내용

오늘은 기능을 크게 한 번에 바꾸기보다, 작은 step을 계속 쌓으면서 테스트와 구조를 같이 단단하게 만들었습니다.

핵심 흐름은 아래와 같습니다.

1. `ChatCommandHandler` 테스트 추가
2. `ClientRegistry` 테스트 추가
3. `/ping`, `/uptime`, `/whoami`, `/leave`, `/rename` 명령 추가
4. `/commands`를 `/help` 별칭으로 추가
5. 닉네임과 방 이름 규칙을 `NameRules`로 공통화
6. 클라이언트 시작 닉네임도 같은 규칙으로 검증
7. 테스트 중 의도적으로 출력되는 콘솔 메시지를 캡처해서 테스트 출력을 깔끔하게 정리
8. 기본 방 `lobby`를 항상 방 목록에 포함

### 테스트가 늘어난 이유

명령이 많아질수록 직접 실행해서 확인하는 방식만으로는 실수하기 쉽습니다.

그래서 `SocketStudy.ProtocolTests/Program.cs`에 아래 테스트들을 추가했습니다.

- `/help`, `/commands`
- `/where`, `/whoami`
- `/join`, `/leave`
- `/room-users`
- `/me`
- `/whisper`
- `/name`, `/rename`
- 잘못된 닉네임과 방 이름
- `ClientRegistry`의 접속자 목록, 방 목록, 중복 이름 검색, drain 동작

이 테스트들은 실제 서버 전체를 띄우기보다 필요한 객체만 만들어서 명령 처리 결과를 확인합니다.

공부 포인트:

- 테스트하기 쉬운 코드는 보통 의존성을 밖에서 주입받습니다.
- `ChatCommandHandler`는 메시지 전송, 공지 방송, 방 이동 같은 동작을 함수로 주입받습니다.
- 그래서 실제 네트워크 없이도 명령 처리 결과를 검증할 수 있습니다.

### 시간 주입

`/time`과 `/uptime`은 현재 시간이 필요합니다.

처음처럼 코드 안에서 바로 `DateTimeOffset.Now`를 읽으면 테스트할 때 결과가 매번 달라집니다.

그래서 현재 시간을 가져오는 함수를 생성자로 전달하도록 바꿨습니다.

```csharp
Func<DateTimeOffset> getCurrentTime
```

이렇게 하면 실제 서버에서는 현재 시간을 쓰고, 테스트에서는 고정된 시간을 넣을 수 있습니다.

공부 포인트:

- 시간이 들어가는 코드는 테스트가 어려워지기 쉽습니다.
- 현재 시간을 직접 읽는 대신 함수로 주입하면 테스트가 쉬워집니다.

### 이름 규칙 공통화

닉네임과 방 이름은 같은 문자 규칙을 사용합니다.

- 영문
- 숫자
- `-`
- `_`
- 최대 20자

이 규칙을 여러 파일에 복사해두면 나중에 한쪽만 바뀔 수 있습니다.

그래서 `NameRules.cs`를 추가했습니다.

```csharp
public static class NameRules
{
    public const int MaxNameLength = 20;
    public static bool HasOnlyAllowedCharacters(string name) { ... }
}
```

공부 포인트:

- 중복 제거는 단순히 코드를 줄이는 일이 아닙니다.
- 같은 정책을 한 곳에서 관리해서 실수를 줄이는 일입니다.

### 기본 방 lobby

`ClientConnection`의 기본 방은 이제 문자열 `"lobby"`를 직접 쓰지 않고 `ClientRegistry.DefaultRoomName`을 사용합니다.

또한 `/rooms` 결과에는 접속자가 없어도 기본 방이 항상 포함됩니다.

공부 포인트:

- 중요한 문자열을 여러 곳에 직접 쓰면 오타와 불일치가 생깁니다.
- 상수로 빼면 의미가 분명해지고 변경도 쉬워집니다.

### 오늘 추가된 주요 커밋

```text
7f18ae5 Add commands alias
b4eab9d Validate nickname characters
7cb8488 Validate startup nickname options
502ead1 Extract shared name rules
4ee7577 Capture option validation test output
000e91d Show help hint on client start
435c2a8 Keep default room in room list
908add5 Add rename command alias
```

복습 추천 순서:

1. `ChatCommandHandler.cs`에서 `/rename`, `/leave`, `/whoami`가 어떻게 처리되는지 보기
2. `SocketStudy.ProtocolTests/Program.cs`에서 각 명령 테스트가 어떤 값을 검증하는지 보기
3. `NameRules.cs`를 보고 닉네임과 방 이름 규칙이 어디서 재사용되는지 찾기
4. `ClientRegistry.cs`에서 `DefaultRoomName`과 `GetRoomNames()` 흐름 보기

## 18. 2026-06-25 이어서 진행한 내용

오늘은 명령 처리와 테스트 품질을 조금 더 다듬었습니다.

추가된 주요 내용:

- `/echo <message>` 명령 추가
- 빈 `/echo` 입력 테스트 추가
- `/whisper` 실패 케이스 테스트 추가
- 빈 `/me` 입력 테스트 추가
- 명령 사용법 문구를 상수로 정리
- 테스트 성공 문구를 `All socket study tests passed.`로 변경

### /echo 명령

`/echo`는 서버가 받은 문장을 그대로 돌려주는 명령입니다.

```text
> /echo hello server
< [notice] echo: hello server
```

공부 포인트:

- 명령어 뒤쪽의 본문만 잘라내는 방법을 볼 수 있습니다.
- 본문이 비어 있으면 사용법을 돌려주는 흐름을 볼 수 있습니다.
- 서버 왕복이 정상인지 확인하는 간단한 디버그 명령으로도 쓸 수 있습니다.

관련 코드:

- `ChatCommandHandler.cs`: `/echo` 처리
- `SocketStudy.ProtocolTests/Program.cs`: 정상 `/echo`, 빈 `/echo` 테스트
- `SocketStudy/README.md`: 사용 예시

### 실패 케이스 테스트

오늘은 정상 동작뿐 아니라 실패 케이스도 보강했습니다.

추가된 테스트:

- `/echo   ` -> `Usage: /echo <message>`
- `/whisper clara hello` -> `User not found: clara`
- `/whisper bob` -> `Usage: /whisper <nickname> <message>`
- `/me   ` -> `Usage: /me <action>`

공부 포인트:

- 네트워크 프로그램은 정상 입력보다 잘못된 입력을 더 많이 방어해야 합니다.
- 실패 케이스 테스트가 있으면 리팩터링할 때 동작이 망가졌는지 빨리 알 수 있습니다.
- 사용법 안내 문자열은 여러 곳에 직접 쓰지 않고 상수로 관리하는 편이 안전합니다.

### 테스트 출력 문구 변경

처음 테스트 프로젝트는 protocol만 확인했지만, 지금은 아래까지 같이 확인합니다.

- protocol round-trip
- command-line option parsing
- `ClientRegistry`
- `ChatCommandHandler`
- 명령 성공/실패 케이스

그래서 마지막 출력 문구를 아래처럼 바꿨습니다.

```text
All socket study tests passed.
```

복습 추천 순서:

1. `ChatCommandHandler.cs`에서 `/echo` 처리 흐름 보기
2. 빈 입력일 때 `EchoUsage`를 보내는 부분 보기
3. `SocketStudy.ProtocolTests/Program.cs`에서 실패 케이스 테스트들이 어떤 메시지를 기대하는지 보기
4. 테스트를 직접 실행해 마지막 출력 문구 확인하기

### 메시지 크기 제한 사전 검증

`MessageProtocol`에는 메시지 본문 최대 크기 제한이 있습니다.

```csharp
public const int MaxMessageBytes = 1024 * 1024;
```

오늘은 이 제한을 외부에서도 확인할 수 있도록 아래 메서드를 추가했습니다.

```csharp
public static bool IsWithinMessageSizeLimit(string message)
```

이제 클라이언트는 사용자가 입력한 메시지를 서버로 보내기 전에 크기를 확인합니다.

```text
[client] Message is too large. Limit: 1048576 bytes.
```

공부 포인트:

- protocol 제한은 보내는 쪽과 받는 쪽이 모두 알고 있어야 안전합니다.
- 너무 큰 메시지는 네트워크로 보내기 전에 거절하는 편이 낫습니다.
- 문자열 길이가 아니라 UTF-8 byte 수를 기준으로 검사해야 합니다.

### 테스트 보강

오늘 추가한 테스트 보강:

- 메시지 크기 제한 경계값 테스트
- `ClientRegistry.SnapshotRoom()` 대소문자 무시 테스트
- `NameRules` 직접 테스트

공부 포인트:

- 간접 테스트만으로는 규칙이 어디서 깨졌는지 찾기 어렵습니다.
- 작은 규칙 클래스라도 여러 곳에서 쓰이면 직접 테스트할 가치가 있습니다.
- 방 이름처럼 사용자가 입력하는 값은 대소문자 정책을 테스트로 고정해두는 편이 좋습니다.

오늘 추가된 주요 커밋:

```text
93ba1d3 Add echo chat command
adfcd64 Test empty echo command
e9ed782 Extract command usage messages
614cacc Cover whisper error cases
36aae83 Test empty action command
728f6a8 Rename test success message
5a5124c Document echo command study step
b7db2db Validate client message size before send
dfd2889 Document message size limit
7374fc7 Cover room snapshot casing
092cf4f Cover shared name rules
```

### MOTD와 명령 사용법 개선

추가로 `/motd` 명령을 만들었습니다.

```text
> /motd
< [notice] Welcome to SocketStudy. Type /help to see commands.
```

MOTD는 message of the day의 줄임말로, 서버가 사용자에게 보여주는 짧은 안내 메시지라고 생각하면 됩니다.

이번 변경에서는 두 가지 흐름을 만들었습니다.

1. 사용자가 `/motd`를 입력하면 안내 메시지를 다시 볼 수 있습니다.
2. 클라이언트가 처음 접속했을 때도 서버가 같은 안내 메시지를 보내줍니다.

공부 포인트:

- 같은 문자열을 여러 곳에 직접 쓰지 않고 `ServerInfo.MessageOfTheDay` 상수로 공유했습니다.
- 테스트도 같은 상수를 사용해서 문자열 중복을 줄였습니다.
- 서버 접속 흐름과 명령 처리 흐름이 같은 메시지를 재사용합니다.

### 명령 목록 관리 개선

`/help` 출력은 처음에는 긴 문자열 하나로 관리했습니다.

명령이 많아지면서 아래처럼 배열에서 문자열을 만들도록 바꿨습니다.

```csharp
private static readonly string[] CommandNames = [ ... ];
private static readonly string CommandList = $"Commands: {string.Join(", ", CommandNames)}";
```

공부 포인트:

- 긴 문자열 하나보다 배열이 수정하기 쉽습니다.
- 새 명령을 넣을 때 쉼표나 순서를 확인하기 편합니다.
- `/help` 테스트가 `/motd`, `/echo <message>` 같은 최근 명령을 포함하는지 확인합니다.

### 인자 없는 명령 처리

아래 명령들은 인자가 필요합니다.

- `/name <nickname>`
- `/rename <nickname>`
- `/join <room>`
- `/echo <message>`
- `/me <action>`
- `/whisper <nickname> <message>`

이제 인자 없이 명령만 입력하면 unknown command가 아니라 사용법을 보여줍니다.

```text
> /join
< [notice] Usage: /join <room>
```

반복되는 처리는 아래 헬퍼로 정리했습니다.

```csharp
private async Task<bool> SendUsageIfExactCommandAsync(...)
```

공부 포인트:

- 같은 패턴이 여러 번 반복되면 작은 헬퍼를 고려할 수 있습니다.
- 단, 처음부터 추상화하지 말고 반복이 실제로 보일 때 정리하는 편이 안전합니다.
- 테스트가 있으면 리팩터링 후에도 동작 유지 여부를 바로 확인할 수 있습니다.

추가된 주요 커밋:

```text
1e66c84 Add motd chat command
7c9495a Send motd on client connect
0234254 Reuse motd constant in tests
e9162ad Verify new commands in help output
5fa096a Build help text from command list
4c4f5ab Handle missing join room
41455e6 Handle missing rename arguments
9874e4f Handle missing message command arguments
39af501 Extract exact command usage helper
```

### 서버 정보 분리

마지막으로 서버 이름, 버전, 안내 메시지를 `ServerInfo.cs`로 분리했습니다.

```csharp
public static class ServerInfo
{
    public const string Name = "SocketStudy";
    public const string Version = "v1";
    public const string VersionMessage = $"{Name} server {Version}";
    public const string MessageOfTheDay = "Welcome to SocketStudy. Type /help to see commands.";
}
```

공부 포인트:

- 서버 이름, 버전처럼 여러 곳에서 쓰는 값은 한 곳에서 관리하는 편이 좋습니다.
- `/version` 명령은 `ServerInfo.VersionMessage`를 사용합니다.
- `/motd` 명령과 접속 직후 안내는 `ServerInfo.MessageOfTheDay`를 사용합니다.
- 테스트도 같은 상수를 검증해서 버전 표시 형식을 고정합니다.

추가된 커밋:

```text
aeaaabe Extract server info constants
ddb073e Cover server info constants
```

## 19. MMO 서버 방향으로 첫 확장

최종 목표를 MMO RPG 서버 학습으로 잡았기 때문에, 채팅 서버 위에 MMO 서버의 핵심 개념을 하나씩 얹기 시작했습니다.

### PlayerSession 추가

`PlayerSession.cs`를 추가했습니다.

```csharp
public sealed class PlayerSession
{
    public const long AnonymousPlayerId = 0;
    public long PlayerId { get; private set; }
    public bool IsAuthenticated => PlayerId != AnonymousPlayerId;
}
```

현재는 아주 단순합니다.

- 처음 연결되면 anonymous 상태입니다.
- `/login <playerId>` 명령으로 학습용 플레이어 ID를 세션에 연결합니다.
- `/session` 명령으로 현재 세션 상태를 확인합니다.

예시:

```text
> /session
< [notice] Session: player-id=0, state=anonymous

> /login 1001
< [notice] Logged in as player 1001.

> /session
< [notice] Session: player-id=1001, state=authenticated
```

공부 포인트:

- `ClientConnection`은 TCP 연결에 가깝습니다.
- `PlayerSession`은 그 연결 위에 올라가는 게임 플레이어 상태입니다.
- MMO 서버에서는 연결과 플레이어 상태를 분리해서 생각하는 것이 중요합니다.

지금은 진짜 계정 인증이 아닙니다. 비밀번호, 토큰, DB 검증 없이 player id만 세션에 넣는 학습용 로그인입니다.

나중에는 아래처럼 확장할 수 있습니다.

```text
/login 1001
-> LoginRequest packet
-> Account DB 검증
-> Character 선택
-> PlayerSession에 AccountId, CharacterId, ZoneId 연결
```

추가된 주요 커밋:

```text
3f186fc Add player session model
81197c5 Add session status command
3aa9c30 Add learning login command
8721856 Cover authenticated session status
```

### 월드 위치와 이동

MMO 서버의 가장 기본적인 게임 상태 중 하나는 플레이어 위치입니다.

이번 step에서 `WorldPosition`과 `WorldRules`를 추가했습니다.

```csharp
public readonly record struct WorldPosition(int X, int Y);
```

현재 세션은 기본 위치 `x=0, y=0`에서 시작합니다.

```text
> /pos
< [notice] Position: x=0, y=0

> /move 10 20
< [notice] Moved to x=10, y=20
```

학습용 월드 경계도 추가했습니다.

```csharp
public const int MinCoordinate = -100;
public const int MaxCoordinate = 100;
```

공부 포인트:

- 클라이언트가 보낸 위치를 서버가 그대로 믿으면 안 됩니다.
- 서버는 이동 요청을 받으면 월드 규칙을 기준으로 검증해야 합니다.
- 지금은 단순한 좌표 범위만 확인하지만, 나중에는 맵 충돌, 이동 속도, 거리 검증으로 확장됩니다.

추가된 주요 커밋:

```text
0d5bfb5 Add player position commands
67e5f31 Validate player movement bounds
```

### 주변 플레이어 조회와 이동 알림

MMO 서버는 보통 모든 플레이어에게 모든 정보를 보내지 않습니다.

현재 플레이어와 가까운 플레이어에게만 필요한 정보를 보냅니다. 이 개념을 interest management라고 부릅니다.

이번 step에서는 아주 작은 버전으로 `/nearby`를 추가했습니다.

```text
> /nearby
< [notice] Nearby players (1): bob
```

규칙:

- 같은 채팅방 안에 있어야 합니다.
- `WorldRules.ViewDistance` 안에 있어야 합니다.
- 자기 자신은 제외합니다.

또한 `/move`로 위치가 바뀌면 주변 플레이어에게만 이동 notice를 보냅니다.

공부 포인트:

- MMO 서버는 “누구에게 보낼 것인가”를 계속 판단해야 합니다.
- 지금은 맨해튼 거리로 단순 계산합니다.
- 나중에는 zone, grid, quad tree, AOI 같은 구조로 확장할 수 있습니다.

추가된 주요 커밋:

```text
3d3aef1 Add nearby player lookup
c717c78 Notify nearby players on move
```

### 스폰 상태

MMO에서는 플레이어가 연결되어 있다고 해서 곧바로 월드에 등장한 것은 아닙니다.

그래서 `PlayerSession`에 `IsSpawned` 상태를 추가했습니다.

흐름:

```text
접속
-> session anonymous
-> /login 1001
-> authenticated
-> /spawn
-> spawned
```

`/session` 출력도 스폰 상태를 함께 보여줍니다.

```text
> /session
< [notice] Session: player-id=1001, state=authenticated, spawn=spawned
```

공부 포인트:

- 로그인 상태와 월드 스폰 상태는 다릅니다.
- 실제 MMO에서는 로그인 후 캐릭터 선택, 맵 로딩, 월드 입장 과정을 거칩니다.
- 주변 플레이어에게 스폰 알림을 보내는 것은 “월드에 등장했다”는 이벤트의 작은 버전입니다.

추가된 주요 커밋:

```text
706d067 Add player spawn command
7cb7c6f Track player spawn state
```

### 디스폰 상태 전환

스폰의 반대 흐름으로 `/despawn` 명령을 추가했습니다.

```text
> /despawn
< [notice] Despawned from x=10, y=20
```

이미 스폰되어 있지 않은 상태에서 다시 despawn을 요청하면 상태를 바꾸지 않고 본인에게만 알려줍니다.

```text
> /despawn
< [notice] You are not spawned.
```

공부 포인트:

- 월드에 “존재한다”와 “존재하지 않는다”는 서버가 명확히 관리해야 하는 상태입니다.
- despawn은 로그아웃, 맵 이동, 캐릭터 선택 화면 복귀, 사망 후 제거 같은 흐름의 기초가 됩니다.
- 주변 플레이어에게 despawn 알림을 보내는 것은 “더 이상 이 플레이어를 화면에 보여주지 말라”는 이벤트의 작은 버전입니다.

추가된 주요 커밋:

```text
03b23df Add player despawn command
```

### 스폰 전 이동 거부

`IsSpawned`가 단순히 출력에만 사용되는 값이 아니라 실제 월드 규칙이 되도록 `/move`에 상태 검사를 추가했습니다.

```text
> /move 10 20
< [notice] You must spawn before moving.

> /spawn
< [notice] Spawned at x=0, y=0

> /move 10 20
< [notice] Moved to x=10, y=20
```

서버는 이동 요청을 받으면 좌표를 변경하기 전에 플레이어가 월드에 스폰되어 있는지 확인합니다.
스폰되지 않았다면 위치를 그대로 유지하고 주변 플레이어에게도 이동 알림을 보내지 않습니다.

공부 포인트:

- 클라이언트가 보낸 명령을 그대로 실행하지 않고 서버가 현재 상태를 기준으로 허용 여부를 결정해야 합니다.
- 상태 검사에 실패한 명령은 게임 상태와 주변 플레이어에게 어떤 부작용도 남기지 않아야 합니다.
- 이런 검사는 비정상 패킷과 치팅 시도를 막는 권위 서버(authoritative server)의 기본입니다.

### 스폰 플레이어만 주변 탐색

AOI 탐색 결과에 접속만 했을 뿐 월드에 나타나지 않은 플레이어가 포함되면 다른 클라이언트는 존재하지 않는 캐릭터를 보게 됩니다.
그래서 `/nearby` 요청자와 탐색 결과의 플레이어가 모두 스폰 상태인지 확인하도록 변경했습니다.

```text
접속했지만 not-spawned인 bob
-> alice의 /nearby 결과에서 제외

bob이 /spawn 실행
-> 거리와 방 조건이 맞으면 alice의 /nearby 결과에 포함

bob이 /despawn 실행
-> 다시 alice의 /nearby 결과에서 제외
```

구현에서는 두 책임을 나누었습니다.

- `ChatCommandHandler`는 `/nearby`를 요청한 세션이 스폰 상태인지 검사합니다.
- `ClientRegistry`는 주변 이름과 알림 대상에서 스폰되지 않은 다른 세션을 제외합니다.
- despawn 알림은 발신자가 먼저 not-spawned가 된 뒤에도 전달되어야 하므로 주변 알림의 중심 세션은 필터링하지 않습니다.

공부 포인트:

- 네트워크 연결 목록과 월드 엔티티 목록은 같은 개념이 아닙니다.
- AOI는 거리뿐 아니라 방, 맵, 스폰 상태 같은 여러 조건을 함께 적용해야 합니다.
- 상태 변경 이벤트의 처리 순서가 탐색 조건과 충돌하지 않는지 확인해야 합니다.

### 스폰 상태 전이 검증

`/spawn`은 아무 세션에서나 실행할 수 있는 명령이 아닙니다.
서버가 허용하는 순서는 다음과 같습니다.

```text
anonymous
-> /login <playerId>
authenticated
-> /spawn
spawned
```

로그인하지 않은 플레이어가 `/spawn`을 요청하면 서버가 거부합니다.

```text
> /spawn
< [notice] You must login before spawning.
```

이미 스폰된 플레이어가 다시 `/spawn`을 요청해도 중복 이벤트를 만들지 않습니다.

```text
> /spawn
< [notice] You are already spawned.
```

두 거부 상황 모두 `IsSpawned`를 잘못 변경하지 않으며 주변 플레이어에게 spawn 알림도 보내지 않습니다.

공부 포인트:

- 상태 전이(state transition)는 현재 상태에서 허용된 다음 상태로만 이동해야 합니다.
- 같은 명령이 반복되어도 월드에 중복 엔티티나 중복 이벤트를 만들면 안 됩니다.
- 검증, 상태 변경, 이벤트 전송의 순서를 명확히 유지해야 합니다.

### 로그인 상태 전이 검증

로그인 후 `/login`을 다시 실행해 `PlayerId`를 바꿀 수 있으면 월드에 존재하는 캐릭터의 정체성이 갑자기 달라질 수 있습니다.
서버는 인증이 끝난 세션의 반복 로그인을 거부합니다.

```text
> /login 1001
< [notice] Logged in as player 1001.

> /login 2002
< [notice] You are already logged in as player 1001.
```

특히 스폰된 상태에서는 월드 엔티티와 연결된 플레이어 ID를 절대 교체하지 않습니다.

```text
spawned player 1001
-> /login 2002
<- You cannot login while spawned.
-> player id=1001, spawn=spawned 유지
```

검증은 두 계층에 적용했습니다.

- `ChatCommandHandler`는 현재 상태에 맞는 안내 메시지를 클라이언트에 반환합니다.
- `PlayerSession.Authenticate`는 서버 내부에서 실수로 반복 호출해도 기존 ID를 덮어쓰지 못하게 합니다.

공부 포인트:

- 세션의 인증 정체성은 월드 상태보다 먼저 확정되어야 합니다.
- 명령 처리기의 검증과 도메인 객체의 불변 조건(invariant)은 서로 다른 방어선입니다.
- 실패한 상태 전이는 기존 인증 정보와 월드 상태를 그대로 보존해야 합니다.

### 로그아웃과 세션 초기화

인증 상태에서 익명 상태로 돌아가는 `/logout` 명령을 추가했습니다.
월드 엔티티가 남은 채 인증 정보만 사라지지 않도록 먼저 `/despawn`을 실행해야 합니다.

```text
spawned
-> /despawn
authenticated, not-spawned
-> /logout
anonymous, not-spawned
```

스폰 상태에서 바로 로그아웃하면 요청을 거부하고 기존 세션을 유지합니다.

```text
> /logout
< [notice] You must despawn before logging out.
```

정상 로그아웃은 플레이어 ID를 `0`으로, 위치를 원점으로 초기화합니다.

```text
> /despawn
< [notice] Despawned from x=10, y=20

> /logout
< [notice] Logged out.

> /session
< [notice] Session: player-id=0, state=anonymous, spawn=not-spawned
```

공부 포인트:

- 로그아웃은 연결 종료와 다르며 같은 TCP 연결을 유지한 채 인증 상태만 해제할 수 있습니다.
- 상태를 해제할 때는 역순으로 정리해야 합니다: 월드 상태를 먼저 제거하고 인증 정보를 나중에 제거합니다.
- 세션 재사용 시 이전 플레이어의 위치 같은 데이터가 다음 플레이어에게 이어지지 않도록 초기화해야 합니다.

### 채팅방과 게임 맵 분리

기존 주변 탐색은 같은 채팅방인지 확인했지만, 채팅 채널과 게임 월드는 서로 다른 개념입니다.
`PlayerSession`에 `MapId`를 추가하고 AOI가 같은 맵의 플레이어만 찾도록 변경했습니다.

```text
alice: room=lobby, map=1, position=(0, 0)
bob:   room=trade, map=1, position=(10, 10)
-> 서로 다른 채팅방이지만 같은 맵과 시야 거리이므로 nearby

clara: room=lobby, map=2, position=(5, 5)
-> 같은 채팅방이고 가까워도 맵이 다르므로 nearby가 아님
```

현재 맵은 `/map`으로 확인합니다.

```text
> /map
< [notice] Map: 1
```

`ChangeMap`은 스폰 전에만 호출할 수 있으며 로그아웃하면 기본 맵 `1`로 초기화됩니다.
실제 명령을 통한 맵 이동은 다음 단계에서 안전한 despawn과 spawn 순서를 적용해 추가할 수 있습니다.

공부 포인트:

- 채팅방은 메시지를 전달할 채널이고 게임 맵은 월드 엔티티와 AOI를 나누는 경계입니다.
- 위치 좌표가 같아도 맵 ID가 다르면 서로 볼 수 없습니다.
- AOI 키는 앞으로 `MapId`에서 채널, 존, 인스턴스 ID 조합으로 확장할 수 있습니다.

### 안전한 맵 전환

스폰된 플레이어가 다른 맵으로 이동하는 `/warp <mapId> <x> <y>` 명령을 추가했습니다.

```text
> /warp 2 30 40
< [notice] Warped to map=2, x=30, y=40
```

서버는 상태를 바꾸기 전에 다음 조건을 모두 검증합니다.

```text
로그인 상태인가?
-> 스폰 상태인가?
-> mapId, x, y가 정수인가?
-> mapId가 양수인가?
-> 좌표가 월드 경계 안인가?
```

검증이 끝난 뒤에만 맵 전환 상태를 순서대로 변경합니다.

```text
1. 기존 맵 주변 플레이어에게 left 알림
2. 기존 맵에서 despawn
3. MapId와 Position 변경
4. 새 맵에 spawn
5. 새 맵 주변 플레이어에게 entered 알림
```

실패한 요청은 맵, 위치, 스폰 상태를 그대로 유지하며 주변 알림도 만들지 않습니다.

공부 포인트:

- 여러 필드를 바꾸는 명령은 모든 입력을 검증한 뒤 상태 변경을 시작해야 합니다.
- 맵 전환은 단순한 좌표 변경이 아니라 두 AOI 사이에서 엔티티를 제거하고 추가하는 과정입니다.
- 퇴장 이벤트는 이전 맵 상태에서, 입장 이벤트는 새 맵 상태에서 전송해야 올바른 대상이 선택됩니다.

### 이동 거리 검증

기존 `/move`는 월드 경계 안이라면 현재 위치에서 아무 좌표로든 즉시 이동할 수 있었습니다.
이제 한 번의 이동 명령은 맨해튼 거리 `10` 이하만 허용합니다.

```text
현재 위치: x=0, y=0

> /move 1 4 6
거리: |4 - 0| + |6 - 0| = 10
< [notice] Moved to x=4, y=6

> /move 2 15 6
거리: |15 - 4| + |6 - 6| = 11
< [notice] Move distance must be 10 or less.
```

거리 제한을 넘은 요청은 위치를 변경하지 않고 주변 플레이어에게 이동 알림도 보내지 않습니다.
맵 사이의 장거리 이동은 일반 이동이 아니라 검증된 `/warp` 흐름을 사용합니다.

공부 포인트:

- 서버는 클라이언트가 보낸 목적지가 월드 안에 있는지만 아니라 현재 위치에서 도달 가능한지도 확인해야 합니다.
- 위치 검증은 치팅 방지와 서버 권위 이동의 가장 기초적인 형태입니다.
- 실제 MMO에서는 이동 거리와 함께 경과 시간, 캐릭터 속도, 충돌 지형, 이동 상태를 검사합니다.

### 이동 쿨다운과 서버 시간

거리 제한만 있으면 클라이언트가 짧은 이동 요청을 매우 빠르게 반복해 결과적으로 비정상적인 속도를 만들 수 있습니다.
그래서 성공한 `/move` 사이에 서버 시간 기준 `1초`의 최소 간격을 추가했습니다.

```text
10:00:00.000  /move 1 4 0  -> 성공
10:00:00.500  /move 2 8 0  -> 거부
10:00:01.000  /move 2 8 0  -> 성공
```

쿨다운 중인 요청에는 다음 응답을 보냅니다.

```text
< [notice] You must wait 1 second between moves.
```

`PlayerSession.LastMoveAt`에는 클라이언트가 보내는 시간이 아니라 서버의 `getCurrentTime()` 값만 기록합니다.
거부된 이동은 위치와 `LastMoveAt`을 변경하지 않으며 주변 이동 알림도 만들지 않습니다.

공부 포인트:

- 클라이언트 시각은 조작될 수 있으므로 권위 있는 판정에는 서버 시간을 사용해야 합니다.
- 실패한 요청이 쿨다운을 다시 시작하면 정상 사용자의 대기 시간이 계속 늘어나므로 성공한 이동만 기록해야 합니다.
- 실제 MMO에서는 고정 tick과 이동 속도를 결합해 `허용 거리 = 속도 x 경과 시간` 형태로 확장합니다.

### 이동 순서 번호와 중복 패킷 방어

TCP는 바이트 순서를 보장하지만 애플리케이션 재시도나 클라이언트 버그로 같은 이동 명령이 다시 들어올 수 있습니다.
`/move`에 클라이언트 이동 순서 번호를 추가했습니다.

```text
/move <sequence> <x> <y>
```

sequence는 `1`부터 시작하고 서버가 마지막으로 승인한 값보다 커야 합니다.

```text
> /move 10 4 0
< [notice] Moved to x=4, y=0

> /move 10 8 0
< [notice] Move sequence must be greater than 10.
```

`PlayerSession.LastMoveSequence`는 성공한 이동에서만 갱신됩니다.
거리 또는 쿨다운 검증에서 거부된 sequence는 소비하지 않으므로 조건을 고친 뒤 같은 번호로 다시 요청할 수 있습니다.
맵 전환과 로그아웃에서는 새 이동 흐름을 시작할 수 있도록 sequence를 `0`으로 초기화합니다.

공부 포인트:

- 순서 번호는 중복되거나 오래된 상태 변경 요청을 식별하는 간단한 방법입니다.
- 검증에 실패한 요청은 위치, 시간, sequence 중 어느 것도 변경하면 안 됩니다.
- 실제 바이너리 게임 프로토콜에서는 sequence를 패킷 헤더에 넣고 응답 확인, 예측 보정, 재전송 판단에도 활용합니다.

### 주변 플레이어 상태 스냅샷 조회

이번 step에서는 `/nearby`보다 한 단계 더 MMO 서버다운 조회 명령인 `/look`을 추가했습니다.

기존 `/nearby`는 가까운 플레이어의 이름만 보여줍니다.

```text
> /nearby
< [notice] Nearby players (1): bob
```

새 `/look`은 주변 플레이어의 이름, 플레이어 ID, 맵 ID, 위치를 함께 보여줍니다.

```text
> /look
< [notice] Nearby snapshots (1): bob[player-id=2002,map=1,x=10, y=10,distance=20]
```

이 기능을 위해 `NearbyPlayerSnapshot` 타입을 만들었습니다.

```csharp
public readonly record struct NearbyPlayerSnapshot(
    string Name,
    long PlayerId,
    int MapId,
    WorldPosition Position,
    long Distance);
```

공부 포인트:

- `/nearby`는 사람이 읽는 이름 목록에 가깝습니다.
- `/look`은 클라이언트 화면에 엔티티를 그리기 위한 상태 복제 데이터에 가깝습니다.
- `Distance`는 서버가 AOI 우선순위를 판단할 때 쓰는 거리 값입니다.
- 지금은 텍스트 notice로 보내지만, 나중에는 `SpawnEntity`, `UpdatePosition`, `DespawnEntity` 같은 전용 패킷으로 바꿀 수 있습니다.
- `ClientRegistry.GetNearbySnapshots`는 기존 AOI 조건과 같은 규칙을 사용합니다. 같은 맵, 스폰 상태, 시야 거리 안이라는 조건이 모두 맞아야 결과에 포함됩니다.

이번 step은 "주변에 누가 있는가?"에서 "주변 엔티티가 어떤 상태인가?"로 넘어가는 작은 다리입니다. MMO RPG 서버에서는 이 스냅샷이 캐릭터, 몬스터, NPC, 아이템 상태 복제의 출발점이 됩니다.
