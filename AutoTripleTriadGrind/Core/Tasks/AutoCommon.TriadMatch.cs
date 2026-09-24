using AutoTripleTriadGrind.Core.Game.Ops;
using AutoTripleTriadGrind.Core.Planning;
using AutoTripleTriadGrind.Core.Triad.Addons;
using AutoTripleTriadGrind.Core.Triad.Data;
using AutoTripleTriadGrind.Core.Triad.Decks;
using AutoTripleTriadGrind.Core.Triad.Logic;
using AutoTripleTriadGrind.Core.Triad.Logic.Agents;
using AutoTripleTriadGrind.Core.Triad.Match;
using System.Threading.Tasks;
using RewardAgent = AutoTripleTriadGrind.Core.Triad.Addons.TriadAgent;

namespace AutoTripleTriadGrind.Core.Tasks;

public abstract partial class AutoCommon
{
    private const int WindowTimeoutMs = 15_000;
    private const int MatchTimeoutMs = 8 * TimeUnits.MillisecondsPerMinute;
    private const int UnreadableBoardFallbackMs = 20_000;
    private const int RetryFrames = 15;
    private const int DeckSelectAttempts = 12;
    private const int RegionalSlots = 2;

    // Returns a result to leave the NPC with, or null to go round the NPC loop again (for example to register a card).
    private async Task<NpcRunResult?> PlayMatchSeries(Configuration configuration, TriadNpcRun run, AutoTriadSession session, TriadProgress progress)
    {
        while (!CancelToken.IsCancellationRequested)
        {
            if (!await AcceptChallenge(configuration, run, progress))
            {
                await LeaveMatchWindows();
                return null;
            }

            if (!await SelectDeck(configuration))
            {
                Warn($"Deck selection for {run.Name} did not go through.");
                await LeaveMatchWindows();
                return new NpcRunResult(NpcRunEnd.Skipped, SkipReason.InteractFailed);
            }

            progress.SetPhase(TriadPhase.Playing);
            if (!await PlayBoard(run))
            {
                await LeaveMatchWindows();
                return null;
            }

            var (outcome, rewardItem) = await ReadResult();
            run.Matches++;
            run.LossStreak = outcome == MatchOutcome.Lost ? run.LossStreak + 1 : 0;
            session.RecordMatch(outcome);
            progress.RecordNpcMatch(outcome);
            var newCard = TriadData.Set.CardIdByItemId.TryGetValue(rewardItem, out var cardId) && !TriadOwnership.IsOwned(cardId);
            Diag($"{run.Name}: match {run.Matches} {outcome}, reward item {rewardItem}{(newCard ? $" ({TriadData.Set.CardName(cardId)}, new)" : string.Empty)}.");
            if (newCard)
            {
                session.RecordCardObtained(cardId);
            }

            if (newCard || run.GoalMet(run) || Stop(configuration, run) is not null || run.MatchLimitReached)
            {
                await LeaveMatchWindows();
                return run.MatchLimitReached && !run.GoalMet(run) ? new NpcRunResult(NpcRunEnd.Skipped, SkipReason.MatchLimit) : null;
            }

            if (configuration.DelayBetweenMatchesMs > 0)
            {
                await DelayMs(configuration.DelayBetweenMatchesMs);
            }

            progress.SetPhase(TriadPhase.Rematch);
            if (!await Rematch())
            {
                await LeaveMatchWindows();
                return null;
            }
        }

        return new NpcRunResult(NpcRunEnd.StopRun, SkipReason.None);
    }

    private async Task<bool> AcceptChallenge(Configuration configuration, TriadNpcRun run, TriadProgress progress)
    {
        if (!await WaitUntilTimed(static () => TriadAddons.IsReady(TriadAddons.Request) || TriadAddons.IsVisible(TriadAddons.DeckSelect), WindowTimeoutMs, "triad-request"))
        {
            return false;
        }

        RememberRegionalRules(configuration, run);
        if (!await EnsureDeck(configuration, run, progress))
        {
            return false;
        }

        progress.SetPhase(TriadPhase.Challenging);
        var deadline = Environment.TickCount64 + WindowTimeoutMs;
        while (Environment.TickCount64 < deadline && !CancelToken.IsCancellationRequested)
        {
            if (TriadAddons.IsVisible(TriadAddons.DeckSelect) || TriadAddons.IsVisible(TriadAddons.Board))
            {
                return true;
            }

            // A Triple Triad prompt, such as the fee confirmation, can sit between Challenge and the deck window.
            if (TriadDialog.Advance() != TriadDialog.Step.Handled)
            {
                TriadMatchOps.ClickChallenge();
            }

            await NextFrame(RetryFrames);
        }

        return TriadAddons.IsVisible(TriadAddons.DeckSelect) || TriadAddons.IsVisible(TriadAddons.Board);
    }

