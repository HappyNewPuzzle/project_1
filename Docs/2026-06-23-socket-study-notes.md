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
< [notice] Nearby snapshots (1/1, hidden=0): bob[player-id=2002,map=1,x=10, y=10,distance=20]
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
- `/look`은 가까운 플레이어부터 최대 10명까지만 보여줍니다. 사람이 너무 많이 몰린 지역에서 모든 엔티티를 한 번에 보내지 않기 위한 작은 대역폭 보호 장치입니다.
- `/look`의 `(보이는 수/전체 수, hidden=생략 수)` 표기는 제한 때문에 몇 명이 응답에서 빠졌는지 알려줍니다.

이번 step은 “주변에 누가 있는가?”에서 “주변 엔티티가 어떤 상태인가?”로 넘어가는 작은 다리입니다. MMO RPG 서버에서는 이 스냅샷이 캐릭터, 몬스터, NPC, 아이템 상태 복제의 출발점이 됩니다.

### Step 1. AOI Grid 후보 탐색

이번 step에서는 `/nearby`, `/look`, 주변 notice가 모든 접속자를 그대로 훑기 전에 AOI grid 후보 셀을 먼저 보도록 바꿨습니다.

추가된 타입:

```csharp
public readonly record struct WorldGridCell(int MapId, int X, int Y);
```

`WorldGrid.GetCell(mapId, position)`은 월드 좌표를 셀 좌표로 바꿉니다. `WorldGrid.GetNeighborCells(center)`는 중심 셀과 주변 8개 셀, 총 3x3 셀을 반환합니다.

현재 흐름:

```text
player position
-> WorldGridCell 계산
-> 주변 3x3 셀만 후보로 선택
-> 후보 안에서 실제 ViewDistance 검사
-> /nearby, /look, nearby notice 대상 결정
```

공부 포인트:

- 모든 플레이어를 매번 검사하면 접속자가 많아질수록 비용이 커집니다.
- grid는 먼저 후보를 줄이고, 그 다음 정확한 거리 검사를 하는 방식입니다.
- 지금 구현은 단순하게 매 조회 시 후보 셀을 계산하지만, 다음 단계에서는 맵별/셀별 목록을 계속 유지하는 구조로 발전시킬 수 있습니다.
- 실제 MMO 서버의 AOI, interest management, visibility culling이 이런 방향으로 발전합니다.

### Step 2. 월드 엔티티 모델 분리

이번 step에서는 네트워크 연결 객체인 `ClientConnection`에서 바로 월드 상태를 꺼내 쓰지 않고, `WorldEntity`와 `PlayerEntity` 읽기 모델을 추가했습니다.

추가된 구조:

```csharp
public abstract record WorldEntity(long EntityId, int MapId, WorldPosition Position, bool IsSpawned);

public sealed record PlayerEntity(
    long PlayerId,
    string Name,
    int MapId,
    WorldPosition Position,
    bool IsSpawned) : WorldEntity(PlayerId, MapId, Position, IsSpawned);
```

현재는 `PlayerEntity.FromConnection(connection)`으로 접속 객체와 세션 상태를 월드 엔티티 읽기 모델로 변환합니다.

공부 포인트:

- `ClientConnection`은 TCP 연결, 닉네임, 송신 lock 같은 네트워크 책임을 가집니다.
- `PlayerSession`은 로그인, 위치, 스폰 상태 같은 플레이어 세션 상태를 가집니다.
- `PlayerEntity`는 클라이언트 화면에 복제할 월드 상태를 읽는 모델입니다.
- 나중에 `MonsterEntity`, `NpcEntity`, `ItemEntity`를 추가해도 AOI 스냅샷 구조를 확장하기 쉬워집니다.

이번 step은 아직 큰 구조 변경은 아니지만, “네트워크 연결”과 “월드에 존재하는 엔티티”를 머릿속에서 분리하기 위한 기초 작업입니다.

### Step 3. 스폰/디스폰 전용 월드 이벤트 모델

이번 step에서는 주변 플레이어에게 보내는 spawn, move, despawn, map enter/leave 알림을 바로 문자열로 만들지 않고 `WorldEvent` 모델을 거치도록 변경했습니다.

추가된 구조:

```csharp
public enum WorldEventType
{
    PlayerSpawned,
    PlayerMoved,
    PlayerDespawned,
    PlayerLeftMap,
    PlayerEnteredMap
}

public sealed record WorldEvent(WorldEventType Type, string ActorName, int MapId, WorldPosition Position);
```

현재는 `WorldEvent.ToNoticeMessage()`로 기존 notice 문자열을 만듭니다.

```text
WorldEvent.PlayerMoved("alice", 1, position)
-> alice moved to x=10, y=20
```

공부 포인트:

- 문자열은 클라이언트에 보여주는 표현이고, 이벤트는 서버 내부의 의미입니다.
- 이벤트 타입을 분리하면 나중에 텍스트 notice 대신 바이너리 패킷이나 JSON 패킷으로 바꾸기 쉽습니다.
- 실제 MMO 서버에서는 `SpawnEntity`, `MoveEntity`, `DespawnEntity` 같은 이벤트가 클라이언트 동기화의 중심이 됩니다.
- 지금은 작은 record 하나지만, 나중에는 이벤트 큐, 월드 tick, 패킷 직렬화로 연결할 수 있습니다.

### Step 4. 맵별 플레이어 목록 조회

이번 step에서는 `ClientRegistry`가 특정 게임 맵에 스폰되어 있는 플레이어 목록을 조회할 수 있게 했습니다.

추가된 메서드:

```csharp
public string[] GetSpawnedPlayerNamesInMap(int mapId)
```

새 명령:

```text
> /map-users
< [notice] Players in map 1 (2): alice, bob
```

공부 포인트:

- 채팅방 목록과 게임 맵 목록은 다릅니다.
- `/room-users`는 같은 채팅 채널의 접속자 목록입니다.
- `/map-users`는 같은 게임 맵에 실제로 스폰된 플레이어 목록입니다.
- 스폰되지 않은 플레이어는 맵 안에 존재하는 월드 엔티티가 아니므로 목록에서 제외합니다.
- 나중에는 이 구조가 맵별 zone, channel, instance, shard 관리로 확장됩니다.

### Step 5. 이동 상태 서버 tick 처리기 분리

이번 step에서는 `/move` 명령이 세션 상태를 직접 바꾸는 대신 `MovementTickProcessor`를 통해 최종 이동 적용을 하도록 변경했습니다.

추가된 구조:

```csharp
public sealed record MovementRequest(long Sequence, WorldPosition TargetPosition, DateTimeOffset ServerTime);
public sealed record MovementTickResult(bool IsAccepted, string? RejectionReason);
public static class MovementTickProcessor { ... }
```

현재 흐름:

```text
/move 1 4 6
-> MovementRequest 생성
-> MovementTickProcessor.Process(session, request)
-> 검증 통과 시 session.MoveTo(...)
-> 주변 플레이어에게 WorldEvent.PlayerMoved 알림
```

공부 포인트:

- 입력 파싱과 이동 상태 적용을 분리하면 나중에 서버 tick loop로 옮기기 쉬워집니다.
- 서버 tick 처리기는 sequence, 월드 경계, 이동 거리, 쿨다운을 한곳에서 검증합니다.
- 실패한 이동은 위치, 마지막 이동 시각, sequence를 변경하지 않습니다.
- 실제 MMO 서버에서는 네트워크 스레드가 요청을 큐에 넣고, 월드 tick이 큐를 꺼내 순서대로 처리하는 구조로 발전합니다.

이번 step은 아직 독립 tick loop는 아니지만, 이동 처리 책임을 별도 처리기로 분리해 다음 구조 변경의 발판을 만든 단계입니다.

### Step 1. 이동 요청 큐

이번 step에서는 네트워크 명령이 만든 이동 요청을 `MovementRequestQueue`에 넣은 뒤 FIFO 순서로 꺼내 처리하도록 변경했습니다.

```text
/move 수신
-> QueuedMovementRequest 생성
-> MovementRequestQueue.Enqueue(...)
-> MovementRequestQueue.TryDequeue(...)
-> MovementTickProcessor.Process(...)
```

`QueuedMovementRequest`는 소켓 연결이 아니라 `PlayerSession`과 `MovementRequest`만 참조합니다. 덕분에 월드 이동 로직이 네트워크 계층에 직접 의존하지 않습니다.

공부 포인트:

- FIFO 큐는 먼저 도착한 입력을 먼저 처리합니다.
- `lock`으로 enqueue, dequeue, count 연산을 보호해 여러 네트워크 작업이 동시에 접근해도 큐 내부 상태가 깨지지 않게 했습니다.
- 현재는 기존 명령 응답을 유지하기 위해 요청을 바로 꺼냅니다. 다음 step에서 `WorldTickProcessor`가 여러 요청을 한 번에 꺼내 처리하게 됩니다.

### Step 2. 월드 틱 처리기

이번 step에서는 `WorldTickProcessor.ProcessOnce()`가 이동 요청 큐를 비우면서 한 tick의 입력을 일괄 처리하도록 변경했습니다.

```text
네트워크 명령들 -> MovementRequestQueue
                         |
                         v
                  WorldTickProcessor.ProcessOnce()
                         |
                         v
                 WorldTickResult 반환
```

각 결과는 `ProcessedMovement`에 원본 요청과 성공 또는 거절 결과를 함께 보관합니다. 따라서 명령 처리기는 월드 상태를 직접 변경하지 않고 해당 플레이어의 처리 결과만 찾아 응답할 수 있습니다.

공부 포인트:

- tick은 서버가 정한 시점에 게임 상태를 갱신하는 논리적 단위입니다.
- 한 tick에서 입력을 모아 처리하면 입력 도착과 게임 시뮬레이션의 실행 시점을 분리할 수 있습니다.
- 현재는 학습과 기존 테스트 호환을 위해 `/move`가 즉시 한 tick을 실행합니다. 이후에는 고정 주기 loop가 같은 `ProcessOnce()`를 호출하도록 발전시킬 수 있습니다.

### Step 3. 월드 이벤트 큐

