using AutoTripleTriadGrind.Core.Game.Ops;
using AutoTripleTriadGrind.Core.Planning;
using AutoTripleTriadGrind.Core.Triad.Addons;
using AutoTripleTriadGrind.Core.Triad.Data;
using AutoTripleTriadGrind.Core.Triad.Decks;
using AutoTripleTriadGrind.Core.Triad.Logic;
using AutoTripleTriadGrind.Core.Triad.Match;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using System.Numerics;
using System.Threading.Tasks;

namespace AutoTripleTriadGrind.Core.Tasks;

internal enum NpcRunEnd : byte
{
    GoalMet,
    Skipped,
    StopRun,
}

internal readonly record struct NpcRunResult(NpcRunEnd End, SkipReason Reason);

// Per-NPC state that must not outlive the NPC: the deck in play, the screen tracker and the streak counters.
internal sealed class TriadNpcRun(ushort npcIndex, Func<TriadNpcRun, bool> goalMet)
{
    public readonly ushort NpcIndex = npcIndex;
    public readonly Func<TriadNpcRun, bool> GoalMet = goalMet;
    public readonly TriadDeck NpcDeck = TriadDeckBuilder.NpcDeck(npcIndex);
    public readonly TriadScreenMemory Memory = new();
    public readonly TriadScreenState Screen = new();
    public readonly TriadRuleId[] Regional = new TriadRuleId[2];
    public int Matches;
    public int LossStreak;
    public int InteractFailures;
    public int FailedSeries;
    public ushort DeckRuleMask = ushort.MaxValue;
    public bool DeckReady;
    // Zero plays on until the goal is met; Collect sets it from the per-NPC match limit.
    public int MatchLimit;

    public bool MatchLimitReached => MatchLimit > 0 && Matches >= MatchLimit;

    public TriadNpc Npc => TriadData.Set.Npcs[NpcIndex];

    public string Name => TriadData.Set.NpcNames[NpcIndex];
}

public abstract partial class AutoCommon
{
    private const float InteractRangeMeters = 4f;
    // Travel counts a stop a couple of metres past its tolerance as arrived, which can leave the NPC out of talking range.
    private const float TalkRangeMeters = 3f;
    private const float TalkApproachToleranceMeters = 1.5f;
    private const int TalkApproachWatchdogMs = 10_000;
    private const int MaxInteractFailures = 6;
    private const int ChallengeOpenTimeoutMs = 15_000;
    private const int DialogSettleTimeoutMs = 8_000;
    private const int MaxFailedSeries = 3;

    // Card items that would not register this run, so each is tried once rather than before every match.
    private readonly HashSet<uint> unregistrableItems = [];