    private static void RememberRegionalRules(Configuration configuration, TriadNpcRun run)
    {
        Span<TriadRuleId> regional = stackalloc TriadRuleId[RegionalSlots];
        if (!TriadMatchOps.TryReadRegionalRules(regional) || regional.SequenceEqual(run.Regional))
        {
            return;
        }

        regional.CopyTo(run.Regional);
        configuration.RegionalRules[run.Npc.TriadRowId] = [.. run.Regional];
        configuration.SaveDebounced();
    }

    // Waits for the deck built for this NPC's rules and writes it into the configured slot.
    private async Task<bool> EnsureDeck(Configuration configuration, TriadNpcRun run, TriadProgress progress)
    {
        if (configuration.DeckSource != DeckSource.Optimized)
        {
            return ProfileDecks.HasCards(configuration.OwnDeckSlot) || WarnFalse($"Saved deck {configuration.OwnDeckSlot + 1} is empty.");
        }

        var ruleMask = TriadDeckBuilder.RuleMask(run.NpcIndex, run.Regional);
        if (!TriadDeckBuilder.TryGetCached(configuration, run.NpcIndex, ruleMask, out var deck))
        {
            progress.SetPhase(TriadPhase.Optimizing);
            if (ruleMask != run.DeckRuleMask || !TriadDeckBuilder.IsBuilding)
            {
                run.DeckRuleMask = ruleMask;
                TriadDeckBuilder.Start(configuration, run.NpcIndex, ruleMask);
            }

            while (!CancelToken.IsCancellationRequested)
            {
                progress.SetOptimizerProgress(TriadDeckBuilder.Progress);
                if (TriadDeckBuilder.TryCollect(configuration) is not null || !TriadDeckBuilder.IsBuilding)
                {
                    break;
                }

                await NextFrame(10);
            }

            if (!TriadDeckBuilder.TryGetCached(configuration, run.NpcIndex, ruleMask, out deck))
            {
                return WarnFalse($"No deck could be built for {run.Name}.");
            }
        }

        run.DeckRuleMask = ruleMask;
        run.DeckReady = true;
        return ProfileDecks.TryWrite(configuration.OptimizedDeckSlot, deck.Cards) || WarnFalse("The optimized deck could not be written to the deck slot.");
    }

    private async Task<bool> SelectDeck(Configuration configuration)
    {
        var slot = configuration.DeckSource == DeckSource.Optimized ? configuration.OptimizedDeckSlot : configuration.OwnDeckSlot;
        if (!await WaitUntilTimed(static () => TriadAddons.IsReady(TriadAddons.DeckSelect) || TriadAddons.IsVisible(TriadAddons.Board), WindowTimeoutMs, "triad-deck-select"))
        {
            return false;
        }

        for (var attempt = 0; attempt < DeckSelectAttempts && !CancelToken.IsCancellationRequested; attempt++)
        {
            if (TriadMatchOps.DeckChosen())
            {
                return true;
            }

            TriadMatchOps.ChooseDeck(slot, attempt);
            for (var wait = 0; wait < 2 && !TriadMatchOps.DeckChosen(); wait++)
            {
                await NextFrame(RetryFrames);
            }
        }

        return TriadMatchOps.DeckChosen();
    }