이번 step에서는 스폰, 이동, 디스폰, 맵 입장과 퇴장 사건을 `WorldEventQueue`에 넣고 공통 dispatch 단계에서 AOI 대상에게 전송하도록 변경했습니다.

```text
월드 상태 변경
-> WorldEvent 생성
-> WorldEventQueue.Enqueue(...)
-> DispatchWorldEventAsync(...)
-> 주변 플레이어에게 notice 전송
```

공부 포인트:

- 상태 변경과 네트워크 알림 전송은 서로 다른 책임입니다.
- 사건을 큐에 보관하면 저장, 로그, 다른 서버 전달 같은 소비자를 나중에 추가하기 쉽습니다.
- 사건 순서를 FIFO로 유지하므로 같은 처리 흐름에서 발생한 알림 순서를 예측할 수 있습니다.
- 현재 dispatch는 기존 동작을 유지하기 위해 즉시 실행합니다. 이후 월드 tick의 마지막 단계에서 한꺼번에 전송하도록 옮길 수 있습니다.

### Step 4. 월드 격자 인덱스

이번 step에서는 AOI 후보를 찾을 때 전체 접속자 목록을 순회하지 않고 `WorldGridIndex`에서 중심 셀과 주변 8개 셀의 플레이어만 조회하도록 변경했습니다.

`WorldGridIndex`는 두 방향의 정보를 관리합니다.

```text
WorldGridCell -> 그 셀에 있는 ClientConnection 집합
ClientConnection -> 현재 등록된 WorldGridCell
```

스폰, 이동, 디스폰, 워프 직후 `RefreshWorldIndex`가 호출되며 연결 종료와 서버 종료 때는 인덱스에서도 제거됩니다.

공부 포인트:

- 전체 플레이어 수가 커질수록 모든 플레이어를 매번 검사하는 비용이 커집니다.
- 공간 인덱스는 먼저 가까울 가능성이 있는 후보를 줄인 뒤 실제 거리 계산을 수행합니다.
- 셀 조회는 후보 필터일 뿐이므로 마지막에는 반드시 같은 맵과 정확한 시야 거리 조건을 다시 검사해야 합니다.
- 플레이어 상태와 인덱스가 어긋나지 않도록 모든 상태 전이 지점에서 갱신하는 것이 중요합니다.

### Step 5. 몬스터 월드 엔티티

이번 step에서는 `WorldEntity`를 상속하는 `MonsterEntity`와 서버가 몬스터를 소유하는 `MonsterRegistry`를 추가했습니다.

새 명령:

```text
/spawn-monster <id> <type> <x> <y>
/monsters
```

`/spawn-monster`는 플레이어가 스폰된 현재 맵에 몬스터를 생성합니다. 서버는 양수 ID, 중복 ID, 타입 문자열, 월드 좌표를 검증합니다. `/monsters`는 현재 맵에 스폰된 몬스터를 ID 순서로 보여줍니다.

공부 포인트:

- 플레이어와 몬스터는 모두 ID, 맵, 위치, 스폰 상태를 가진 `WorldEntity`입니다.
- 몬스터는 클라이언트가 아니라 서버가 생성하고 관리하는 권한 모델을 사용합니다.
- `MonsterRegistry`는 엔티티 ID의 유일성과 맵별 스냅샷을 책임집니다.
- 다음 발전 단계에서는 몬스터 상태, AI tick, 타깃 탐색, 이동 요청, 전투 판정을 차례로 붙일 수 있습니다.

### 다음 단계 1. 고정 주기 WorldTickLoop

이번 step에서는 `/move` 명령이 `WorldTickProcessor.ProcessOnce()`를 직접 호출하던 구조를 제거하고, 독립적인 `WorldTickLoop`가 50ms마다 월드 입력 큐를 처리하도록 변경했습니다. 현재 학습 서버의 tick rate는 초당 20회입니다.

```text
네트워크 작업                     월드 작업
/move 수신
-> QueuedMovementRequest
-> MovementRequestQueue  ------>  50ms 주기 WorldTickLoop
                                  -> WorldTickProcessor.ProcessOnce()
                                  -> 이동 검증 및 상태 적용
<------- Completion 결과 전달 ---+
-> 클라이언트 응답 및 AOI 알림
```

`QueuedMovementRequest`는 `TaskCompletionSource<MovementTickResult>`를 이용해 처리 완료 신호를 제공합니다. 네트워크 작업은 큐에 요청을 넣은 후 비동기로 기다리며, 월드 tick이 처리 결과를 설정하면 다시 실행됩니다.

공부 포인트:

- 네트워크 패킷이 도착한 순간과 게임 상태가 변경되는 순간이 분리됐습니다.
- 모든 이동은 서버가 정한 tick 경계에서 처리되므로 시뮬레이션 순서를 제어하기 쉬워집니다.
- `RunContinuationsAsynchronously`는 완료 신호를 받은 네트워크 후속 작업이 월드 tick 실행 흐름을 직접 점유하지 않게 합니다.
- `PeriodicTimer`는 busy loop 없이 일정한 간격으로 비동기 tick을 실행합니다.
- 서버 종료 시 큐에 이미 들어온 요청을 마지막으로 처리해 대기 작업이 영원히 남지 않게 했습니다.

현재는 이동만 tick 입력으로 처리합니다. 다음 단계에서는 몬스터 AI 판단과 이동도 같은 월드 tick에 포함할 수 있습니다.

### 다음 단계 2. Monster AI tick과 서버 권한 이동

이번 step에서는 `MonsterAiTickProcessor`를 고정 주기 `WorldTickLoop`에 연결했습니다. 몬스터는 같은 맵에 스폰된 플레이어 중 맨해튼 거리가 가장 가까운 플레이어를 대상으로 선택하고, 500ms마다 한 칸씩 추적합니다.

```text
WorldTickLoop (50ms)
-> 플레이어 이동 요청 처리
-> MonsterAiTickProcessor
   -> 스폰 몬스터 스냅샷
   -> 같은 맵 플레이어 필터
   -> 가장 가까운 대상 선택
   -> 이동 간격 확인
   -> MonsterRegistry.TryMove(...)
```

몬스터 이동은 클라이언트가 좌표를 지정하지 않습니다. 서버 AI가 목표와 다음 좌표를 결정하고, `MonsterRegistry`의 잠금 안에서 기존 위치가 예상 위치와 같은지 확인한 후 새 불변 `MonsterEntity`로 교체합니다.

공부 포인트:

- AI 판단과 상태 변경은 네트워크 요청과 독립적으로 월드 tick에서 실행됩니다.
- 다른 맵의 플레이어는 타깃 후보에서 제외됩니다.
- 거리가 같으면 플레이어 ID가 작은 대상을 먼저 선택해 결과를 결정적으로 만듭니다.
- 이동 간격은 tick 간격과 별도입니다. 월드는 초당 20회 갱신되지만 몬스터는 초당 2회만 이동합니다.
- `MonsterMovement`는 이전 위치, 다음 위치, 대상 플레이어를 기록해 이후 AOI 이벤트나 전투 로그로 확장할 수 있습니다.

현재 AI는 장애물이 없는 직선 추적입니다. 다음 단계에서는 감지 거리, 어그로 상태, 복귀 위치를 도입할 수 있습니다.

### 다음 단계 3. 감지 거리, 어그로, 추적 한계와 복귀

이번 step에서는 몬스터 AI를 세 가지 상태를 가진 상태 머신으로 확장했습니다.

```text
Idle
  | 감지 거리 10 안에서 플레이어 발견
  v
Chasing
  | 대상 소멸, 맵 이탈, 스폰 지점에서 추적 한계 15 초과
  v
Returning
  | 스폰 지점 도착
  v
Idle
```

`MonsterEntity`에는 최초 생성 위치인 `SpawnPosition`, 현재 `AiState`, 어그로 대상인 `AggroTargetPlayerId`가 저장됩니다. `/monsters` 명령에서도 `slime#10[Chasing]@x=3, y=4`처럼 현재 상태를 확인할 수 있습니다.

공부 포인트:

- 감지 거리는 대상을 처음 발견하는 범위이고, 추적 한계는 이미 발견한 대상을 포기하는 기준입니다.
- `Chasing` 상태는 매 tick 아무 플레이어나 다시 고르지 않고 저장된 어그로 대상을 추적합니다.
- 대상이 사라지거나 다른 맵으로 이동해도 몬스터가 무한히 추적하지 않고 스폰 지점으로 복귀합니다.
- 복귀 이동에는 플레이어 타깃이 없으므로 `MonsterMovement.TargetPlayerId`는 nullable 값입니다.
- 저장소의 `TryUpdate(expected, updated)`는 AI가 읽은 뒤 다른 작업이 상태를 바꿨다면 오래된 판단으로 덮어쓰지 않습니다.

아직 전투는 없지만 이제 몬스터가 언제 공격 가능한 상태인지 판단할 기반이 생겼습니다. 다음 단계는 HP, 공격 거리, 공격 쿨다운과 피해 이벤트입니다.

### 다음 단계 4. HP와 몬스터 기본 공격

이번 step에서는 플레이어와 몬스터에 HP를 추가하고, `Chasing` 몬스터가 공격 거리 안에서 서버 권한으로 피해를 적용하도록 확장했습니다.

기본 전투 규칙:

```text
플레이어 최대 HP: 100
몬스터 최대 HP: 50
몬스터 공격력: 10
공격 거리: 맨해튼 거리 1
공격 쿨다운: 1초
```

전투 흐름:

```text
MonsterAiTickProcessor
-> 저장된 어그로 대상 확인
-> 공격 거리와 쿨다운 검사
-> ClientRegistry.ApplyDamage(playerId, damage)
-> PlayerSession.ApplyDamage(damage)
-> MonsterAttack 결과 생성
-> HP가 0이면 플레이어 사망 및 자동 despawn
-> WorldGridIndex에서 사망 플레이어 제거
```

`MonsterAttack`에는 몬스터 ID, 대상 플레이어 ID, 실제 적용 피해, 남은 HP, 치명타 여부가 기록됩니다. `/health`로 플레이어 HP를 확인하고 `/monsters`로 몬스터 HP와 AI 상태를 볼 수 있습니다.

공부 포인트:

