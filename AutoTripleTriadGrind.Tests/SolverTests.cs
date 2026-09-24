using AutoTripleTriadGrind.Core.Triad.Data;
using AutoTripleTriadGrind.Core.Triad.Logic;
using AutoTripleTriadGrind.Core.Triad.Logic.Agents;
using AutoTripleTriadGrind.Core.Triad.Logic.Rules;
using System.Threading;

namespace AutoTripleTriadGrind.Tests;

public sealed class SolverTests
{
    public SolverTests()
    {
        TestCards.Install();
        TriadRolloutAgent.RolloutCount = 200;
        TriadRolloutAgent.MaxParallelism = 2;
    }

    [Fact]
    public void LiveSolverTakesTheWinningLastMove()
    {
        var solver = TriadSolver.CreateLive();
        solver.InitializeSimulation([]);
        ushort[] blue = [TestCards.Weak, TestCards.Weak, TestCards.Weak, TestCards.Weak, TestCards.Ace];
        ushort[] red = [TestCards.Weak, TestCards.Weak, TestCards.Weak, TestCards.Weak, TestCards.Weak];
        var state = solver.StartSimulation(new TriadDeck(blue), new TriadDeck(red), TriadGameStatus.InProgressBlue);
        int[] cells = [0, 2, 6, 8, 1, 3, 5, 7];
        for (var turn = 0; turn < cells.Length; turn++)
        {
            var deck = state.TurnDeck;
            Assert.True(solver.Simulation.PlaceCard(state, deck.FirstAvailableCard(), deck, state.TurnOwner, cells[turn]));
        }

        Assert.True(solver.FindNextMove(state, out var cardIndex, out var boardPosition, out var result));
        Assert.Equal(4, boardPosition);
        Assert.Equal(TestCards.Ace, state.DeckBlue.GetCard(cardIndex));
        Assert.Equal(TriadGameStatus.BlueWins, result.Expected);
    }

    [Fact]
    public void SolverResultRanksWinsOverDraws()
    {
        var likelyWin = new TriadSolverResult(6f, 1f, 10);
        var likelyDraw = new TriadSolverResult(1f, 8f, 10);
        var likelyLoss = new TriadSolverResult(1f, 1f, 10);
        Assert.True(likelyWin.IsBetterThan(likelyDraw));
        Assert.True(likelyDraw.IsBetterThan(likelyLoss));
        Assert.Equal(TriadGameStatus.BlueLost, likelyLoss.Expected);
    }

    [Fact]
    public void RandomAgentPlaysAWholeGame()
    {
        var solver = new TriadSolver(new TriadRandomAgent(7));
        solver.InitializeSimulation([TriadRules.Create(TriadRuleId.Plus), TriadRules.Create(TriadRuleId.Same)]);
        ushort[] blue = [TestCards.AllFives, TestCards.Sevens, TestCards.Eights, TestCards.Mixed, TestCards.Twos];
        ushort[] red = [TestCards.Fours, TestCards.Threes, TestCards.AllSixes, TestCards.Mixed, TestCards.Weak];
        var state = solver.StartSimulation(new TriadDeck(blue), new TriadDeck(red), TriadGameStatus.InProgressBlue);
        var agent = new TriadRandomAgent(3);
        solver.RunSimulation(state, agent, agent);
        Assert.True(state.IsFinished);
    }

    [Fact]
    public void NpcDeckDrawsOnlyWhatItsHandLeavesRoomFor()
    {
        var deck = new TriadDeck([TestCards.Weak, TestCards.Twos, TestCards.Threes], [TestCards.Fours, TestCards.AllFives, TestCards.AllSixes]);
        var instance = new TriadDeckInstanceManual(deck);
        instance.OnCardPlaced(3);
        instance.OnCardPlaced(4);
        Assert.Equal(0b111, instance.AvailableCardMask);
    }

    [Fact]
    public void DeckValidationCapsFiveAndFourStars()
    {
        var twoFiveStars = new TriadDeck([TestCards.Ace, TestCards.Ace, TestCards.Weak, TestCards.Twos, TestCards.Threes]);
        Assert.Equal(TriadDeckState.HasDuplicates, twoFiveStars.Validate(static _ => true));
        var threeHigh = new TriadDeck([TestCards.Ace, TestCards.Nines, TestCards.Weak, TestCards.Twos, TestCards.Threes]);
        Assert.Equal(TriadDeckState.Valid, threeHigh.Validate(static _ => true));
        Assert.Equal(TriadDeckState.MissingCards, threeHigh.Validate(static cardId => cardId != TestCards.Ace));
    }

    [Fact]
    public void OptimizerPrefersStrongerCards()
    {
        var npcDeck = new TriadDeck([TestCards.AllFives, TestCards.AllFives, TestCards.AllSixes, TestCards.Fours, TestCards.Fours]);
        ushort[] owned = [TestCards.Weak, TestCards.Twos, TestCards.Threes, TestCards.Fours, TestCards.AllFives, TestCards.Sevens, TestCards.Eights, TestCards.Strong, TestCards.Nines, TestCards.Ace];
        var optimizer = new TriadDeckOptimizer(npcDeck, [], owned);
        Assert.True(optimizer.HasPool);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var result = optimizer.Run(2, timeout.Token);
        Assert.Contains(TestCards.Ace, result.Cards);
        Assert.DoesNotContain(TestCards.Weak, result.Cards);
        Assert.True(result.WinChance > 0.5f);
    }
}
