// Preserves the server receive order of player attack requests.
public sealed class PlayerAttackRequestQueue
{
    private readonly Queue<QueuedPlayerAttackRequest> requests = new();
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

    public void Enqueue(QueuedPlayerAttackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (syncRoot)
        {
            requests.Enqueue(request);
        }
    }

    public bool TryDequeue(out QueuedPlayerAttackRequest? request)
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
