using AutoTripleTriadGrind.Core.Game.Ops;
using AutoTripleTriadGrind.Core.Ipc;
using AutoTripleTriadGrind.Core.Travel;
using ECommons.DalamudServices;
using System.Numerics;
using System.Threading.Tasks;

namespace AutoTripleTriadGrind.Core.Tasks;

public abstract partial class AutoCommon
{
    private const float EntranceApproachMeters = 3f;
    private const float EntranceInteractMeters = 3f;
    private const float EntranceWalkToleranceMeters = 0.5f;
    private const int EntranceWalkWatchdogMs = 10_000;
    private const int EntranceTimeoutMs = 30_000;

    private async Task<bool> EnterThroughEntrance(EntranceRoute route)
    {
        var targetName = TerritoryNames.Of(route.TargetTerritoryId);
        Diag($"Travel: entering {targetName} ({route.TargetTerritoryId}) through entrance {route.EntranceBaseId} in territory {route.SourceTerritoryId}");
        try
        {
            if (!await TravelTo(route.SourceTerritoryId, route.Position, EntranceApproachMeters) || !await SafeDismount("entrance")
                || CancelToken.IsCancellationRequested)
            {
                return false;
            }

            NavmeshIPC.Instance.Stop();
            return await InteractWithEntrance(route, targetName);
        }
        finally
        {
            NavmeshIPC.Instance.Stop();
        }
    }

    private async Task<bool> InteractWithEntrance(EntranceRoute route, string targetName)
    {
        Status = $"Entering {targetName}";
        var deadline = Environment.TickCount64 + EntranceTimeoutMs;
        var interacted = false;
        var walkedUp = false;
        while (Environment.TickCount64 < deadline && !CancelToken.IsCancellationRequested)
        {
            var territoryId = Svc.ClientState.TerritoryType;
            if (territoryId == route.TargetTerritoryId)
            {
                Diag($"Travel: entered {targetName} ({route.TargetTerritoryId})");
                return await WaitForPlayerReady();
            }

            if (territoryId != route.SourceTerritoryId)
            {
                Warn($"Travel: entered territory {territoryId} instead of {targetName} ({route.TargetTerritoryId})");
                return false;
            }

            if (interacted)
            {
                DialogDriver.AdvanceAccepting();
            }
            else if (!DialogDriver.AnyOpen() && NpcInteraction.PlayerReady() && NpcInteraction.FindNearest(route.EntranceBaseId) is { } entrance)
            {
                var entrancePosition = entrance.Position;
                if (WithinEntranceReach(entrancePosition))
                {
                    NpcInteraction.Target(entrance);
                    NpcInteraction.Interact(entrance);
                    interacted = true;
                    Diag($"Travel: interacted with entrance {route.EntranceBaseId}; waiting for territory {route.TargetTerritoryId}");
                }
                else if (walkedUp)
                {
                    Warn($"Travel: entrance {route.EntranceBaseId} is still beyond {EntranceInteractMeters:F0}m after walking up to it");
                    return false;
                }
                else
                {
                    walkedUp = true;
                    await WalkUpToEntrance(entrancePosition);
                }
            }

            await NextFrame(5);
        }

        if (CancelToken.IsCancellationRequested)
        {
            return false;
        }

        if (interacted)
        {
            await SettleDialog();
        }

        var entranceFound = NpcInteraction.FindNearest(route.EntranceBaseId) is not null;
        Warn($"Travel: timed out entering {targetName} ({route.TargetTerritoryId}); still in territory {Svc.ClientState.TerritoryType}, entrance found: {entranceFound}, interacted: {interacted}, blockers: {NpcInteraction.DescribeBlockers()}");
        return false;
    }

    private async Task WalkUpToEntrance(Vector3 entrancePosition)
    {
        var walk = new MoveOp(move => move.MoveInZone(entrancePosition, walkMovement.WithTolerance(EntranceWalkToleranceMeters), () => WithinEntranceReach(entrancePosition)));
        await RunCancellable(walk, EntranceWalkWatchdogMs, "entrance-walk", StuckDetector.MoveStallAbort("entrance-walk"));
        if (walk.Fault is { } fault)
        {
            Diag($"Travel: the walk up to the entrance faulted: {fault.Message}");
        }
    }

    private static bool WithinEntranceReach(Vector3 entrancePosition)
        => Svc.Objects.LocalPlayer is { } player && Vector3.Distance(player.Position, entrancePosition) <= EntranceInteractMeters;
}