    internal async Task<NpcRunResult> PlayNpc(TriadNpcRun run, AutoTriadSession session, TriadProgress progress)
    {
        var configuration = Plugin.Instance.Configuration;
        progress.BeginNpc(run.NpcIndex);
        LoadRegionalRules(configuration, run);
        PrepareDeck(configuration, run);

        progress.SetPhase(TriadPhase.Travelling);
        var npc = run.Npc;
        if (!await ReachNpc(run))
        {
            return new NpcRunResult(NpcRunEnd.Skipped, SkipReason.Unreachable);
        }

        while (!CancelToken.IsCancellationRequested)
        {
            TriadOwnership.Refresh(force: true);
            if (run.GoalMet(run))
            {
                await LeaveMatchWindows();
                return new NpcRunResult(NpcRunEnd.GoalMet, SkipReason.None);
            }

            if (Stop(configuration, run) is { } limit)
            {
                await LeaveMatchWindows();
                return new NpcRunResult(NpcRunEnd.Skipped, limit);
            }

            progress.SetPhase(TriadPhase.Registering);
            await RegisterPendingCards(session);
            if (CardRegistrar.FreeBagSlots() < configuration.MinFreeBagSlots)
            {
                Svc.Chat.PrintError($"{AttgConstants.LogPrefix} Your bags are nearly full, so the run stops. Free a few slots and start again.");
                return new NpcRunResult(NpcRunEnd.StopRun, SkipReason.InventoryFull);
            }

            if (CardRegistrar.Mgp() < npc.Fee)
            {
                Svc.Chat.PrintError($"{AttgConstants.LogPrefix} You need {npc.Fee} MGP to challenge {run.Name}, so the run stops.");
                return new NpcRunResult(NpcRunEnd.StopRun, SkipReason.NotEnoughMgp);
            }

            progress.SetPhase(TriadPhase.Challenging);
            if (!await OpenChallenge(run))
            {
                run.InteractFailures++;
                if (run.InteractFailures >= MaxInteractFailures)
                {
                    return new NpcRunResult(NpcRunEnd.Skipped, SkipReason.InteractFailed);
                }

                await DelayMs(1_000);
                continue;
            }

            run.InteractFailures = 0;
            var matchesBefore = run.Matches;
            var leave = await PlayMatchSeries(configuration, run, session, progress);
            await SettleDialog();
            if (leave is { } stop)
            {
                return stop;
            }

            run.FailedSeries = run.Matches == matchesBefore ? run.FailedSeries + 1 : 0;
            if (run.FailedSeries >= MaxFailedSeries)
            {
                return new NpcRunResult(NpcRunEnd.Skipped, SkipReason.InteractFailed);
            }
        }

        return new NpcRunResult(NpcRunEnd.StopRun, SkipReason.None);
    }

    private static SkipReason? Stop(Configuration configuration, TriadNpcRun run)
    {
        if (configuration.LossStreakSkip > 0 && run.LossStreak >= configuration.LossStreakSkip)
        {
            return SkipReason.LossStreak;
        }

        return null;
    }

    private static void LoadRegionalRules(Configuration configuration, TriadNpcRun run)
    {
        if (!configuration.RegionalRules.TryGetValue(run.Npc.TriadRowId, out var remembered))
        {
            return;
        }

        for (var index = 0; index < run.Regional.Length && index < remembered.Length; index++)
        {
            run.Regional[index] = remembered[index];
        }
    }

    // Starts the build before travel, so it usually finishes on the way.
    private static void PrepareDeck(Configuration configuration, TriadNpcRun run)
    {
        if (configuration.DeckSource != DeckSource.Optimized)
        {
            run.DeckReady = true;
            return;
        }

        var ruleMask = TriadDeckBuilder.RuleMask(run.NpcIndex, run.Regional);
        run.DeckRuleMask = ruleMask;
        if (TriadDeckBuilder.TryGetCached(configuration, run.NpcIndex, ruleMask, out _))
        {
            run.DeckReady = true;
            return;
        }

        run.DeckReady = false;
        TriadDeckBuilder.Start(configuration, run.NpcIndex, ruleMask);
    }

    private async Task<bool> ReachNpc(TriadNpcRun run)
    {
        var npc = run.Npc;
        if (NpcInteraction.FindNearest(npc.ENpcBaseId) is { } nearby && Svc.ClientState.TerritoryType == npc.TerritoryId
            && WithinReach(nearby.Position, InteractRangeMeters))
        {
            return true;
        }

        if (!await TravelTo(npc.TerritoryId, npc.Position, InteractRangeMeters))
        {
            Warn($"Could not reach {run.Name} in territory {npc.TerritoryId}.");
            return false;
        }

        if (NpcInteraction.FindNearest(npc.ENpcBaseId) is { } found && !WithinReach(found.Position, InteractRangeMeters))
        {
            await TravelTo(npc.TerritoryId, found.Position, InteractRangeMeters);
        }

        await SafeDismount("triad-npc");
        return NpcInteraction.FindNearest(npc.ENpcBaseId) is not null;
    }