- 피해량과 사망 여부는 클라이언트가 아니라 서버가 계산합니다.
- 현재 HP보다 큰 피해가 들어오면 실제 적용 피해는 남은 HP까지만 계산됩니다.
- 공격 쿨다운은 이동 쿨다운과 독립적입니다.
- 사망한 플레이어는 즉시 스폰 상태와 AOI 인덱스에서 제거됩니다.
- 다시 `/spawn`하면 학습용 규칙에 따라 HP가 최대치로 회복됩니다.

현재는 몬스터가 플레이어만 공격합니다. 다음 단계에서는 플레이어 공격 명령, 몬스터 피해·사망·리스폰을 추가할 수 있습니다.

### 다음 단계 5. 플레이어 공격과 몬스터 리스폰

이번 step에서는 `/attack <monsterId>` 명령과 `CombatTickProcessor`를 추가했습니다. 네트워크 작업은 공격 요청을 큐에 넣고, 월드 tick이 서버 시각을 기준으로 검증한 결과를 기다립니다.

플레이어 공격 규칙:

```text
공격력: 20
공격 거리: 맨해튼 거리 2
공격 쿨다운: 500ms
몬스터 리스폰: 사망 5초 후
```

처리 흐름:

```text
/attack 10
-> PlayerAttackRequestQueue
-> CombatTickProcessor
   -> 플레이어 생존·스폰 검사
   -> 몬스터 생존·맵·거리 검사
   -> 서버 공격 쿨다운 검사
   -> MonsterRegistry.ApplyDamage(...)
   -> PlayerAttackResult 완료
-> 클라이언트 결과 응답
```

몬스터 HP가 0이 되면 `IsSpawned=false`와 `RespawnAt`이 저장됩니다. 매 combat tick의 시작에서 `RespawnReady`가 만료된 몬스터를 찾아 스폰 위치, 최대 HP, `Idle` 상태로 복원합니다.

공부 포인트:

- 공격 패킷도 이동 패킷처럼 월드 tick 경계에서 처리됩니다.
- 요청에 담긴 시간이 아니라 combat tick의 서버 시각으로 쿨다운과 사망 시각을 판정합니다.
- 치명타 피해는 남은 HP까지만 적용되므로 HP가 음수가 되지 않습니다.
- 죽은 몬스터는 AI 스냅샷과 `/monsters` 목록에서 제외되어 이동하거나 공격할 수 없습니다.
- 리스폰은 별도 타이머를 몬스터마다 만들지 않고 월드 tick이 `RespawnAt`을 비교하는 방식입니다.

다음 단계에서는 전투 이벤트를 AOI 플레이어에게 전파하고 경험치와 보상 소유권을 추가할 수 있습니다.

### 다음 단계 6. 전투 이벤트, 경험치와 처치 소유권

이번 step에서는 전투 결과를 주변 AOI 플레이어에게 전파하고, 몬스터를 마지막으로 처치한 플레이어에게 경험치를 지급하도록 확장했습니다.

```text
몬스터 처치 경험치: 25 XP
```

플레이어 공격은 명령 응답 후 주변 플레이어에게 타격 또는 처치 알림을 보냅니다. 월드 tick에서 자동으로 발생하는 몬스터 공격은 `CombatEventQueue`에 넣고 별도의 `CombatEventDispatchLoop`가 대상과 주변 플레이어에게 비동기로 전송합니다.

```text
WorldTickLoop
-> MonsterAttack 생성
-> CombatEventQueue

CombatEventDispatchLoop
-> 대상 ClientConnection 조회
-> 대상에게 notice
-> 주변 AOI 플레이어에게 notice
```

처치 보상 흐름:

```text
치명타 적용
-> MonsterEntity.KillCreditPlayerId 기록
-> PlayerSession.AddExperience(25)
-> PlayerAttackResult.ExperienceAwarded 반환
-> /experience로 누적 경험치 확인
-> 몬스터 리스폰 시 처치 소유권 초기화
```

공부 포인트:

- 월드 tick은 소켓 전송을 직접 기다리지 않고 이벤트 큐에 기록하므로 느린 클라이언트가 시뮬레이션을 막지 않습니다.
- 경험치와 처치 소유권은 치명타를 실제 적용한 서버 tick에서 한 번만 결정됩니다.
- 거절된 공격과 비치명 공격은 경험치를 지급하지 않습니다.
- `KillCreditPlayerId`는 리스폰 전까지 감사와 보상 추적에 사용할 수 있습니다.
- 리스폰 시 이전 생명 주기의 처치 소유권을 반드시 제거합니다.

다음 단계에서는 경험치 기반 레벨과 몬스터별 보상 테이블, 아이템 드롭을 추가할 수 있습니다.

### 다음 단계 7. 레벨, 보상 테이블과 아이템 드롭

이번 step에서는 누적 경험치 기반 레벨과 서버 보상 카탈로그, 플레이어 인벤토리를 추가했습니다.

레벨 규칙:

```text
레벨 1: 0~99 XP
레벨 2: 100~199 XP
레벨 3: 200~299 XP
레벨 = 누적 XP / 100 + 1
```

기본 몬스터 보상:

```text
slime    -> 25 XP, slime-gel x1
skeleton -> 30 XP, bone x1
orc      -> 40 XP, orc-tusk x1
기타 타입 -> 20 XP, monster-token x1
```

처치 시 `MonsterRewardCatalog`이 서버에 저장된 몬스터 타입으로 보상을 조회합니다. `CombatTickProcessor`는 경험치와 아이템을 처치자 세션에 반영하고 `PlayerAttackResult`에 XP, 현재 레벨, 레벨업 여부, 드롭을 담아 클라이언트에 전달합니다.

새 명령:

```text
/level
/inventory
```

출력 예:

```text
Level: 2, XP: 125, next level in 75 XP
Inventory (2): bone x3, slime-gel x1
```

공부 포인트:

- 보상 수치는 클라이언트 요청이나 명령 코드가 아니라 중앙 `MonsterRewardCatalog`에서 결정됩니다.
- 알려지지 않은 몬스터 타입도 기본 보상을 사용하므로 보상 누락으로 처리가 실패하지 않습니다.
- 같은 아이템을 여러 번 획득하면 인벤토리에서 수량을 누적합니다.
- 인벤토리 스냅샷은 아이템 ID 순으로 정렬해 결과와 테스트를 결정적으로 유지합니다.
- 레벨은 누적 경험치에서 계산하므로 별도 레벨 값과 경험치 값이 어긋나지 않습니다.
- 현재 드롭은 학습을 위해 확정 지급합니다. 이후 확률 드롭을 넣을 때는 테스트 가능한 서버 난수 공급자를 주입해야 합니다.

다음 단계에서는 장비 슬롯, 아이템 사용, 몬스터별 확률 드롭과 전리품 획득 권한을 추가할 수 있습니다.

### 다음 단계 8. 장비, 아이템 사용과 확률 드롭

이번 step에서는 서버 `ItemCatalog`, Weapon/Armor 장비 슬롯, 소비 아이템 사용, 확률 드롭을 추가했습니다.

새 명령:

```text
/equipment
/equip <itemId>
/unequip <Weapon|Armor>
/use <itemId>
```

`iron-sword`는 Weapon 슬롯에 장착되며 기본 공격력 20에 보너스 5를 더합니다. `health-potion`은 잃은 HP 중 최대 30을 회복하고 한 개를 소비합니다. 장비를 교체하면 이전 장비는 인벤토리로 돌아갑니다.

확률 드롭은 `DropTableEntry`의 0~1 확률로 정의합니다. 기본 재료는 100%이며 potion이나 장비는 추가 확률 보상입니다. 실제 서버는 `SystemRandomSource`, 테스트는 고정 난수 공급자를 주입해 결과를 재현합니다.

전리품 권한:

- 드롭 판정과 인벤토리 지급은 치명타를 적용한 `PlayerSession`에만 수행됩니다.
- 다른 주변 플레이어는 전투 알림만 받고 아이템은 받지 않습니다.
- 거절된 공격이나 이미 죽은 몬스터 공격은 드롭 판정을 실행하지 않습니다.

공부 포인트:

- 아이템 능력치는 문자열 명령이 아니라 서버 `ItemCatalog`에서 조회합니다.
- 장착 시 인벤토리에서 제거하고 해제 시 되돌려 아이템 복제를 막습니다.
- 소비 아이템은 효과 적용이 가능한 경우에만 차감합니다.
- 난수를 인터페이스로 주입하면 확률 로직도 안정적으로 테스트할 수 있습니다.
- 플레이어 공격력은 장착 장비에서 계산되어 combat tick 피해량에 반영됩니다.

다음 단계에서는 방어력, 장비 등급, 전리품 바닥 오브젝트와 획득 만료 시간을 추가할 수 있습니다.

### 다음 단계 9. 방어력, 아이템 등급과 바닥 전리품

이번 step에서는 Armor 방어력, 아이템 등급, 월드 바닥 전리품 엔티티를 추가했습니다.

전리품 규칙:

```text
획득 거리: 2
처치자 독점 시간: 10초
전체 만료 시간: 30초
```

몬스터 처치 시 드롭은 즉시 인벤토리에 들어가지 않고 `GroundLootRegistry`에 맵, 위치, 소유 플레이어, 권한 만료와 전체 만료 시각을 가진 엔티티로 생성됩니다.

새 명령:

```text
/loot
/pickup <lootId>
```

처치자는 즉시 획득할 수 있고 다른 플레이어는 10초 동안 거절됩니다. 독점 시간이 지나면 가까운 모든 플레이어가 획득할 수 있으며, 30초가 지나면 월드 tick 정리 단계에서 삭제됩니다.

장비 확장:

- 아이템 등급: Common, Uncommon, Rare
- `leather-armor`: Armor 슬롯, 방어력 3, Uncommon
- `iron-sword`: Weapon 슬롯, 공격력 5, Rare
- 최종 피해는 `max(1, 원래 피해 - 방어력)`으로 계산합니다.

공부 포인트:

