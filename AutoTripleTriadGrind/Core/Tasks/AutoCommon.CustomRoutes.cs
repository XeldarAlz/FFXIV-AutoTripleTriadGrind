using AutoTripleTriadGrind.Core.Game.Ops;
using AutoTripleTriadGrind.Core.Ipc;
using AutoTripleTriadGrind.Core.Travel;
using clib.TaskSystem;
using ECommons.DalamudServices;
using System.Threading.Tasks;

namespace AutoTripleTriadGrind.Core.Tasks;

public abstract partial class AutoCommon
{
    private readonly HashSet<uint> activeCustomRoutes = [];

    private async Task<bool> FollowCustomRoute(uint territoryId)
    {
        territoryId = CustomTravelRoutes.ResolveTerritoryId(territoryId);
        if (Svc.ClientState.TerritoryType == territoryId)
            return await WaitForPlayerReady();

        if (!CustomTravelRoutes.TryGet(territoryId, Plugin.Instance.Configuration, out var steps)
            || !CustomTravelRoutes.IsValid(territoryId, steps))
        {
            Warn($"Custom route: invalid entry route for territory {territoryId}.");
            return false;
        }
        if (!activeCustomRoutes.Add(territoryId))
        {
            Warn($"Custom route: circular entry route for territory {territoryId}.");
            return false;
        }
        try
        {
            var start = 0;
            for (var index = 0; index < steps.Length; index++)
            {
                if (steps[index].SourceTerritoryId == Svc.ClientState.TerritoryType)
                    start = index;
            }
            for (var index = start; index < steps.Length; index++)
            {
                if (CancelToken.IsCancellationRequested || !await FollowCustomStep(steps[index]))
                    return false;
            }
            return Svc.ClientState.TerritoryType == territoryId;
        }
        finally
        {
            activeCustomRoutes.Remove(territoryId);
            NavmeshIPC.Instance.Stop();
        }
    }

    private async Task<bool> FollowCustomStep(CustomTravelStep step)
    {
        var targetName = TerritoryNames.Of(step.TargetTerritoryId);
        Diag($"Custom route: {step.SourceTerritoryId} -> {step.TargetTerritoryId}, entrance {step.EntranceBaseId}.");
        if (step.AethernetDestinationId != 0 && !await RideCustomRouteAethernet(step))
            return false;
        // Normal travel treats a boundary's expected map change as leaving the destination.
        var approached = step.EntranceBaseId == 0
            ? await WaitForPlayerReady() && (Svc.ClientState.TerritoryType == step.SourceTerritoryId
                || await EnterTerritory(step.SourceTerritoryId, step.Position, TerritoryNames.Of(step.SourceTerritoryId)))
            : await TravelTo(step.SourceTerritoryId, step.Position, step.ApproachMeters);
        if (!approached || !await SafeDismount("custom-route-entry") || CancelToken.IsCancellationRequested)
            return false;

        NavmeshIPC.Instance.Stop();
        if (step.EntranceBaseId == 0)
        {
            await WaitForNavmeshReady(TravelNavmeshWaitMs, TravelNavmeshPollFrames);
            if (CancelToken.IsCancellationRequested)
                return false;
            var crossing = new MoveOp(move => move.MoveInZone(step.Position,
                MovementConfig.Default.WithTolerance(0.1f),
                () => Svc.ClientState.TerritoryType != step.SourceTerritoryId));
            await RunCancellable(crossing, step.TimeoutMs, "custom-route-crossing");
            if (crossing.Fault is { } fault)
            {
                Warn($"Custom route: boundary movement failed: {fault.Message}");
                return false;
            }
        }

        Status = $"Entering {targetName}";
        var deadline = Environment.TickCount64 + step.TimeoutMs;
        var interactionSent = false;
        var approachAttempted = false;
        while (Environment.TickCount64 < deadline && !CancelToken.IsCancellationRequested)
        {
            if (Svc.ClientState.TerritoryType == step.TargetTerritoryId)
            {
                Diag($"Custom route: entered {targetName} ({step.TargetTerritoryId}).");
                return await WaitForPlayerReady();
            }
            if (Svc.ClientState.TerritoryType != step.SourceTerritoryId)
            {
                Warn($"Custom route: entered territory {Svc.ClientState.TerritoryType} instead of {targetName} ({step.TargetTerritoryId}).");
                return false;
            }
            if (interactionSent)
                AdvanceCustomEntryDialog(step);
            else if (step.EntranceBaseId != 0 && !DialogDriver.AnyOpen() && NpcInteraction.PlayerReady()
                && NpcInteraction.FindNearest(step.EntranceBaseId) is { } entrance
                && Svc.Objects.LocalPlayer is { } player)
            {
                if (!step.CanInteract(player.Position, entrance.Position))
                {
                    // TravelTo can report arrival at a snapped floor point that is still out of interaction range.
                    // Repeating it can complete synchronously forever on the framework thread.
                    if (approachAttempted)
                    {
                        Warn($"Custom route: entrance {step.EntranceBaseId} is beyond the {step.InteractMeters:F1}m interaction limit after approaching; stopping this step.");
                        return false;
                    }

                    approachAttempted = true;
                    var remainingMs = (int)Math.Min(10_000, deadline - Environment.TickCount64);
                    if (remainingMs <= 0)
                        break;

                    var approach = new MoveOp(move => move.MoveInZone(entrance.Position,
                        MovementConfig.Default.WithTolerance(0.5f),
                        () => step.CanInteract(player.Position, entrance.Position)));
                    await RunCancellable(approach, remainingMs, "custom-route-approach",
                        StuckDetector.MoveStallAbort("custom-route-approach"));
                    if (approach.Fault is { } approachFault)
                    {
                        Warn($"Custom route: entrance approach failed: {approachFault.Message}");
                        return false;
                    }

                    // Always yield before reevaluating the object and the character's position.
                    await NextFrame(5);
                    continue;
                }
                NpcInteraction.Target(entrance);
                NpcInteraction.Interact(entrance);
                interactionSent = true;
                Diag($"Custom route: interacted with entrance {step.EntranceBaseId}; waiting for territory {step.TargetTerritoryId}.");
            }
            await NextFrame(5);
        }
        if (!CancelToken.IsCancellationRequested)
        {
            if (interactionSent)
                await SettleDialog();
            else
            {
                var entrance = NpcInteraction.FindNearest(step.EntranceBaseId);
                var entranceState = entrance is null
                    ? "no targetable entrance with this ID was found"
                    : Svc.Objects.LocalPlayer is { } player
                        ? $"entrance is {System.Numerics.Vector3.Distance(player.Position, entrance.Position):F1}m away"
                        : "character is unavailable";
                Warn($"Custom route: entrance {step.EntranceBaseId} interaction was not sent: {entranceState}; dialog open: {DialogDriver.AnyOpen()}; blockers: {NpcInteraction.DescribeBlockers()}.");
            }
            Warn($"Custom route: timed out entering {targetName} ({step.TargetTerritoryId}); current territory {Svc.ClientState.TerritoryType}, interaction sent: {interactionSent}. Check the entrance and access requirements.");
        }
        return false;
    }

