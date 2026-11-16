namespace Scriptorium.Core.Enums;

/// <summary>Why the router refused to choose a backend.</summary>
public enum RoutingFailureKind
{
    /// <summary>The cloud was asked for but it is switched off or has no key.</summary>
    CloudNotAvailable,

    /// <summary>The cloud was asked for a private document, which is only ever answered on this computer.</summary>
    PrivateDocumentCloudRefused
}