- 바닥 전리품은 고유 loot ID를 가진 일시적인 월드 상태입니다.
- 소유권, 거리, 맵, 만료를 모두 서버가 검사한 뒤 인벤토리로 이동합니다.
- pickup 성공 시 레지스트리에서 먼저 제거하고 인벤토리에 넣어 중복 획득을 막습니다.
- combat tick이 만료된 전리품을 주기적으로 정리하므로 개별 타이머가 필요하지 않습니다.
- 방어력에도 최소 피해 1을 적용해 공격이 완전히 무효화되는 상황을 제한합니다.

다음 단계에서는 데이터베이스 영속성으로 캐릭터 레벨, 인벤토리와 장비를 저장하는 구조가 적합합니다.

### 다음 단계 10. 캐릭터 영속성 계층

이번 step에서는 캐릭터 상태를 서버 재시작 이후에도 유지하기 위한 `ICharacterRepository`와 JSON 파일 구현을 추가했습니다.

저장 대상:

- 플레이어 ID
- 맵과 위치
- 현재 HP
- 누적 경험치와 계산된 레벨
- 인벤토리 아이템과 수량
- 장착 중인 Weapon/Armor

새 명령:

```text
/save
/load
```

`/load`는 인증된 상태이면서 despawn 상태일 때만 허용됩니다. 현재 AOI에 존재하는 플레이어의 위치와 장비를 갑자기 덮어써 월드 인덱스가 어긋나는 상황을 막기 위한 규칙입니다.

저장 구조:

```text
PlayerSession.CreateSaveData()
-> CharacterSaveData
-> ICharacterRepository.SaveAsync(...)
-> JsonCharacterRepository
-> Data/characters.json
```

JSON 저장소는 전체 상태를 `.tmp` 파일에 먼저 기록한 뒤 실제 파일로 교체합니다. 저장 도중 프로세스가 종료돼 기존 파일 일부만 덮어써지는 위험을 줄입니다. `SemaphoreSlim`으로 같은 프로세스 안의 동시 save/load도 직렬화합니다.

공부 포인트:

- 저장 모델에는 소켓, stream, lock 같은 런타임 객체를 포함하지 않습니다.
- 저장 인터페이스와 구현을 분리해 이후 JSON을 SQLite, PostgreSQL 등으로 교체할 수 있습니다.
- 복원 시 player ID, 맵, 좌표, 장비 슬롯과 아이템 정의를 다시 검증합니다.
- 테스트는 빠른 `InMemoryCharacterRepository`와 실제 JSON 재로드 테스트를 나눠 사용합니다.
- 실제 저장 파일은 `.gitignore`에서 제외해 캐릭터 데이터가 소스 저장소에 올라가지 않게 했습니다.

현재 JSON은 단일 프로세스 학습용입니다. 다음 단계에서는 SQLite/PostgreSQL 스키마, 트랜잭션, 낙관적 동시성 버전을 도입할 수 있습니다.

### 다음 단계 11. SQLite 트랜잭션과 낙관적 동시성

이번 step에서는 서버 기본 캐릭터 저장소를 JSON에서 SQLite로 교체했습니다. 공식 `Microsoft.Data.Sqlite` ADO.NET provider를 사용하며, .NET 8 계열에 맞춘 8.0.29 버전을 참조합니다.

SQLite 스키마:

```sql
CREATE TABLE schema_info (
    version INTEGER NOT NULL
);

CREATE TABLE characters (
    player_id INTEGER PRIMARY KEY,
    version INTEGER NOT NULL,
    payload TEXT NOT NULL,
    updated_at TEXT NOT NULL
);
```

캐릭터의 구조화된 스냅샷은 현재 JSON payload로 보관하고, 조회와 동시성 제어에 필요한 player ID와 version은 별도 컬럼으로 둡니다. 이후 인벤토리 검색이나 거래 기능이 필요해지면 각각의 정규화 테이블로 분리할 수 있습니다.

낙관적 동시성 처리:

```text
클라이언트 A: version 1 load
클라이언트 B: version 1 load

A save -> WHERE version = 1 -> 성공, version 2
B save -> WHERE version = 1 -> 변경된 행 0개
       -> CharacterConcurrencyException
       -> version 2 데이터 보존
```

INSERT와 UPDATE는 SQLite transaction 안에서 실행됩니다. UPDATE 조건에 기존 version을 포함하고 실제 변경 행이 정확히 1개인지 확인합니다. `/save` 성공 후 `PlayerSession.SaveVersion`을 새 버전으로 갱신하며, 충돌 시 최신 데이터를 `/load`하라는 안내를 반환합니다.

공부 포인트:

- 트랜잭션은 저장 데이터와 version 변경을 하나의 원자적 작업으로 만듭니다.
- 낙관적 동시성은 장시간 DB lock을 잡지 않고 저장 순간에 충돌을 검출합니다.
- stale save가 최신 캐릭터 상태를 조용히 덮어쓰지 못합니다.
- `schema_info`는 이후 컬럼 및 테이블 migration 버전을 관리할 시작점입니다.
- SQLite 파일과 WAL/SHM 파일은 `.gitignore`에서 제외했습니다.
- 기존 JSON 및 메모리 저장소도 같은 인터페이스와 version 규칙을 유지합니다.

SQLite는 단일 월드 서버 학습과 로컬 개발에 적합합니다. 다음 단계에서는 자동 저장, 접속 종료 저장, 저장 실패 재시도와 dirty-state 추적을 추가할 수 있습니다.

### 다음 단계 12. Dirty state와 자동 저장

이번 step에서는 변경된 캐릭터만 저장하는 dirty-state 추적과 자동 저장, 로그아웃·연결 종료 저장, 일시적 실패 재시도를 추가했습니다.

```text
게임 상태 변경
-> PlayerSession.IsDirty = true

30초 autosave
-> 인증 세션 스냅샷
-> dirty 세션만 CharacterSaveService
-> 성공 시 SaveVersion 증가, IsDirty=false
```

dirty로 표시되는 주요 변경:

- 위치와 맵 이동
- 스폰과 디스폰, HP 피해
- 공격 시각
- 경험치와 인벤토리
- 장비 장착·해제
- 아이템 사용

`CharacterSaveService`는 플레이어 ID별 `SemaphoreSlim`을 사용해 수동 `/save`, autosave, 연결 종료 저장이 같은 캐릭터에 동시에 실행되지 않게 합니다. lock을 기다린 후 dirty를 다시 검사하므로 앞선 작업이 이미 저장했다면 중복 DB 호출을 생략합니다.

재시도 정책:

```text
최대 시도: 3회
기본 지연: 50ms
2번째 대기: 100ms
```

일시적 저장 예외는 제한적으로 재시도하지만 `CharacterConcurrencyException`은 최신 버전을 먼저 load해야 해결되므로 즉시 Conflict로 반환합니다. 모든 재시도가 실패하면 dirty 상태를 유지해 다음 autosave나 연결 종료 저장에서 다시 시도할 수 있습니다.

저장 시점:

- `/save`
- 30초 주기 autosave
- `/logout` 직전
- 네트워크 연결 종료 정리
- 서버 종료 시 autosave loop의 마지막 flush

공부 포인트:

- 매 tick 또는 모든 명령마다 저장하지 않아 DB 쓰기 부하를 줄입니다.
- 저장 성공 후에만 dirty를 해제하므로 실패한 변경이 저장된 것으로 오인되지 않습니다.
- 플레이어별 직렬화는 같은 캐릭터의 version 경쟁을 줄이고 다른 캐릭터 저장은 병렬 진행할 수 있게 합니다.
- 로그아웃 저장이 실패하면 세션 초기화를 중단해 메모리 상태를 보존합니다.
- 자동 저장 테스트는 timer를 기다리지 않고 `SaveAllAsync`를 직접 호출해 결정적으로 검증합니다.

다음 단계에서는 graceful shutdown에서 모든 클라이언트 처리 작업을 추적하고 저장 완료 후 서버를 종료하는 구조가 적합합니다.

### 다음 단계 13. Graceful shutdown과 클라이언트 작업 추적

이번 step에서는 접속마다 실행되는 `HandleClientAsync` 작업을 `ClientTaskTracker`에 등록하고, 서버 종료 시 모든 접속 정리가 끝날 때까지 기다리도록 변경했습니다.

기존 코드는 다음처럼 클라이언트 작업을 시작한 뒤 참조를 버렸습니다.

```csharp
_ = HandleClientAsync(client, cancellationToken);
```

이 방식에서는 서버 종료 로그가 출력된 뒤에도 연결 종료 저장이 실행 중일 수 있고, 작업 내부의 예상하지 못한 예외도 관찰하기 어렵습니다. 이제 작업을 추적합니다.

```csharp
clientTasks.Track(HandleClientAsync(client, cancellationToken));
```

정상 종료 순서:

```text
1. listener.Stop()으로 신규 접속 중단
2. CloseAllClients()로 pending 네트워크 읽기/쓰기 해제
3. world tick, 전투 이벤트, autosave loop 취소
4. ClientTaskTracker.WaitForAllAsync()로 모든 접속 작업 대기
5. 각 HandleClientAsync의 finally에서 dirty 캐릭터 저장
6. 백그라운드 loop의 마지막 정리와 autosave flush 대기
7. Server stopped 로그 출력
```

`ClientTaskTracker`는 `ConcurrentDictionary<long, Task>`에 실행 중인 작업을 보관합니다. 작업이 완료되면 별도 continuation이 목록에서 제거하므로 장시간 운영해도 이미 끝난 접속 작업이 계속 메모리에 남지 않습니다.

공부 포인트:

- fire-and-forget 작업도 서버 생명주기에 포함하려면 참조를 추적해야 합니다.
- 소켓을 먼저 닫으면 `ReadMessageAsync`처럼 대기 중인 I/O가 깨어나 접속 작업의 `finally`로 진입합니다.
- 클라이언트 작업을 기다려야 연결 종료 저장이 끝난 뒤 프로세스를 종료할 수 있습니다.
- `Task.WhenAll`은 여러 접속 작업을 동시에 기다리며, 실패한 작업의 예외도 관찰할 수 있습니다.
- 종료 중 한 작업이 실패해도 서버는 오류를 기록한 뒤 나머지 tick 및 autosave 작업을 계속 정리합니다.
- 테스트는 두 개의 `TaskCompletionSource`를 사용해 하나의 작업만 끝난 상태에서는 전체 종료 대기가 완료되지 않는지 검증합니다.

