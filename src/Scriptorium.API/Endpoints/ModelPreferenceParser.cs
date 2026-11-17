using Scriptorium.Core.Enums;

namespace Scriptorium.API.Endpoints;

public static class ModelPreferenceParser
{
    /// <summary>Absent, null or empty is Auto; otherwise exactly auto, local or cloud in any letter case.</summary>
    public static bool TryParse(string? value, out ModelPreference preference)
    {
        preference = ModelPreference.Auto;
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        return value.ToLowerInvariant() switch
        {
            "auto" => true,
            "local" => Chosen(ModelPreference.Local, out preference),
            "cloud" => Chosen(ModelPreference.Cloud, out preference),
            _ => false
        };
    }

    private static bool Chosen(ModelPreference chosen, out ModelPreference preference)
    {
        preference = chosen;
        return true;
    }
}
