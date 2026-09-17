namespace Mentora.Core.Options;

public sealed class LiveKitOptions
{
    public const string SectionName = "LiveKit";

    public string Url { get; init; } = "";
    public string ApiKey { get; init; } = "";
    public string ApiSecret { get; init; } = "";
    public int JoinBeforeMinutes { get; init; } = 15;
    public int JoinAfterEndMinutes { get; init; } = 15;
}