다음 단계에서는 종료 시간을 무한정 기다리지 않도록 shutdown timeout을 두고, 시간 초과 시 남은 작업 수와 저장 실패 플레이어를 기록하는 운영 정책을 추가할 수 있습니다.

### 다음 단계 14. Shutdown timeout과 종료 상태 보고

이번 step에서는 graceful shutdown이 특정 클라이언트 작업 때문에 무한히 멈추지 않도록 10초 제한 시간을 추가했습니다.

```text
클라이언트 작업 전체 대기
├─ 10초 안에 완료: Completed=true, RemainingTaskCount=0
└─ 10초 초과:     Completed=false, 현재 남은 작업 수 기록
```

`ClientTaskTracker.WaitForAllAsync`는 이제 `ClientTaskWaitResult`를 반환합니다.

```csharp
public sealed record ClientTaskWaitResult(
    bool Completed,
    int RemainingTaskCount,
    TimeSpan Elapsed);
```

서버는 정상 종료라면 실제 소요 시간을 info 로그로 남깁니다. 제한 시간을 초과하면 종료를 계속 진행하면서 남은 클라이언트 작업 수를 error 로그로 남깁니다. 제한 시간은 `WorldRules.ServerShutdownTimeout`에 모아 현재 10초로 설정했습니다.

연결 종료 저장에서 `Conflict` 또는 `Failed`가 발생한 플레이어 ID도 서버가 집계합니다. 종료 마지막에는 실패가 없었다는 요약 또는 실패한 플레이어 ID 목록을 기록하므로 운영자가 데이터베이스와 로그를 조사할 대상을 알 수 있습니다. 이후 같은 플레이어 저장이 성공하면 실패 목록에서 제거됩니다.

공부 포인트:

- graceful shutdown에는 기다림뿐 아니라 기다릴 수 있는 최대 시간도 필요합니다.
- timeout은 실행 중인 작업 자체를 강제로 중단하지 않으며, 서버가 더 기다리지 않겠다는 운영 정책입니다.
- `Task.WhenAny`로 전체 완료 작업과 timeout 작업 중 먼저 끝나는 쪽을 확인할 수 있습니다.
- 결과 객체를 사용하면 bool 하나보다 완료 여부, 남은 작업 수, 소요 시간을 함께 전달할 수 있습니다.
- 종료 요약 로그는 장애 이후 어떤 캐릭터 저장을 확인해야 하는지 알려주는 최소한의 운영 정보입니다.
- 테스트에서는 20ms 제한을 사용해 실제 10초를 기다리지 않고 timeout 경로를 검증합니다.

다음 단계에서는 서버 상태를 `Starting`, `Running`, `Draining`, `Stopped`로 명시하고, draining 중 신규 로그인과 게임 명령을 거부하는 생명주기 상태 머신을 추가할 수 있습니다.

### 다음 단계 15. 서버 생명주기 상태 머신

이번 step에서는 서버 실행 상태를 명시적인 네 단계로 관리합니다.

```text
Starting -> Running -> Draining -> Stopped
    \----------------> Draining
```

- `Starting`: 객체 생성 후 listener가 아직 준비되지 않은 상태
- `Running`: listener가 시작되어 정상적으로 접속과 게임 명령을 처리하는 상태
- `Draining`: 종료 요청을 받아 신규 게임 작업을 만들지 않고 기존 작업을 정리하는 상태
- `Stopped`: 접속 작업, 저장, 백그라운드 loop 정리가 끝난 상태

`ServerLifecycle`은 `Interlocked.CompareExchange`를 사용해 여러 스레드가 동시에 종료를 요청해도 상태가 역방향으로 이동하거나 같은 전이가 중복 실행되지 않게 합니다. listener 시작이 실패해도 `Starting -> Draining -> Stopped` 경로로 정리할 수 있습니다.

`CancellationToken`의 종료 callback은 서버 상태를 즉시 `Draining`으로 전환합니다. 이후 `ChatServer`의 `finally`가 listener, 접속 작업, 저장 작업을 정리하고 마지막에 `Stopped`로 전환합니다. 각 전이는 서버 로그에 기록됩니다.

Draining 중 차단하는 명령의 예:

- 신규 인증: `/login`
- 월드 상태: `/spawn`, `/despawn`, `/move`, `/warp`
- 전투와 전리품: `/attack`, `/loot`, `/pickup`
- 인벤토리 변경: `/equip`, `/unequip`, `/use`, `/load`
- 상호작용: `/join`, `/leave`, `/whisper`, `/me`

`/save`, `/logout`, `/quit`은 정리와 데이터 보존에 필요하므로 계속 허용합니다. `/help`, `/session`, `/health` 같은 조회 명령도 사용할 수 있습니다.

공부 포인트:

- 서버 상태를 bool 여러 개로 표현하면 `running=true`, `stopping=true` 같은 모순 조합이 생길 수 있습니다.
- enum 상태 머신은 현재 단계와 허용할 동작을 한 곳에서 판단하게 합니다.
- Draining은 즉시 프로세스를 끄는 상태가 아니라 새로운 변경을 막고 진행 중인 작업을 비우는 단계입니다.
- 명령 처리 진입부에서 정책을 검사하면 각 명령 구현에 같은 조건문을 반복하지 않아도 됩니다.
- 테스트는 정상 상태 전이, 중복·역방향 전이 거부, Draining 중 로그인·이동 거부와 `/quit` 허용을 검증합니다.

다음 단계에서는 접속 자체에 admission control을 추가해 최대 동시 접속자 수와 IP별 접속 제한을 적용하고, 과부하 상태에서 명확한 거부 응답을 보내는 구조로 발전할 수 있습니다.

### 다음 단계 16. Admission control과 접속 제한

이번 step에서는 accept된 TCP 연결을 실제 클라이언트 작업으로 등록하기 전에 전체 접속 수와 IP별 접속 수를 검사합니다.

현재 학습용 제한:

```text
서버 전체 동시 접속: 최대 100개
동일 IP 동시 접속:  최대 5개
제한 초과 연결: TLS handshake 전에 즉시 종료
```

`ConnectionAdmissionController.TryAcquire`는 lock 안에서 검사와 카운트 증가를 한 번에 수행합니다. 따라서 여러 접속이 동시에 들어와도 모두 제한 이하라고 판단한 뒤 한도를 초과하는 경쟁 조건이 생기지 않습니다.

허용된 연결은 `ConnectionAdmissionLease`를 받습니다. `HandleAdmittedClientAsync`가 종료될 때 lease를 dispose하면 전체 카운트와 IP 카운트가 함께 감소합니다. `Dispose`를 여러 번 호출해도 `Interlocked.Exchange`로 실제 반납은 한 번만 실행됩니다.

거부 흐름:

```text
TCP accept
-> 전체/IP 제한 검사
-> 초과 시 protocol Notice 전송
-> TcpClient 종료
-> ClientRegistry와 ClientTaskTracker에는 등록하지 않음
```

전체 한도나 IP별 한도를 초과한 연결은 TLS handshake 전에 즉시 종료합니다. TLS 적용 전에는 이유를 담은 protocol Notice를 보냈지만, 암호화되지 않은 메시지와 TLS handshake를 섞을 수 없고 공격자의 비싼 handshake 요청도 제한해야 하므로 현재 정책에서는 서버 로그에만 거부 이유를 남깁니다.

IPv4-mapped IPv6 주소는 IPv4로 정규화합니다. 같은 호스트가 `127.0.0.1`과 `::ffff:127.0.0.1` 표현을 바꿔 IP 제한을 우회하지 못하게 하기 위한 처리입니다.

공부 포인트:

- admission control은 비싼 세션 생성과 인증 처리 전에 수행해야 서버 자원을 보호할 수 있습니다.
- 검사와 카운트 증가는 하나의 임계 구역에서 원자적으로 처리해야 합니다.
- lease 패턴은 성공한 획득과 반드시 실행해야 하는 반납을 연결 작업의 수명과 묶습니다.
- 전체 제한은 서버 메모리와 작업 수를, IP 제한은 단일 출발지의 과도한 연결을 제어합니다.
- 테스트는 IP 제한, 전체 제한, lease 이중 dispose, 반납된 슬롯 재사용을 검증합니다.

다음 단계에서는 짧은 시간에 반복되는 연결 시도를 제한하는 token bucket 기반 IP별 접속 속도 제한과 임시 차단 정책을 추가할 수 있습니다.

### 다음 단계 17. Token bucket 접속 속도 제한과 임시 차단

이번 step에서는 동시 접속 수뿐 아니라 짧은 시간에 반복되는 TCP 접속 시도도 IP별로 제한합니다.

현재 정책:

```text
초기 token: 10개
refill: 초당 2개
접속 시도 비용: token 1개
token 소진 후 연속 위반 3회: 30초 임시 차단
사용하지 않은 IP bucket: 10분 후 정리
```

Token bucket은 평상시에는 refill 속도로 접속을 허용하면서, 순간적인 정상 재접속은 bucket 용량만큼 burst로 받아들입니다. token이 부족하면 다음 token이 생길 때까지의 `RetryAfter`를 계산해 거부 응답에 포함합니다.

같은 IP가 token이 없는 상태에서 계속 접속을 시도하면 위반 횟수가 증가합니다. 세 번째 연속 위반부터 30초 동안 `TemporarilyBlocked`가 되며, 차단 중 요청에도 남은 시간을 반환합니다. 차단 시간이 끝나면 위반 횟수를 초기화하고 elapsed time에 따라 refill된 token을 사용할 수 있습니다.

처리 순서:

```text
TCP accept
-> IP token bucket 검사
-> 동시 접속 admission 검사
-> lease 확보
-> 클라이언트 작업 시작
```

속도 제한을 동시 접속 제한보다 먼저 검사하므로 서버가 가득 찬 상태에서 반복되는 접속 시도도 rate limiter에 기록됩니다. IPv4-mapped IPv6 주소 역시 IPv4로 정규화해 같은 IP bucket을 공유합니다.

메모리 보호를 위해 10분 동안 사용되지 않았고 현재 차단 중이 아닌 IP bucket을 주기적으로 제거합니다. 이 정리가 없다면 공격자가 출발지 주소를 계속 바꿀 때 dictionary가 끝없이 증가할 수 있습니다.