    // Talks to the NPC and walks the dialogue until the challenge window opens.
    private async Task<bool> OpenChallenge(TriadNpcRun run)
    {
        if (TriadAddons.AnyMatchWindowVisible())
        {
            return true;
        }

        var deadline = Environment.TickCount64 + ChallengeOpenTimeoutMs;
        var lastInteract = 0L;
        var approached = false;
        while (Environment.TickCount64 < deadline && !CancelToken.IsCancellationRequested)
        {
            if (TriadAddons.IsVisible(TriadAddons.Request) || TriadAddons.IsVisible(TriadAddons.DeckSelect))
            {
                return true;
            }

            var step = TriadDialog.Advance();
            if (step == TriadDialog.Step.UnknownMenu)
            {
                Warn($"{run.Name} opened a menu without a Triple Triad entry.");
                return false;
            }

            if (step == TriadDialog.Step.Nothing && NpcInteraction.PlayerReady() && Environment.TickCount64 - lastInteract > 1_500
                && NpcInteraction.FindNearest(run.Npc.ENpcBaseId) is { } target)
            {
                if (!approached && await ApproachToTalk(run, target))
                {
                    approached = true;
                    deadline = Environment.TickCount64 + ChallengeOpenTimeoutMs;
                    continue;
                }

                NpcInteraction.Target(target);
                NpcInteraction.Interact(target);
                lastInteract = Environment.TickCount64;
            }

            await NextFrame(5);
        }

        Warn($"The challenge window for {run.Name} did not open.");
        return false;
    }

    private async Task<bool> ApproachToTalk(TriadNpcRun run, IGameObject target)
    {
        if (Svc.Objects.LocalPlayer is not { } player)
        {
            return false;
        }

        var distance = Vector3.Distance(player.Position, target.Position);
        if (distance <= TalkRangeMeters)
        {
            return false;
        }

        Diag($"Walking up to {run.Name}, {distance:F1}m away.");
        var approach = new MoveOp(move => move.MoveInZone(target.Position, walkMovement.WithTolerance(TalkApproachToleranceMeters), null));
        await RunCancellable(approach, TalkApproachWatchdogMs, "triad-approach", StuckDetector.MoveStallAbort("triad-approach"));
        if (approach.Fault is { } fault)
        {
            Diag($"The walk up to {run.Name} faulted: {fault.Message}");
        }

        return true;
    }

    private async Task SettleDialog()
    {
        var deadline = Environment.TickCount64 + DialogSettleTimeoutMs;
        while (Environment.TickCount64 < deadline && !CancelToken.IsCancellationRequested)
        {
            if (TriadAddons.AnyMatchWindowVisible())
            {
                TriadMatchOps.CloseOneWindow();
            }
            else if (TriadDialog.Advance(towardChallenge: false) == TriadDialog.Step.Nothing)
            {
                return;
            }

            await NextFrame(5);
        }
    }

    private async Task RegisterPendingCards(AutoTriadSession session)
    {
        for (var attempt = 0; attempt < 20 && !CancelToken.IsCancellationRequested; attempt++)
        {
            var itemId = CardRegistrar.FindUnregisteredCardItem(unregistrableItems);
            if (itemId == 0 || !TriadData.Set.CardIdByItemId.TryGetValue(itemId, out var cardId))
            {
                return;
            }

            if (!await WaitUntilTimed(NpcInteraction.PlayerReady, 10_000, "register-ready"))
            {
                return;
            }

            var cardName = TriadData.Set.CardName(cardId);
            Diag($"Registering {cardName} (item {itemId}).");
            CardRegistrar.Use(itemId);
            var registered = await WaitUntilTimed(() =>
            {
                // Only a prompt about this card is confirmed; anything else is left for the player.
                if (NpcInteraction.SelectYesnoOpen() && NpcInteraction.SelectYesnoText().Contains(cardName, StringComparison.OrdinalIgnoreCase))
                {
                    DialogDriver.Confirm();
                }

                TriadOwnership.Refresh(force: true);
                return TriadOwnership.IsOwned(cardId);
            }, 8_000, "register-card");
            if (!registered)
            {
                Warn($"{cardName} did not register; leaving it in your bags.");
                unregistrableItems.Add(itemId);
                continue;
            }

            session.RecordCardRegistered();
            session.RecordCardObtained(cardId);
            await DelayMs(600);
        }
    }
}
