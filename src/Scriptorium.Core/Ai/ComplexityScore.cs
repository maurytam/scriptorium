using System.Numerics;
using Scriptorium.Core.Enums;

namespace Scriptorium.Core.Ai;

/// <summary>How demanding a question is: the signals that fired, and their count (0 to 5).</summary>
public sealed record ComplexityScore(ComplexitySignals Signals)
{
    public int Value => BitOperations.PopCount((uint)Signals);
}