공부 포인트:

- 동시 접속 제한은 현재 사용량을, rate limit은 시간당 요청 빈도를 제어합니다.
- token bucket은 평균 속도 제한과 제한된 burst 허용을 함께 표현할 수 있습니다.
- `RetryAfter`는 클라이언트가 언제 재시도해야 하는지 알려줍니다.
- 가짜 시계를 주입하면 refill과 30초 차단 해제를 실제 대기 없이 결정적으로 테스트할 수 있습니다.
- 테스트는 burst 소진, rate limit, 임시 차단, IPv4/IPv6 정규화, 차단 해제와 refill을 검증합니다.

다음 단계에서는 로그인 시도에도 별도의 계정/IP rate limit을 적용하고, 실패 횟수 기반 지수 backoff로 인증 공격을 완화할 수 있습니다.

### 다음 단계 18. 인증 시도 제한과 지수 backoff

이번 step에서는 TCP 접속 속도 제한과 별도로 `/login` 실패를 IP와 계정 키 기준으로 추적합니다.

```text
첫 실패:  1초 대기
두 번째:  2초 대기
세 번째:  4초 대기
네 번째:  8초 대기
...
최대:    30초 대기
```

로그인 요청은 IP 제한과 계정 제한을 모두 조회하고 더 긴 `RetryAfter`를 적용합니다. 따라서 한 IP에서 계정을 계속 바꾸거나, 여러 IP에서 같은 계정을 공격하는 두 패턴을 함께 제한할 수 있습니다.

현재 프로젝트에는 실제 비밀번호가 없으므로 양수가 아닌 player ID와 잘못된 player ID 형식을 인증 실패로 기록합니다. 유효한 player ID 로그인은 성공으로 처리해 해당 계정의 실패 상태를 초기화합니다. IP 실패 상태는 성공 후에도 유지해 하나의 정상 계정 로그인으로 여러 계정에 대한 IP 제한을 우회하지 못하게 합니다.

```text
/login abc
-> 형식 실패 기록
-> IP backoff 1초

즉시 /login 1001
-> Login temporarily limited
-> Retry after 1 seconds
```

`AuthenticationAttemptLimiter`에는 현재 시간 함수를 주입할 수 있습니다. 테스트는 시간을 직접 전진시켜 실제로 기다리지 않고 IP별 제한, 계정별 제한, `1 → 2 → 4초` 증가와 성공 후 계정 초기화를 검증합니다.

10분 동안 사용하지 않은 IP 및 계정 실패 상태는 자동으로 제거합니다. 최대 지연을 30초로 제한해 실패 횟수가 커져도 `TimeSpan` 계산이 과도하게 증가하지 않도록 했습니다.

공부 포인트:

- 접속 rate limit과 인증 rate limit은 보호하는 자원과 공격 단계가 다릅니다.
- 계정과 IP를 함께 제한해야 분산 공격과 계정 순환 공격을 각각 완화할 수 있습니다.
- 지수 backoff는 반복 실패 비용을 빠르게 높이면서 첫 실수에는 짧은 대기만 부과합니다.
- 성공 시 계정 실패 상태를 초기화하지 않으면 과거 실패가 정상 사용자를 계속 방해할 수 있습니다.
- 클라이언트에는 계정 존재 여부를 노출하지 않는 동일한 제한 메시지를 반환합니다.

다음 단계에서는 학습용 player ID 로그인을 실제 비밀번호 hash 검증과 계정 저장소로 교체하고, 인증 성공 시 추측하기 어려운 session token을 발급하는 구조로 발전할 수 있습니다.

### 다음 단계 19. 비밀번호 hash, 계정 저장소와 session token

이번 step에서는 player ID만 알면 로그인할 수 있던 학습용 인증을 계정과 비밀번호 기반 인증으로 교체했습니다.

새 명령:

```text
/register <playerId> <password>
/login <playerId> <password>
```

비밀번호는 8자 이상 128자 이하만 허용합니다. 서버는 원문 비밀번호를 저장하지 않고 다음 값만 SQLite `accounts` 테이블에 저장합니다.

```text
player_id
password_salt: 계정마다 생성한 16-byte 난수
password_hash: PBKDF2-SHA256 32-byte 결과
iterations: 100,000
created_at
```

같은 비밀번호라도 salt가 다르면 저장되는 hash가 달라집니다. 로그인 시 저장된 salt와 iteration으로 입력 비밀번호를 다시 계산하고, `CryptographicOperations.FixedTimeEquals`로 비교해 비교 시간 차이를 줄입니다.

계정 생성은 `INSERT ... ON CONFLICT DO NOTHING`을 사용합니다. 같은 player ID가 동시에 등록되어도 SQLite primary key가 하나만 성공시키며, 클라이언트에는 계정 존재 여부에 관한 자세한 내부 정보를 노출하지 않습니다. 로그인 실패도 존재하지 않는 계정과 틀린 비밀번호 모두 `Invalid player id or password`로 동일하게 응답합니다. 존재하지 않는 계정도 dummy PBKDF2 검증을 수행해 계정 유무에 따른 큰 응답 시간 차이를 줄입니다.

로그인 성공 흐름:

```text
계정 조회
-> PBKDF2 hash 검증
-> 32-byte 암호학적 난수 생성
-> URL-safe Base64 session token 변환
-> PlayerSession에 player ID와 token 연결
-> 계정 backoff 초기화
```

`PlayerSession.Logout`은 player ID와 함께 session token도 제거합니다. 내부 월드 단위 테스트가 사용하는 `Authenticate(playerId)` API는 유지되며, token을 전달하지 않으면 테스트용 세션에도 안전한 난수 token을 자동 생성합니다.

구성 요소:

- `IAccountRepository`: 계정 생성과 조회 계약
- `SqliteAccountRepository`: 운영 학습용 SQLite 구현
- `InMemoryAccountRepository`: 빠른 명령 테스트 구현
- `PasswordHasher`: salt 생성, PBKDF2 hash와 고정 시간 검증
- `SessionTokenGenerator`: 256-bit URL-safe token 생성

공부 포인트:

- 비밀번호는 암호화 후 복호화하는 데이터가 아니라 단방향 password hash로 검증해야 합니다.
- 일반 SHA-256 한 번은 너무 빠르므로 비밀번호에는 의도적으로 느린 PBKDF2 같은 KDF가 필요합니다.
- salt는 rainbow table과 동일 비밀번호 hash 비교를 어렵게 합니다.
- session token은 순차 player ID와 달리 공격자가 추측하기 어려워야 합니다.
- 저장소 테스트는 SQLite round-trip, 중복 계정 거부, 올바른/틀린 비밀번호를 검증합니다.
- 명령 테스트는 계정 등록, 비밀번호 로그인, 실패 backoff와 token 발급을 검증합니다.

중요한 현재 한계:

현재 TCP protocol에는 TLS가 없으므로 비밀번호와 session token이 네트워크에서 암호화되지 않습니다. 이 단계는 인증 데이터 구조 학습용이며 인터넷에 공개하면 안 됩니다.

다음 단계에서는 TLS로 전송 구간을 암호화하고, session token을 서버 측 세션 저장소에서 만료·폐기·중복 로그인 정책과 함께 관리할 수 있습니다.

### 다음 단계 20. 서버 측 session token 저장소와 만료

이번 step에서는 로그인 응답으로만 전달하던 session token을 서버가 직접 저장하고 검증하도록 변경했습니다.

정책:

```text
token 수명: 30분
계정별 활성 token: 1개
새 비밀번호 로그인: 이전 token 즉시 폐기
/logout: 현재 token 즉시 폐기
/resume <token>: 유효한 token으로 TCP 재접속 세션 복구
```

`SessionTokenStore`는 raw token을 그대로 저장하지 않고 SHA-256 fingerprint를 key로 보관합니다. 서버 메모리 dump나 디버깅 출력에서 저장소 내용이 노출되더라도 raw token을 바로 사용할 수 없게 하기 위한 방어입니다.

발급 흐름:

```text
비밀번호 로그인 성공
-> 256-bit random token 생성
-> SHA-256 fingerprint 계산
-> player ID, 만료 시각과 함께 저장
-> 같은 player ID의 이전 fingerprint 제거
-> raw token은 클라이언트와 PlayerSession에만 전달
```

재접속 시 `/resume <sessionToken>`을 보내면 서버가 fingerprint를 계산해 저장소를 조회합니다. token이 존재하고 만료되지 않았다면 비밀번호를 다시 보내지 않고 해당 player ID로 인증합니다. 존재하지 않거나 만료·폐기된 token에는 동일하게 `Invalid or expired session token`을 반환합니다.

활성 연결도 명령을 처리할 때 managed token을 다시 검증합니다. 다른 위치의 로그인으로 token이 교체되거나 30분이 지나면 캐릭터 dirty state를 먼저 저장하고 익명 상태로 전환합니다. 저장에 실패하면 데이터 손실을 피하기 위해 로그아웃을 미루고 오류를 반환합니다.

내부 월드 테스트가 `Authenticate(playerId)`로 만든 세션은 token 저장소 밖의 테스트 세션이므로 managed token 검사를 받지 않습니다. 비밀번호 로그인과 `/resume`처럼 token을 명시해 인증한 실제 세션만 검증 대상입니다.

공부 포인트:

- token 자체가 인증 정보이므로 비밀번호와 마찬가지로 노출되면 안 됩니다.
- 서버 저장소에는 raw token 대신 fingerprint를 저장하면 저장소 노출 피해를 줄일 수 있습니다.
- 고정 만료는 탈취된 token을 영구적으로 사용할 수 없게 합니다.
- 계정별 단일 token 정책은 새 로그인 시 이전 재접속 권한을 무효화합니다.
- logout은 클라이언트 상태만 지우는 것이 아니라 서버 token도 폐기해야 합니다.
- 테스트는 발급, 검증, token rotation, 폐기, 만료, `/resume`, 활성 세션 만료를 검증합니다.

현재 `SessionTokenStore`는 단일 서버 프로세스 메모리에 있습니다. 서버 재시작 시 모든 token이 사라지며, 여러 서버 인스턴스가 token을 공유할 수도 없습니다. 이후 Redis 같은 공유 저장소로 교체할 수 있습니다.

