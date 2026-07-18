// Stores movement requests in the same order that the server received them.
public sealed class MovementRequestQueue
{
    private readonly Queue<QueuedMovementRequest> requests = new();
    private readonly object syncRoot = new();

    public int Count
    {
        get
        {
            lock (syncRoot)
            {
                return requests.Count;
            }
        }
    }

    public void Enqueue(QueuedMovementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (syncRoot)
        {
            requests.Enqueue(request);
        }
    }

    public bool TryDequeue(out QueuedMovementRequest? request)
    {
        lock (syncRoot)
        {
            if (requests.Count == 0)
            {
                request = null;
                return false;
            }

            request = requests.Dequeue();
            return true;
        }
    }
}
