// A world event paired with the connection used to resolve its AOI recipients.
public sealed record QueuedWorldEvent(ClientConnection Source, WorldEvent Event);