중요하게도 TCP 전송은 아직 평문입니다. 다음 단계에서는 `SslStream`과 서버 인증서를 적용하고, 클라이언트가 인증서 chain과 서버 이름을 검증하도록 만들어 비밀번호와 token의 전송 구간을 암호화합니다.

### 다음 단계 21. SslStream TLS 전송 암호화

이번 step에서는 `NetworkStream` 위에 `SslStream`을 추가해 protocol header와 body 전체를 TLS로 암호화합니다. 비밀번호, session token, 채팅과 게임 명령이 더 이상 평문 TCP payload로 전송되지 않습니다.

서버 연결 계층:

```text
TcpListener
-> TcpClient
-> NetworkStream
-> SslStream server handshake
-> MessageProtocol
-> ChatCommandHandler
```

클라이언트도 TCP 연결 직후 TLS client handshake를 완료한 뒤에만 nickname과 사용자 입력을 보냅니다. 기존 `MessageProtocol`과 `ClientConnection`은 `NetworkStream` 대신 기반 `Stream`을 받도록 바꿨기 때문에 framing과 명령 코드는 TLS 구현 세부 사항을 알 필요가 없습니다.

지원 protocol:

```text
TLS 1.2
TLS 1.3
handshake timeout: 10초
```

학습용 인증서:

- 첫 서버 실행 시 `Data/tls/server.pfx` private-key 인증서를 생성합니다.
- 클라이언트 pinning용 공개 인증서는 `Data/tls/server.cer`에 생성합니다.
- SAN에는 `localhost`, 현재 machine name, `127.0.0.1`, `::1`을 넣습니다.
- 유효 기간은 생성 시점부터 1년입니다.
- PFX와 CER은 `.gitignore`에 포함해 저장소에 올리지 않습니다.

클라이언트 검증은 두 조건을 모두 요구합니다.

1. 접속할 때 사용한 host가 인증서 SAN과 일치해야 합니다.
2. 서버가 제시한 인증서 raw bytes가 pin된 `server.cer`와 고정 시간 비교에서 일치해야 합니다.

self-signed 개발 인증서는 공인 CA chain을 만들 수 없으므로 chain 오류는 정확한 pin과 host 검증이 성공한 경우에만 허용합니다. 다른 인증서나 이름 불일치는 거부합니다.

외부 인증서 설정:

```text
서버:
SOCKETSTUDY_TLS_PFX=<server.pfx 경로>
SOCKETSTUDY_TLS_PASSWORD=<PFX 비밀번호>

클라이언트:
SOCKETSTUDY_TLS_CERT=<pin할 공개 인증서 경로>
```

원격 PC에서는 `server.cer`만 안전한 별도 경로로 복사해야 합니다. private key가 들어 있는 `server.pfx`는 서버 밖으로 복사하면 안 됩니다. 접속 host는 인증서 SAN에 포함된 machine name이어야 하며, 실제 서비스에서는 서비스 DNS 이름이 들어간 CA 발급 인증서를 사용해야 합니다.

Admission과 TLS 순서:

```text
TCP accept
-> IP rate limit / 동시 접속 제한
-> 허용된 연결만 TLS handshake
```

공격자가 비싼 TLS handshake를 무제한 유발하지 못하도록 admission 검사는 TLS보다 먼저 수행합니다. 따라서 rate limit이나 접속 수 제한에서 거부된 연결은 암호화되지 않은 Notice를 보내지 않고 즉시 닫습니다. 클라이언트에는 TLS handshake 실패 또는 연결 종료로 보입니다.

공부 포인트:

- TLS는 application protocol을 교체하지 않고 그 아래 stream을 암호화할 수 있습니다.
- 암호화만 하고 인증서를 검증하지 않으면 중간자 공격을 막을 수 없습니다.
- pinning은 정확한 인증서를 강하게 확인하지만 인증서 교체 시 클라이언트 pin도 배포해야 합니다.
- handshake timeout은 데이터를 보내지 않는 연결이 admission slot을 계속 점유하지 못하게 합니다.
- private key 파일과 비밀번호는 source control에 포함하면 안 됩니다.
- 통합 테스트는 실제 loopback TCP에서 TLS handshake, pin 검증, 암호화 상태와 protocol 왕복을 검증합니다.

다음 단계에서는 인증서 만료 모니터링과 무중단 certificate rotation을 추가하고, 운영 환경에서는 개발용 PFX 자동 생성을 금지하는 설정 계층으로 발전할 수 있습니다.

### 다음 단계 22. 인증서 만료 모니터링과 무중단 rotation

이번 step에서는 서버 인증서를 시작할 때 한 번만 읽는 대신 `TlsServerCertificateProvider`가 관리하도록 변경했습니다.

monitor 정책:

```text
PFX 변경 검사: 1분 주기
만료 경고 시작: 만료 30일 전
같은 인증서 경고 반복: 최대 하루 1회
손상되거나 유효하지 않은 replacement: 거부하고 현재 인증서 유지
```

provider는 PFX의 수정 시각과 파일 크기를 확인합니다. 변경된 파일을 발견하면 새 인증서를 별도로 로드하고 다음 항목을 검증합니다.

- private key 포함
- 현재 시각이 `NotBefore` 이후
- 현재 시각이 `NotAfter` 이전
- 기존 인증서와 다른 thumbprint

검증에 성공하면 현재 인증서를 교체합니다. 이미 연결된 `SslStream`은 기존 인증서로 수립된 TLS session을 계속 사용하고, 교체 이후의 새 handshake만 새 인증서를 사용합니다. 이전 인증서 객체는 서버 종료까지 보존해 진행 중인 handshake와 기존 연결의 native TLS 참조가 안전하게 유지되도록 합니다.

잘못된 PFX를 배포하면 reload 오류를 기록하고 기존 인증서를 계속 제공합니다. 파일 identity를 성공 상태로 갱신하지 않으므로 다음 monitor tick에서 다시 로드를 시도합니다.

Production 안전 정책:

```text
SOCKETSTUDY_ENVIRONMENT=Production
SOCKETSTUDY_TLS_PFX=<운영 PFX 경로>
SOCKETSTUDY_TLS_PASSWORD=<PFX 비밀번호>
```

Production에서는 `SOCKETSTUDY_TLS_PFX`가 없으면 서버 시작을 거부합니다. 개발용 self-signed 인증서를 조용히 생성해 운영 서버가 잘못된 인증서로 시작하는 일을 막습니다.

pinning을 사용하는 클라이언트의 무중단 rotation에는 신뢰 overlap 기간이 필요합니다. `SOCKETSTUDY_TLS_CERT`는 운영체제의 path separator로 여러 공개 인증서 경로를 받을 수 있습니다. Windows에서는 세미콜론을 사용합니다.

```text
SOCKETSTUDY_TLS_CERT=C:\certs\current.cer;C:\certs\next.cer
```

권장 rotation 순서:

1. 다음 인증서의 공개 CER를 클라이언트 pin set에 먼저 배포합니다.
2. 모든 클라이언트가 current와 next를 신뢰하는 overlap 기간을 둡니다.
3. 서버 PFX 파일을 next 인증서로 원자적으로 교체합니다.
4. monitor가 reload 성공과 새 thumbprint를 기록했는지 확인합니다.
5. 충분한 전환 기간 후 클라이언트 pin set에서 old 인증서를 제거합니다.

서버 PFX부터 먼저 교체하면 아직 next pin을 받지 못한 클라이언트가 접속하지 못합니다. pinning은 강한 검증을 제공하는 대신 인증서 배포 순서를 운영자가 책임져야 합니다.

공부 포인트:

- certificate rotation은 기존 연결을 끊지 않고 새 연결에만 새 인증서를 적용할 수 있습니다.
- 새 파일은 current를 교체하기 전에 완전히 로드하고 검증해야 합니다.
- 만료 경고는 충분히 일찍 시작하되 매 tick 반복되어 실제 장애 로그를 묻지 않아야 합니다.
- current/next overlap pin은 강한 pinning을 유지하면서 무중단 전환을 가능하게 합니다.
- 테스트는 만료 경고, 정상 PFX reload, 손상된 PFX 거부, thumbprint 유지, Production 시작 거부와 overlap pin 검증을 수행합니다.

다음 단계에서는 환경 변수를 흩어 읽는 방식을 strongly typed 서버 설정으로 통합하고, 시작 시 모든 설정을 한 번에 검증·출력하는 configuration 계층을 추가할 수 있습니다.

### 다음 단계 23. Strongly typed 서버 configuration

이번 step에서는 운영 설정을 `ServerOptions` 하나로 읽고 검증한 뒤 `ChatServer`와 TLS 계층에 주입합니다. 환경 변수를 기능 클래스가 직접 읽지 않으므로 테스트와 설정 추적이 쉬워졌습니다.

지원 설정: `SOCKETSTUDY_ENVIRONMENT`, `SOCKETSTUDY_PORT`, `SOCKETSTUDY_DATABASE`, `SOCKETSTUDY_MAX_CONNECTIONS`, `SOCKETSTUDY_MAX_CONNECTIONS_PER_IP`, `SOCKETSTUDY_SHUTDOWN_SECONDS`, `SOCKETSTUDY_TLS_HANDSHAKE_SECONDS`, `SOCKETSTUDY_TLS_PFX`, `SOCKETSTUDY_TLS_PASSWORD`.

시작 순서는 `명령행 포트와 환경 변수 읽기 -> 타입 변환 -> 전체 검증 -> 안전한 설정 요약 로그 -> 서버 객체 생성`입니다. 잘못된 포트, 접속 제한 관계, 0 이하 timeout, Production 인증서 누락을 한 번에 보고합니다. PFX 비밀번호는 `ToSafeSummary`에 절대 포함하지 않습니다.

게임 밸런스와 월드 simulation 규칙은 운영 배포 설정과 성격이 다르므로 `WorldRules`에 유지했습니다. 테스트는 여러 설정 오류의 동시 수집과 비밀값 redaction을 검증합니다.

다음 단계에서는 문자열 로그를 구조화된 event, level, property 형태로 기록하고 실행 중 로그 레벨을 제어할 수 있게 합니다.

