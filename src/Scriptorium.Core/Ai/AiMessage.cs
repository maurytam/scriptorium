using Scriptorium.Core.Enums;

namespace Scriptorium.Core.Ai;

public sealed record AiMessage(AiRole Role, string Content);
