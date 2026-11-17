using Scriptorium.Core.Enums;
using Scriptorium.Core.Results;

namespace Scriptorium.Core.Services;

/// <summary>Turns what the router and the providers report into the failure kinds the endpoints map to a status.</summary>
public static class AskFailureMapper
{
    // The router words both refusals itself; a private document asked with Cloud is the one that is a conflict.
    public static AskResult FromRouting(RoutingFailureKind kind, string error) => kind switch
    {
        RoutingFailureKind.PrivateDocumentCloudRefused => AskResult.Failure(AskFailureKind.PreferenceNotAllowed, error),
        _ => AskResult.Failure(AskFailureKind.ModelUnavailable, error)
    };

    // The provider words the two failures the user can act on; anything else stays generic, with no details.
    public static AskResult FromProvider(AiFailureKind kind, string error) => kind switch
    {
        AiFailureKind.Unavailable => AskResult.Failure(AskFailureKind.ModelUnavailable, error),
        AiFailureKind.Timeout => AskResult.Failure(AskFailureKind.Timeout, error),
        _ => AskResult.Failure(AskFailureKind.Internal, "The answer could not be produced.")
    };
}
