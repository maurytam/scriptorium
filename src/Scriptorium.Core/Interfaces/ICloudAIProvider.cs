namespace Scriptorium.Core.Interfaces;

/// <summary>The cloud model. Same promises as <see cref="IAIProvider"/>, plus the knowledge of whether it can be used at all.</summary>
public interface ICloudAIProvider : IAIProvider
{
    /// <summary>
    /// True only when the provider has what it needs to call the service (a key). When false,
    /// <see cref="IAIProvider.CompleteAsync"/> fails as unavailable without sending anything.
    /// </summary>
    bool IsConfigured { get; }
}
