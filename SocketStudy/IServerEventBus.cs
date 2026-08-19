public interface IServerEventBus
{
    IDisposable Subscribe(string topic, Func<ServerEventEnvelope, Task> handler);
    Task PublishAsync(ServerEventEnvelope envelope, CancellationToken cancellationToken = default);
}
