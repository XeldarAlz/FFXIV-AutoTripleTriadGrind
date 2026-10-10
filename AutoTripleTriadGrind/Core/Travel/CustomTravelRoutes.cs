using System.Numerics;

namespace AutoTripleTriadGrind.Core.Travel;

// A step approaches an entrance in its source map and transitions to the target map.
// EntranceBaseId == 0 means walking through a zone boundary.
public sealed class CustomTravelStep
{
    public uint SourceTerritoryId { get; set; }
    public uint TargetTerritoryId { get; set; }
    public Vector3 Position { get; set; }
    public uint EntranceBaseId { get; set; }
    public uint AethernetDestinationId { get; set; }
    public float ApproachMeters { get; set; } = 3f;
    public float InteractMeters { get; set; } = 3f;
    public int TimeoutMs { get; set; } = 30_000;
    public string MenuEntryText { get; set; } = string.Empty;
    public int? MenuEntryIndex { get; set; }
    public bool ConfirmEntry { get; set; } = true;

    internal bool IsValid => SourceTerritoryId != 0 && TargetTerritoryId != 0
        && SourceTerritoryId != TargetTerritoryId && float.IsFinite(ApproachMeters) && ApproachMeters > 0f
        && float.IsFinite(InteractMeters) && InteractMeters > 0f
        && TimeoutMs > 0 && float.IsFinite(Position.X) && float.IsFinite(Position.Y) && float.IsFinite(Position.Z)
        && (MenuEntryIndex is null or >= 0);

    // Interaction uses actual 3D distance with no navigation arrival slack or floor snapping.
    internal bool CanInteract(Vector3 playerPosition, Vector3 entrancePosition)
        => float.IsFinite(InteractMeters) && InteractMeters > 0f
        && Vector3.Distance(playerPosition, entrancePosition) <= InteractMeters;
}

internal static class CustomTravelRoutes
{
    // Questionable's 1475_The Paths We Walk, sequence 0: House Fortemps guard.
    private static readonly Dictionary<uint, CustomTravelStep[]> BuiltIn = new()
    {
        [433] = [new()
        {
            SourceTerritoryId = 419,
            TargetTerritoryId = 433,
            Position = new Vector3(17.990356f, 16.009666f, -9.567444f),
            EntranceBaseId = 1011217,
            MenuEntryIndex = 0,
        }],
        // The permanent Doman Enclave airship pilot. Quest-only pilot 1032869 shares this position.
        [915] = [new()
        {
            SourceTerritoryId = 759,
            TargetTerritoryId = 915,
            Position = new Vector3(127.27539f, -4.1787133f, 98.68005f),
            EntranceBaseId = 1032884,
            AethernetDestinationId = 162,
            ConfirmEntry = true,
        }],
        // CN NPC location data can reference 1264, but observed entrance transitions land in 1265.
        // Questionable's 5236_Jeuno, sequence 2, and 5326_Dreams of Paradise, sequence 3.
        [1265] = [new()
        {
            SourceTerritoryId = 1189,
            TargetTerritoryId = 1265,
            Position = new Vector3(-528.37415f, -152.20874f, 671.13745f),
            EntranceBaseId = 2014450,
            ConfirmEntry = true,
        }],
    };

    public static uint ResolveTerritoryId(uint territoryId) => territoryId == 1264 ? 1265u : territoryId;

    public static bool TryGet(uint territoryId, Configuration configuration, out CustomTravelStep[] steps)
    {
        territoryId = ResolveTerritoryId(territoryId);
        return configuration.CustomRoutes.TryGetValue(territoryId, out steps!) || BuiltIn.TryGetValue(territoryId, out steps!);
    }

    public static bool IsValid(uint territoryId, CustomTravelStep[]? steps)
    {
        if (steps is null || steps.Length == 0 || steps[^1] is null || steps[^1].TargetTerritoryId != territoryId)
        {
            return false;
        }

        for (var index = 0; index < steps.Length; index++)
        {
            if (steps[index] is not { IsValid: true } step
                || (index > 0 && steps[index - 1].TargetTerritoryId != step.SourceTerritoryId))
            {
                return false;
            }
        }

        return true;
    }
}
