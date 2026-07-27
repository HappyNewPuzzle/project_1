public sealed class ConnectionAdmissionLease : IDisposable
{
    private Action? release;

    internal ConnectionAdmissionLease(Action release)
    {
        this.release = release;
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref release, null)?.Invoke();
    }
}
