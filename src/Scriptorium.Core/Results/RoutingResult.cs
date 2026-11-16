using System.Diagnostics.CodeAnalysis;
using Scriptorium.Core.Enums;
using Scriptorium.Core.Interfaces;

namespace Scriptorium.Core.Results;

/// <summary>The outcome of a routing decision: the backend with the provider to call, or the reason it was refused.</summary>
public sealed class RoutingResult
{
    private RoutingResult(ModelBackend? backend, IAIProvider? provider, RoutingFailureKind? failureKind, string? error)
    {
        Backend = backend;
        Provider = provider;
        FailureKind = failureKind;
        Error = error;
    }

    public ModelBackend? Backend { get; }

    public IAIProvider? Provider { get; }

    public RoutingFailureKind? FailureKind { get; }

    public string? Error { get; }

    [MemberNotNullWhen(true, nameof(Backend), nameof(Provider))]
    [MemberNotNullWhen(false, nameof(FailureKind), nameof(Error))]
    public bool IsSuccess => Provider is not null;

    public static RoutingResult Success(ModelBackend backend, IAIProvider provider) => new(backend, provider, null, null);

    public static RoutingResult Failure(RoutingFailureKind kind, string error) => new(null, null, kind, error);
}
