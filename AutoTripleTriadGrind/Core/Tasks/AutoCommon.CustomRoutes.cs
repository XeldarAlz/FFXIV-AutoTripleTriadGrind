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
        while (Environment.TickCount64 < deadline && !CancelToken.IsCancellationRequested)
        {
            if (Svc.ClientState.TerritoryType == step.TargetTerritoryId)
                return await WaitForPlayerReady();
            if (Svc.ClientState.TerritoryType != step.SourceTerritoryId)
            {
                Warn($"Custom route: entered an unexpected map instead of {targetName}.");
                return false;
            }
            if (interactionSent)
                AdvanceCustomEntryDialog(step);
            else if (step.EntranceBaseId != 0 && !DialogDriver.AnyOpen() && NpcInteraction.PlayerReady()
                && NpcInteraction.FindNearest(step.EntranceBaseId) is { } entrance)
            {
                if (!WithinReach(entrance.Position, step.ApproachMeters))
                {
                    if (!await TravelTo(step.SourceTerritoryId, entrance.Position, step.ApproachMeters))
                        return false;
                    continue;
                }
                NpcInteraction.Target(entrance);
                NpcInteraction.Interact(entrance);
                interactionSent = true;
            }
            await NextFrame(5);
        }
        if (!CancelToken.IsCancellationRequested)
        {
            if (interactionSent)
                await SettleDialog();
            Warn($"Custom route: timed out entering {targetName}; check the entrance and access requirements.");
        }
        return false;
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
