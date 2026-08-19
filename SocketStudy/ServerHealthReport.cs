public sealed record ServerHealthReport(bool Live, bool Ready, string[] Reasons)
{
    public string Format() =>
        $"Health: live={Live.ToString().ToLowerInvariant()}, ready={Ready.ToString().ToLowerInvariant()}, " +
        $"reasons={(Reasons.Length == 0 ? "none" : string.Join("; ", Reasons))}";
}
