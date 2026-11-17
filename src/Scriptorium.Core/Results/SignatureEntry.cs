namespace Scriptorium.Core.Results;

/// <summary>
/// One digital signature of a document as the file declares it. <paramref name="Signer"/> is null for an unknown signer;
/// <paramref name="SignedAt"/> is null when the file declares no date. Never stored.
/// </summary>
public sealed record SignatureEntry(string? Signer, string? SignedAt);
