namespace TgInfoBot
{
    public interface IMercuryRetrogradeProvider
    {
        (DateTimeOffset from, DateTimeOffset to)[] Ranges { get; }
        DateTimeOffset LastUpdated { get; }
        bool HasData { get; }
    }
}
