public sealed record ClientTaskWaitResult(
    bool Completed,
    int RemainingTaskCount,
    TimeSpan Elapsed);