    // The solver runs off the game thread; the board is only read, and a card only played, on the framework thread.
    private async Task<bool> PlayBoard(TriadNpcRun run)
    {
        TriadRolloutAgent.MaxParallelism = Math.Max(1, Environment.ProcessorCount / 4);
        var memory = run.Memory;
        var screen = run.Screen;
        Task<(bool Found, int Card, int Cell)>? solving = null;
        var needMove = true;
        var couldAct = false;
        long unreadableSince = 0;
        var deadline = Environment.TickCount64 + MatchTimeoutMs;
        while (!CancelToken.IsCancellationRequested)
        {
            if (TriadAddons.IsVisible(TriadAddons.Result))
            {
                return true;
            }

            if (Environment.TickCount64 > deadline)
            {
                Warn($"The match against {run.Name} ran past {MatchTimeoutMs / TimeUnits.MillisecondsPerMinute} minutes.");
                return false;
            }

            if (!TriadMatchOps.TryReadBoard(screen, out var frame))
            {
                await NextFrame(2);
                continue;
            }

            if (frame.CanAct && !couldAct)
            {
                needMove = true;
            }

            couldAct = frame.CanAct;
            if (solving is not null)
            {
                if (solving.IsCompleted)
                {
                    var move = solving.IsCompletedSuccessfully ? solving.Result : default;
                    solving = null;
                    if (move.Found && frame.CanAct)
                    {
                        TriadMatchOps.PlaceCard(move.Card, move.Cell);
                        needMove = false;
                    }
                }

                await NextFrame(1);
                continue;
            }

            if (!frame.CanAct || !needMove)
            {
                unreadableSince = 0;
                await NextFrame(1);
                continue;
            }

            if (frame.ReadFailed)
            {
                unreadableSince = unreadableSince == 0 ? Environment.TickCount64 : unreadableSince;
                if (Environment.TickCount64 - unreadableSince > UnreadableBoardFallbackMs && TriadMatchOps.PlayAnyCard(screen))
                {
                    Warn("Part of the board could not be read, so a card was played without the solver.");
                    needMove = false;
                    unreadableSince = 0;
                }

                await NextFrame(5);
                continue;
            }

            unreadableSince = 0;
            memory.OnNewScan(screen, run.NpcDeck);
            var snapshot = new TriadGameState(memory.GameState);
            var solver = memory.Solver;
            solving = Task.Run(() =>
            {
                var found = solver.FindNextMove(snapshot, out var card, out var cell, out _);
                // Under Chaos the game only accepts the card it picked, whatever the solver prefers.
                if (snapshot.ForcedCardIndex >= 0)
                {
                    card = snapshot.ForcedCardIndex;
                }

                return (found && card >= 0 && cell >= 0, card, cell);
            });
            await NextFrame(1);
        }

        return false;
    }

    private async Task<(MatchOutcome Outcome, uint RewardItem)> ReadResult()
    {
        await WaitUntilTimed(TriadMatchOps.ResultReady, WindowTimeoutMs, "triad-result");
        var outcome = MatchOutcome.Unknown;
        var deadline = Environment.TickCount64 + 3_000;
        while (outcome == MatchOutcome.Unknown && Environment.TickCount64 < deadline && !CancelToken.IsCancellationRequested)
        {
            outcome = TriadMatchOps.ReadOutcome();
            if (outcome == MatchOutcome.Unknown)
            {
                await NextFrame(5);
            }
        }

        // The reward lands a moment after the result opens.
        await DelayMs(500);
        return (outcome, RewardAgent.ReadRewardItemId());
    }

    private async Task<bool> Rematch()
    {
        for (var attempt = 0; attempt < 6 && !CancelToken.IsCancellationRequested; attempt++)
        {
            if (TriadAddons.IsVisible(TriadAddons.Request) || TriadAddons.IsVisible(TriadAddons.DeckSelect))
            {
                return true;
            }

            TriadMatchOps.Rematch();
            await NextFrame(RetryFrames * 2);
        }

        return TriadAddons.IsVisible(TriadAddons.Request) || TriadAddons.IsVisible(TriadAddons.DeckSelect);
    }

    // Closes whatever match window is open, as the player would with the window's own quit or close.
    internal async Task LeaveMatchWindows()
    {
        for (var attempt = 0; attempt < 20 && !CancelToken.IsCancellationRequested; attempt++)
        {
            if (!TriadMatchOps.CloseOneWindow())
            {
                return;
            }

            await NextFrame(RetryFrames);
        }
    }

    private bool WarnFalse(string message)
    {
        Warn(message);
        return false;
    }
}
