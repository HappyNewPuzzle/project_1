public sealed class TlsCertificateMonitorLoop
{
    private readonly TlsServerCertificateProvider certificates;
    private readonly TimeSpan interval;

    public TlsCertificateMonitorLoop(
        TlsServerCertificateProvider certificates,
        TimeSpan interval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        this.certificates = certificates;
        this.interval = interval;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                certificates.RefreshIfChanged();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