### 다음 단계 24. 구조화 로그와 로그 레벨

파일 로그를 `socket-study.jsonl` JSON Lines 형식으로 변경했습니다. 각 행은 `timestamp`, `level`, `event`, `message`, `properties`를 가지므로 수집기에서 player ID나 event 이름으로 검색할 수 있습니다. 기존 `Info/Error` 호출은 호환성을 유지하며 새 코드에서는 event와 구조화 property를 전달할 수 있습니다.

`SOCKETSTUDY_LOG_LEVEL`은 `Debug`, `Information`, `Warning`, `Error` 중 하나이며 최소 level보다 낮은 로그는 콘솔과 파일에서 모두 제외됩니다. 설정 요약에는 비밀값이 포함되지 않습니다. 테스트는 JSON field와 property의 타입 보존을 검증합니다.

다음 단계에서는 접속, 명령, 저장, tick의 counter와 latency를 수집하는 서버 metrics registry를 추가합니다.

### 다음 단계 25. 서버 metrics와 상태 진단

`ServerMetrics`가 accepted/rejected/active connections, received messages, processed commands와 평균 command latency를 수집합니다. counter와 gauge는 `Interlocked`로 갱신해 접속 hot path에서 전역 lock을 사용하지 않습니다.

`/metrics`는 한 시점의 immutable `ServerMetricsSnapshot`을 읽어 사람이 확인할 수 있는 문자열로 반환합니다. latency는 각 요청을 보관하지 않고 elapsed tick 합계와 처리 횟수만 저장하므로 메모리 사용량이 요청 수에 따라 증가하지 않습니다. 테스트는 counter, active gauge와 10ms/20ms 처리의 15ms 평균을 검증합니다.

다음 단계에서는 metrics와 서버 lifecycle, DB/TLS 상태를 조합한 liveness/readiness health model을 추가합니다.

### 다음 단계 26. Liveness와 readiness

`ServerHealthService`는 lifecycle, 데이터베이스 디렉터리와 TLS 인증서 만료를 조합합니다. liveness는 프로세스가 종료되지 않았는지, readiness는 새 게임 트래픽을 안전하게 받을 수 있는지를 의미합니다. Running에서만 ready이며 Draining은 live지만 ready가 아닙니다.

플레이어 HP `/health`와 구분하기 위해 `/server-health`와 `/ready`가 같은 immutable report를 반환합니다. 실패 이유는 `lifecycle=Draining`, `database-directory-unavailable`, `tls-certificate-expired`처럼 노출됩니다. 테스트는 Running ready와 Draining live/not-ready 전이를 검증합니다.

다음 단계에서는 진단·운영 명령을 일반 사용자에게서 분리하는 관리자 role과 authorization policy를 추가합니다.

### 다음 단계 27. 관리자 role과 명령 authorization

`SOCKETSTUDY_ADMIN_PLAYER_IDS`에 쉼표로 구분한 player ID를 설정합니다. `/metrics`, `/server-health`, `/ready`, `/spawn-monster`는 command dispatch 초기에 관리자 여부를 검사하며 미인증 또는 일반 계정에는 동일한 `Administrator permission required` 응답을 반환합니다.

권한 검사를 각 명령 구현에 흩뜨리지 않고 관리자 명령 집합과 하나의 policy delegate로 관리합니다. 테스트는 일반 사용자의 운영 명령 거부를 검증합니다.

다음 단계에서는 한 계정의 동시 로그인과 연결별 session ownership을 원자적으로 관리합니다.

### 다음 단계 28. 중복 로그인과 session ownership

`SessionOwnershipRegistry`는 player ID와 connection UUID를 원자적으로 연결합니다. 비밀번호 로그인과 `/resume` 모두 소유권 획득에 실패하면 `Player is already logged in`으로 거부합니다. logout과 network disconnect는 본인 connection ID가 소유자일 때만 반납하므로 오래된 연결의 cleanup이 새 소유권을 지우지 못합니다.

현재 정책은 기존 플레이어를 kick하지 않고 새 접속을 거부합니다. 테스트는 단일 소유자, 비소유자 release 무효와 정상 release 후 재획득을 검증합니다.

다음 단계에서는 session과 cache를 프로세스 메모리 구현 뒤의 인터페이스로 분리해 Redis adapter를 연결할 수 있는 경계를 만듭니다.

### 다음 단계 29. 공유 session·cache 추상화

`ISessionOwnershipStore`와 `ISharedCache`를 추가했습니다. command handler는 더 이상 concrete ownership registry에 의존하지 않으며 현재 `SessionOwnershipRegistry`가 인터페이스를 구현합니다. `InMemorySharedCache`는 string key/value와 absolute TTL, lazy expiration 계약을 제공합니다.

이 단계는 Redis 네트워크 의존성을 바로 추가하는 대신 원자적 acquire/release와 TTL semantics를 먼저 고정합니다. Redis adapter는 ownership에 `SET key owner NX PX`, release에 owner 비교 Lua script, cache에 `SET EX`를 대응시킬 수 있습니다. 테스트는 cache hit와 정확한 만료 경계를 검증합니다.

다음 단계에서는 DB provider와 schema migration을 분리하고 PostgreSQL 운영 전환 경계를 추가합니다.

### 다음 단계 30. PostgreSQL provider와 versioned migration

`DatabaseProvider` 설정과 순서가 보장된 `DatabaseMigrationCatalog`를 추가했습니다. `SOCKETSTUDY_DATABASE_PROVIDER=PostgreSql`은 `SOCKETSTUDY_POSTGRES_CONNECTION`을 필수로 검증합니다. `PostgreSqlMigrationRunner`는 Npgsql 8.0.9로 연결하고 각 migration을 transaction 안에서 적용한 뒤 `schema_migrations`에 version을 기록합니다.

현재 학습 서버의 runtime character repository 기본값은 SQLite이며 PostgreSQL 전환 전 migration을 별도 적용할 수 있습니다. migration은 SQLite/PostgreSQL SQL을 함께 가져 provider별 schema drift를 줄입니다. 테스트는 version 순서, 중복과 provider SQL 누락을 검증합니다.

다음 단계에서는 인증과 월드 기능을 application service 경계로 분리합니다.

### 다음 단계 31. 인증 service와 월드 service 경계

`AuthenticationService`가 account repository와 password hasher를 감싸 registration/credential verification use case를 제공합니다. command handler는 SQLite, PBKDF2 구현을 알지 않습니다. `WorldSessionService`는 dirty save 성공 후에만 logout하는 데이터 보존 규칙을 application boundary로 묶습니다.

이 분리는 이후 인증 프로세스를 별도 서버로 옮길 때 command protocol이 local method 대신 RPC client를 호출하도록 교체할 지점을 만듭니다. 기존 repository 단위 테스트와 command 통합 테스트가 경계를 통과해 유지됩니다.

다음 단계에서는 클라이언트 진입점과 backend 선택을 담당하는 gateway routing model을 추가합니다.

### 다음 단계 32. Gateway와 다중 world routing

`GatewayRouter`가 world backend heartbeat를 server ID별로 upsert하고 map ID, heartbeat freshness, capacity와 load ratio로 backend를 선택합니다. stale server와 full server는 후보에서 제외하며 동일 부하에서는 server ID로 결정적 선택을 합니다.

현재는 routing domain model이며 다음 배포 단계에서 gateway process의 접속 응답으로 사용할 수 있습니다. 테스트는 stale 제외, map isolation과 least-loaded 선택을 검증합니다.

다음 단계에서는 서버 사이의 도메인 event를 전달하는 message bus 계약을 추가합니다.

### 다음 단계 33. 서버 간 event message bus

`IServerEventBus`와 `ServerEventEnvelope`를 추가했습니다. envelope는 event ID, topic, source server, 발생 시각과 payload를 포함합니다. `InMemoryServerEventBus`는 topic subscriber snapshot을 병렬 실행하며 subscription dispose로 안전하게 해제합니다.

향후 Redis Streams, RabbitMQ, Kafka adapter는 같은 계약을 구현할 수 있습니다. 실제 broker는 at-least-once 중복 전달이 가능하므로 event ID를 소비자 idempotency key로 사용해야 합니다. 테스트는 topic 전달과 unsubscribe를 검증합니다.

다음 단계에서는 동시 가상 사용자와 latency percentile을 측정하는 load test harness를 추가합니다.

### 다음 단계 34. 부하 테스트와 latency percentile

`LoadTestRunner`는 virtual user 수와 user별 request 수를 받아 operation을 동시에 실행합니다. 성공/실패, elapsed time, requests/sec, average, p50, p95, p99 latency를 반환하며 모든 개별 latency는 테스트 실행 범위에서만 보관합니다.

평균만으로는 tail latency 장애를 숨길 수 있으므로 p95/p99를 함께 봅니다. cancellation은 실패로 삼키지 않고 호출자에게 전파합니다. 테스트는 4 users x 5 requests와 percentile 순서를 검증합니다.

다음 단계에서는 backup, 복구, container 배포, CI 검증과 운영 runbook을 추가합니다.

### 다음 단계 35. 장애 복구와 배포 자동화

`SqliteBackupService`는 SQLite online backup API로 consistent snapshot을 만들고 즉시 `PRAGMA integrity_check`를 실행합니다. 단순 파일 복사는 WAL transaction과 불일치할 수 있으므로 사용하지 않습니다. 테스트는 정상 backup과 손상 파일 거부를 검증합니다.

Dockerfile은 .NET 8 multi-stage restore/publish/runtime image를 사용하며 Data와 logs를 volume으로 분리합니다. compose는 restart policy와 persistent volume을 제공합니다. GitHub Actions는 Windows Schannel TLS 테스트를 포함해 restore, Release build, 전체 protocol test를 실행합니다. `Docs/operations-runbook.md`에는 deploy, backup, incident, certificate rotation 순서를 기록했습니다.

여기까지 단일 학습 서버에서 시작해 보안 인증, persistence, 운영 진단, 확장 경계와 배포 복구 기반까지 구축했습니다. 다음 학습 주기는 실제 Redis/PostgreSQL/broker 인스턴스를 docker compose에 연결하고 다중 process end-to-end test를 수행하는 단계입니다.
