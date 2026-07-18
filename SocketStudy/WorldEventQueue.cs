// Stores world events until the dispatch phase sends them to interested clients.
public sealed class WorldEventQueue
{
    private readonly Queue<QueuedWorldEvent> events = new();
    private readonly object syncRoot = new();

    public int Count
    {
        get
        {
            lock (syncRoot)
            {
                return events.Count;
            }
        }
    }

    public void Enqueue(QueuedWorldEvent queuedEvent)
    {
        ArgumentNullException.ThrowIfNull(queuedEvent);
        lock (syncRoot)
        {
            events.Enqueue(queuedEvent);
        }
    }

    public bool TryDequeue(out QueuedWorldEvent? queuedEvent)
    {
        lock (syncRoot)
        {
            if (events.Count == 0)
            {
                queuedEvent = null;
                return false;
            }

            queuedEvent = events.Dequeue();
            return true;
        }
    }
}
