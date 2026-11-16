namespace Scriptorium.Core.Ai;

/// <summary>How questions are routed between the local and the cloud model (bound from the "Routing" configuration section).</summary>
public sealed record RoutingOptions
{
    /// <summary>The only switch for the cloud. It is off unless the owner turns it on.</summary>
    public bool CloudEnabled { get; init; } = false;

    /// <summary>A question is complex, and Auto may prefer the cloud, when its score is above this value.</summary>
    public int ComplexityThreshold { get; init; } = 2;
}
