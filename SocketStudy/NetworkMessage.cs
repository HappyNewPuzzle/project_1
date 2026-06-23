// protocol이 읽고 쓰는 메시지 단위입니다.
public sealed record NetworkMessage(MessageType Type, string Text);
