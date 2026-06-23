using System.Net.Sockets;
using System.Text;

// TCP 바이트 흐름 위에 "메시지"라는 단위를 얹기 위한 protocol helper입니다.
static class MessageProtocol
{
    // 메시지 타입 1바이트와 본문 길이 4바이트를 합친 header 크기입니다.
    private const int HeaderSize = 5;
    // 한 번에 받을 수 있는 메시지 본문의 최대 크기입니다. 너무 큰 메시지로 서버가 메모리를 많이 쓰는 일을 막습니다.
    private const int MaxMessageBytes = 1024 * 1024;

    // 문자열 메시지를 "4바이트 길이 + UTF-8 본문" 형식으로 stream에 씁니다.
    public static async Task WriteMessageAsync(
        NetworkStream stream,
        MessageType type,
        string message,
        CancellationToken cancellationToken = default)
    {
        // 문자열을 UTF-8 바이트 배열로 변환합니다.
        byte[] body = Encoding.UTF8.GetBytes(message);
        // 본문이 너무 크면 protocol 위반으로 보고 보내지 않습니다.
        if (body.Length > MaxMessageBytes)
        {
            // 호출자가 문제를 알 수 있도록 예외를 발생시킵니다.
            throw new InvalidOperationException($"Message is too large: {body.Length} bytes");
        }

        // 4바이트 header 배열을 준비합니다.
        byte[] header = new byte[HeaderSize];
        // 첫 바이트에는 메시지 타입을 기록합니다.
        header[0] = (byte)type;
        // 나머지 4바이트에는 메시지 본문 길이를 네트워크 바이트 순서(big-endian)로 기록합니다.
        WriteInt32BigEndian(header.AsSpan(1), body.Length);

        // 먼저 header를 보냅니다.
        await stream.WriteAsync(header, cancellationToken);
        // 그 다음 실제 메시지 본문을 보냅니다.
        await stream.WriteAsync(body, cancellationToken);
        // 버퍼에 남은 데이터가 있다면 즉시 네트워크로 밀어냅니다.
        await stream.FlushAsync(cancellationToken);
    }

    // stream에서 "4바이트 길이 + UTF-8 본문" 형식의 메시지 하나를 읽습니다.
    public static async Task<NetworkMessage?> ReadMessageAsync(
        NetworkStream stream,
        CancellationToken cancellationToken = default)
    {
        // 5바이트 header를 담을 배열을 준비합니다.
        byte[] header = new byte[HeaderSize];
        // header를 정확히 5바이트 읽습니다. 시작 전에 연결이 닫히면 null이 돌아옵니다.
        bool hasHeader = await ReadExactOrEndAsync(stream, header, cancellationToken);
        // header를 읽기 전에 연결이 끝났으면 메시지가 없다는 뜻입니다.
        if (!hasHeader)
        {
            // 호출자에게 정상 연결 종료를 알립니다.
            return null;
        }

        // 첫 바이트를 메시지 타입으로 해석합니다.
        MessageType type = ParseMessageType(header[0]);
        // header의 나머지 4바이트를 int 길이 값으로 바꿉니다.
        int bodyLength = ReadInt32BigEndian(header.AsSpan(1));
        // 길이가 음수거나 너무 크면 잘못된 protocol 데이터입니다.
        if (bodyLength < 0 || bodyLength > MaxMessageBytes)
        {
            // 잘못된 길이를 받은 연결은 더 읽지 않고 예외로 처리합니다.
            throw new IOException($"Invalid message length: {bodyLength}");
        }

        // 길이가 0인 메시지는 빈 문자열로 처리합니다.
        if (bodyLength == 0)
        {
            // 빈 메시지를 반환합니다.
            return new NetworkMessage(type, string.Empty);
        }

        // 본문 길이만큼 바이트 배열을 준비합니다.
        byte[] body = new byte[bodyLength];
        // 본문을 정확히 bodyLength 바이트만큼 읽습니다.
        bool hasBody = await ReadExactOrEndAsync(stream, body, cancellationToken);
        // header는 왔는데 body가 중간에 끊기면 protocol이 깨진 상태입니다.
        if (!hasBody)
        {
            // 연결이 중간에 끊겼다는 예외를 발생시킵니다.
            throw new IOException("Connection closed before message body was fully received.");
        }

        // UTF-8 바이트를 문자열로 변환해 호출자에게 반환합니다.
        return new NetworkMessage(type, Encoding.UTF8.GetString(body));
    }

    // 요청한 크기만큼 정확히 읽거나, 읽기 시작 전에 연결 종료를 감지합니다.
    private static async Task<bool> ReadExactOrEndAsync(
        NetworkStream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        // 지금까지 읽은 바이트 수입니다.
        int totalRead = 0;

        // buffer 전체가 채워질 때까지 반복합니다.
        while (totalRead < buffer.Length)
        {
            // 아직 채워야 하는 구간을 stream에서 읽습니다.
            int read = await stream.ReadAsync(
                buffer.AsMemory(totalRead, buffer.Length - totalRead),
                cancellationToken);
            // read가 0이면 상대방이 연결을 닫았다는 뜻입니다.
            if (read == 0)
            {
                // 한 바이트도 못 읽은 상태라면 정상적인 연결 종료로 볼 수 있습니다.
                if (totalRead == 0)
                {
                    // 호출자에게 데이터 없이 종료되었다고 알려줍니다.
                    return false;
                }

                // 일부만 읽고 끊긴 것은 메시지가 깨진 상황입니다.
                throw new IOException("Connection closed before the message was fully received.");
            }

            // 이번에 읽은 바이트 수를 누적합니다.
            totalRead += read;
        }

        // 요청한 바이트 수를 모두 읽었습니다.
        return true;
    }

    // int 값을 4바이트 big-endian 배열에 씁니다.
    private static void WriteInt32BigEndian(Span<byte> buffer, int value)
    {
        // 가장 높은 8비트를 첫 번째 바이트에 씁니다.
        buffer[0] = (byte)(value >> 24);
        // 다음 8비트를 두 번째 바이트에 씁니다.
        buffer[1] = (byte)(value >> 16);
        // 다음 8비트를 세 번째 바이트에 씁니다.
        buffer[2] = (byte)(value >> 8);
        // 가장 낮은 8비트를 네 번째 바이트에 씁니다.
        buffer[3] = (byte)value;
    }

    // 4바이트 big-endian 배열에서 int 값을 읽습니다.
    private static int ReadInt32BigEndian(ReadOnlySpan<byte> buffer)
    {
        // 각 바이트를 int로 올린 뒤 자리수에 맞게 shift해서 합칩니다.
        return (buffer[0] << 24)
            | (buffer[1] << 16)
            | (buffer[2] << 8)
            | buffer[3];
    }

    // byte 값을 MessageType으로 변환하고, 모르는 값이면 protocol 오류로 처리합니다.
    private static MessageType ParseMessageType(byte value)
    {
        // byte 값을 enum으로 변환합니다.
        var type = (MessageType)value;
        // 정의된 메시지 타입인지 확인합니다.
        if (!Enum.IsDefined(type))
        {
            // 알 수 없는 타입은 protocol 위반입니다.
            throw new IOException($"Unknown message type: {value}");
        }

        // 검증된 메시지 타입을 반환합니다.
        return type;
    }
}
