using System.Collections.Concurrent;

public sealed class InMemoryServerEventBus : IServerEventBus
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Func<ServerEventEnvelope, Task>>> handlers = new(StringComparer.Ordinal);
    public IDisposable Subscribe(string topic, Func<ServerEventEnvelope, Task> handler)
    {
        Guid id = Guid.NewGuid();
        handlers.GetOrAdd(topic, _ => new())[id] = handler;
        return new Subscription(() => handlers.GetValueOrDefault(topic)?.TryRemove(id, out _));
    }
    public async Task PublishAsync(ServerEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Func<ServerEventEnvelope, Task>[] targets = handlers.GetValueOrDefault(envelope.Topic)?.Values.ToArray() ?? [];
        await Task.WhenAll(targets.Select(handler => handler(envelope)));
    }
    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? action = dispose;
        public void Dispose() => Interlocked.Exchange(ref action, null)?.Invoke();
    }
}
