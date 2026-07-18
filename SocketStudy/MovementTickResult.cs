// 서버 tick 이동 처리 결과입니다.
public sealed record MovementTickResult(
    // 이동 요청이 실제 세션 상태에 적용되었는지 나타냅니다.
    bool IsAccepted,
    // 거절되었을 때 클라이언트에 알려줄 이유입니다.
    string? RejectionReason)
{
    // 성공 결과를 만듭니다.
    public static MovementTickResult Accepted() => new(true, null);

    // 거절 결과를 만듭니다.
    public static MovementTickResult Rejected(string reason) => new(false, reason);
}
