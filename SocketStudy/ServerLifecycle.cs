public sealed class ServerLifecycle
{
    private int state = (int)ServerLifecycleState.Starting;

    public ServerLifecycleState State => (ServerLifecycleState)Volatile.Read(ref state);

    public bool MarkRunning()
    {
        return TryTransition(ServerLifecycleState.Starting, ServerLifecycleState.Running);
    }

    public bool BeginDraining()
    {
        while (true)
        {
            ServerLifecycleState current = State;
            if (current is ServerLifecycleState.Draining or ServerLifecycleState.Stopped)
            {
                return false;
            }

            if (TryTransition(current, ServerLifecycleState.Draining))
            {
                return true;
            }
        }
    }

    public bool MarkStopped()
    {
        return TryTransition(ServerLifecycleState.Draining, ServerLifecycleState.Stopped);
    }

    private bool TryTransition(ServerLifecycleState expected, ServerLifecycleState next)
    {
        int previous = Interlocked.CompareExchange(ref state, (int)next, (int)expected);
        return previous == (int)expected;
    }
}
