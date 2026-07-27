using System.Collections.Concurrent;

public sealed class ClientTaskTracker
{
    private readonly ConcurrentDictionary<long, Task> tasks = new();
    private long nextTaskId;

    public int Count => tasks.Count;

    public void Track(Task task)
    {
        ArgumentNullException.ThrowIfNull(task);

        long taskId = Interlocked.Increment(ref nextTaskId);
        tasks[taskId] = task;
        _ = RemoveWhenCompletedAsync(taskId, task);
    }

    public async Task WaitForAllAsync()
    {
        Task[] snapshot = tasks.Values.ToArray();
        if (snapshot.Length == 0)
        {
            return;
        }

        try
        {
            await Task.WhenAll(snapshot);
        }
        finally
        {
            foreach ((long taskId, Task task) in tasks)
            {
                if (task.IsCompleted)
                {
                    tasks.TryRemove(taskId, out _);
                }
            }
        }
    }

    private async Task RemoveWhenCompletedAsync(long taskId, Task task)
    {
        try
        {
            await task;
        }
        catch
        {
            // WaitForAllAsync observes and reports client task failures during shutdown.
        }
        finally
        {
            tasks.TryRemove(taskId, out _);
        }
    }
}
