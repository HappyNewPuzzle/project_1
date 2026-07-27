// Transfers combat notifications from the world tick to network dispatch.
public sealed class CombatEventQueue
{
    private readonly Queue<CombatNotification> notifications = new();
    private readonly object syncRoot = new();

    public void Enqueue(CombatNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        lock (syncRoot)
        {
            notifications.Enqueue(notification);
        }
    }

    public bool TryDequeue(out CombatNotification? notification)
    {
        lock (syncRoot)
        {
            if (notifications.Count == 0)
            {
                notification = null;
                return false;
            }

            notification = notifications.Dequeue();
            return true;
        }
    }
}
