public sealed record ServerEventEnvelope(Guid EventId, string Topic, string SourceServerId,
    DateTimeOffset OccurredAt, string Payload);
