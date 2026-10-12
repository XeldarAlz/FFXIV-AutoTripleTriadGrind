using System.Numerics;

namespace AutoTripleTriadGrind.Core.Travel;

internal readonly record struct EntranceRoute(uint SourceTerritoryId, uint TargetTerritoryId, Vector3 Position, uint EntranceBaseId);

internal static class EntranceRoutes
{
    private const uint ThePillarsTerritoryId = 419;
    private const uint FortempsManorTerritoryId = 433;
    private const uint DomanEnclaveTerritoryId = 759;
    private const uint GangosTerritoryId = 915;
    private const uint YakTelTerritoryId = 1189;
    private const uint LowerJeunoTerritoryId = 1265;

    private const uint HouseFortempsGuardId = 1011217;
    private const uint GangosAirshipPilotId = 1032884;
    private const uint LowerJeunoPassageId = 2014450;

    private static readonly EntranceRoute[] routes =
    [
        new(ThePillarsTerritoryId, FortempsManorTerritoryId, new Vector3(17.990356f, 16.009666f, -9.567444f), HouseFortempsGuardId),
        new(DomanEnclaveTerritoryId, GangosTerritoryId, new Vector3(127.27539f, -4.1787133f, 98.68005f), GangosAirshipPilotId),
        new(YakTelTerritoryId, LowerJeunoTerritoryId, new Vector3(-528.37415f, -152.20874f, 671.13745f), LowerJeunoPassageId),
    ];

    public static bool TryGet(uint targetTerritoryId, out EntranceRoute route)
    {
        for (var routeIndex = 0; routeIndex < routes.Length; routeIndex++)
        {
            if (routes[routeIndex].TargetTerritoryId == targetTerritoryId)
            {
                route = routes[routeIndex];
                return true;
            }
        }

        route = default;
        return false;
    }
}
