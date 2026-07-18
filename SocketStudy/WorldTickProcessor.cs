// Applies queued world input at a server-controlled simulation boundary.
public sealed class WorldTickProcessor
{
    private readonly MovementRequestQueue movementRequests;

    public WorldTickProcessor(MovementRequestQueue movementRequests)
    {
        this.movementRequests = movementRequests;
    }

    public WorldTickResult ProcessOnce()
    {
        var processedMovements = new List<ProcessedMovement>();

        while (movementRequests.TryDequeue(out QueuedMovementRequest? queuedRequest))
        {
            if (queuedRequest is null)
            {
                continue;
            }

            MovementTickResult result = MovementTickProcessor.Process(
                queuedRequest.Session,
                queuedRequest.Request);

            processedMovements.Add(new ProcessedMovement(queuedRequest, result));
        }

        return new WorldTickResult(processedMovements);
    }
}
