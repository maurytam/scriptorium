using Scriptorium.Core.Enums;

namespace Scriptorium.Core.Ai;

/// <summary>The facts a routing decision depends on; the cloud setting is held by the router.</summary>
public sealed record RoutingRequest(bool IsPrivate, ModelPreference Preference, int ComplexityScore);
