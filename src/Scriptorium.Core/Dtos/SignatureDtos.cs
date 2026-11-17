using Scriptorium.Core.Results;

namespace Scriptorium.Core.Dtos;

/// <summary>One signature as the file declares it. A null signer is an unknown signer; a null date is no declared date.</summary>
public sealed record SignatureDto(string? Signer, string? SignedAt);

public sealed record SignaturesResponseDto(IReadOnlyList<SignatureDto> Signatures)
{
    /// <summary>Maps a result; a failed one has no entries and yields an empty list.</summary>
    public static SignaturesResponseDto From(SignatureResult result) =>
        new(result.Signatures.Select(entry => new SignatureDto(entry.Signer, entry.SignedAt)).ToList());
}