    private async Task<bool> RideCustomRouteAethernet(CustomTravelStep step)
    {
        if (!await WaitForPlayerReady()
            || (Svc.ClientState.TerritoryType != step.SourceTerritoryId
                && !await EnterTerritory(step.SourceTerritoryId, step.Position, TerritoryNames.Of(step.SourceTerritoryId))))
            return false;

        if (!ZoneAetherytes.TryFindAttunedAethernetStop(step.SourceTerritoryId, step.AethernetDestinationId, out var stop))
        {
            Diag($"Custom route: aethernet stop {step.AethernetDestinationId} is unavailable; walking to the entrance instead.");
            return true;
        }
        // Already near the entrance: do not walk back to a shard for a ride that takes us farther away.
        if (Svc.Objects.LocalPlayer is not { } player
            || GroundDistance.Between(player.Position, step.Position) <= GroundDistance.Between(stop.Position, step.Position))
            return true;

        if (!await SafeDismount("custom-route-aethernet"))
            return false;
        await WaitForNavmeshReady(AethernetNavmeshWaitMs, AethernetNavmeshPollFrames);
        if (CancelToken.IsCancellationRequested)
            return false;

        await RunWithStatusPinned($"Riding the aethernet to {stop.Name}", async () =>
        {
            Diag($"Custom route: riding the aethernet to {stop.Name} ({stop.Id}) before approaching entrance {step.EntranceBaseId}.");
            var operation = new MoveOp(move => move.Aethernet(step.SourceTerritoryId, stop.Position));
            var completed = await RunCancellable(operation, AethernetLegMs, "custom-route-aethernet");
            if (operation.Fault is { } fault)
                Diag($"Custom route: aethernet ride faulted: {fault.Message}; walking to the entrance instead.");
            else if (!completed && !CancelToken.IsCancellationRequested)
                Diag("Custom route: aethernet ride did not complete; walking to the entrance instead.");
        });
        return !CancelToken.IsCancellationRequested;
    }

    private static void AdvanceCustomEntryDialog(CustomTravelStep step)
    {
        if (NpcInteraction.SelectStringOpen())
        {
            var index = !string.IsNullOrEmpty(step.MenuEntryText)
                ? NpcInteraction.FindSelectStringEntry(step.MenuEntryText)
                : step.MenuEntryIndex ?? NpcInteraction.NoEntry;
            NpcInteraction.SelectEntry(index);
        }
        else if (NpcInteraction.SelectYesnoOpen())
        {
            if (step.ConfirmEntry)
                DialogDriver.AdvanceAccepting();
            else
                NpcInteraction.AnswerNo();
        }
        else
            NpcInteraction.ProgressTalk();
    }
}
