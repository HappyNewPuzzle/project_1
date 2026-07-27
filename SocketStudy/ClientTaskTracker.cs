using System.Collections.Concurrent;
using System.Diagnostics;

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

    public async Task<ClientTaskWaitResult> WaitForAllAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var stopwatch = Stopwatch.StartNew();
        Task[] snapshot = tasks.Values.ToArray();
        if (snapshot.Length == 0)
        {
            return new(true, 0, stopwatch.Elapsed);
        }

        Task allTasks = Task.WhenAll(snapshot);
        try
        {
            Task completedTask = await Task.WhenAny(
                allTasks,
                Task.Delay(timeout, cancellationToken));
            if (completedTask != allTasks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (allTasks.IsCompleted)
                {
                    await allTasks;
                    return new(true, 0, stopwatch.Elapsed);
                }

                return new(false, Count, stopwatch.Elapsed);
            }

            await allTasks;
            return new(true, 0, stopwatch.Elapsed);
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
