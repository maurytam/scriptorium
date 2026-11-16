namespace Scriptorium.Core.Enums;

public enum AskFailureKind
{
    InvalidQuestion,
    NotFound,
    NotReady,
    ModelUnavailable,
    Timeout,
    Internal,

    /// <summary>The preference cannot be used for this document (Cloud for a private one).</summary>
    PreferenceNotAllowed
}
