public sealed class SystemRandomSource : IRandomSource
{
    public static SystemRandomSource Shared { get; } = new();

    private SystemRandomSource()
    {
    }

    public double NextDouble() => Random.Shared.NextDouble();
}
