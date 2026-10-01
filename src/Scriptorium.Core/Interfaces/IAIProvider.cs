using Scriptorium.Core.Ai;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Interfaces;

/// <summary>The boundary between the domain and any answering model.</summary>
public interface IAIProvider
{
    /// <summary>
    /// Asks the model to continue the conversation. Expected problems come back as a failed <see cref="AiResult"/>;
    /// cancellation requested by the caller propagates as <see cref="OperationCanceledException"/>.
    /// </summary>
    Task<AiResult> CompleteAsync(AiRequest request, CancellationToken ct);
}
