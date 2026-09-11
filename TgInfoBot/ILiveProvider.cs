namespace TgInfoBot
{
    /// <summary>
    /// Generic interface for live data providers that supply date ranges.
    /// </summary>
    public interface ILiveProvider
    {
        /// <summary>Active date ranges from live provider.</summary>
        (DateTimeOffset from, DateTimeOffset to)[] Ranges { get; }

        /// <summary>When the data was last updated.</summary>
        DateTimeOffset LastUpdated { get; }

        /// <summary>Whether the provider has loaded data.</summary>
        bool HasData { get; }
    }
}
