using AutoTripleTriadGrind.Core.Triad.Data;
using AutoTripleTriadGrind.Core.Triad.Logic;
using AutoTripleTriadGrind.Core.Triad.Logic.Rules;

namespace AutoTripleTriadGrind.Tests;

public sealed class SimulationTests
{
    public SimulationTests() => TestCards.Install();

    private static TriadSimulation Simulation(params TriadRuleId[] rules)
    {
        var simulation = new TriadSimulation();
        var list = new List<TriadRule>(rules.Length);
        for (var index = 0; index < rules.Length; index++)
        {
            list.Add(TriadRules.Create(rules[index]));
        }

        simulation.Initialize(list);
        return simulation;
    }

    private static void Place(TriadSimulation simulation, TriadGameState state, TriadOwner owner, ushort cardId, int cell)
        => Assert.True(simulation.PlaceCard(state, cardId, owner, cell));

    [Fact]
    public void HigherSideCapturesNeighbour()
    {
        var simulation = Simulation();
        var state = TestCards.Game(simulation, [TestCards.Strong, TestCards.Weak], [TestCards.Weak, TestCards.Twos]);
        Place(simulation, state, TriadOwner.Blue, TestCards.Weak, 0);
        Place(simulation, state, TriadOwner.Red, TestCards.Twos, 1);
        Assert.Equal(TriadOwner.Red, state.Board[0].Owner);

        Place(simulation, state, TriadOwner.Blue, TestCards.Strong, 2);
        Assert.Equal(TriadOwner.Blue, state.Board[1].Owner);
        Assert.Equal(TriadOwner.Red, state.Board[0].Owner);
    }

    [Fact]
    public void EqualSidesDoNotCaptureWithoutSame()
    {
        var simulation = Simulation();
        var state = TestCards.Game(simulation, [TestCards.AllFives], [TestCards.AllFives]);
        Place(simulation, state, TriadOwner.Blue, TestCards.AllFives, 4);
        Place(simulation, state, TriadOwner.Red, TestCards.AllFives, 5);
        Assert.Equal(TriadOwner.Blue, state.Board[4].Owner);
        Assert.Equal(TriadOwner.Red, state.Board[5].Owner);
    }

    [Fact]
    public void SameFlipsBothMatchedNeighboursAndCombos()
    {
        var simulation = Simulation(TriadRuleId.Same);
        var state = TestCards.Game(simulation, [TestCards.AllFives, TestCards.AllFives, TestCards.Twos], [TestCards.AllFives, TestCards.Weak]);
        Place(simulation, state, TriadOwner.Blue, TestCards.Twos, 2);
        Place(simulation, state, TriadOwner.Red, TestCards.AllFives, 1);
        Assert.Equal(TriadOwner.Red, state.Board[2].Owner);

        Place(simulation, state, TriadOwner.Blue, TestCards.AllFives, 7);
        Place(simulation, state, TriadOwner.Red, TestCards.Weak, 3);
        Place(simulation, state, TriadOwner.Blue, TestCards.AllFives, 4);

        Assert.Equal(TriadOwner.Blue, state.Board[1].Owner);
        Assert.Equal(TriadOwner.Blue, state.Board[3].Owner);
        Assert.Equal(TriadOwner.Blue, state.Board[2].Owner);
    }

    [Fact]
    public void PlusFlipsNeighboursWithMatchingSums()
    {
        var simulation = Simulation(TriadRuleId.Plus);
        var state = TestCards.Game(simulation, [TestCards.Twos, TestCards.Weak, TestCards.Weak], [TestCards.Sevens, TestCards.Sevens]);
        Place(simulation, state, TriadOwner.Blue, TestCards.Weak, 8);
        Place(simulation, state, TriadOwner.Red, TestCards.Sevens, 1);
        Place(simulation, state, TriadOwner.Blue, TestCards.Weak, 2);
        Place(simulation, state, TriadOwner.Red, TestCards.Sevens, 3);
        Place(simulation, state, TriadOwner.Blue, TestCards.Twos, 4);

        Assert.Equal(TriadOwner.Blue, state.Board[1].Owner);
        Assert.Equal(TriadOwner.Blue, state.Board[3].Owner);
    }

    [Fact]
    public void ReverseCapturesWithLowerSide()
    {
        var simulation = Simulation(TriadRuleId.Reverse);
        var state = TestCards.Game(simulation, [TestCards.Weak], [TestCards.Strong]);
        Place(simulation, state, TriadOwner.Blue, TestCards.Weak, 0);
        Place(simulation, state, TriadOwner.Red, TestCards.Strong, 1);
        Assert.Equal(TriadOwner.Blue, state.Board[0].Owner);

        var capture = TestCards.Game(simulation, [TestCards.Weak], [TestCards.Strong], TriadGameStatus.InProgressRed);
        Place(simulation, capture, TriadOwner.Red, TestCards.Strong, 0);
        Place(simulation, capture, TriadOwner.Blue, TestCards.Weak, 1);
        Assert.Equal(TriadOwner.Blue, capture.Board[0].Owner);
    }

    [Fact]
    public void FallenAceLetsAOneBeatAnA()
    {
        var simulation = Simulation(TriadRuleId.FallenAce);
        var state = TestCards.Game(simulation, [TestCards.One], [TestCards.Ace], TriadGameStatus.InProgressRed);
        Place(simulation, state, TriadOwner.Red, TestCards.Ace, 0);
        Place(simulation, state, TriadOwner.Blue, TestCards.One, 1);
        Assert.Equal(TriadOwner.Blue, state.Board[0].Owner);
    }

    [Fact]
    public void AscensionRaisesCardsOfTheSameType()
    {
        var simulation = Simulation(TriadRuleId.Ascension);
        var state = TestCards.Game(simulation, [TestCards.PrimalLow, TestCards.Weak], [TestCards.AllFives, TestCards.Weak]);
        Place(simulation, state, TriadOwner.Blue, TestCards.PrimalLow, 0);
        Assert.Equal(1, state.Board[0].ScoreModifier);
        Assert.Equal(5, state.Board[0].Number(TriadSide.Right));
    }

    [Fact]
    public void DescensionLowersCardsOfTheSameType()
    {
        var simulation = Simulation(TriadRuleId.Descension);
        var state = TestCards.Game(simulation, [TestCards.PrimalHigh, TestCards.Weak], [TestCards.AllFives, TestCards.Weak]);
        Place(simulation, state, TriadOwner.Blue, TestCards.PrimalHigh, 0);
        Assert.Equal(-1, state.Board[0].ScoreModifier);
        Assert.Equal(4, state.Board[0].Number(TriadSide.Right));
    }

    [Fact]
    public void OrderForcesTheFirstCardInHand()
    {
        var solver = new TriadSolver(new Core.Triad.Logic.Agents.TriadRandomAgent(1));
        solver.Simulation.Initialize([TriadRules.Create(TriadRuleId.Order)]);
        var state = solver.StartSimulation(new TriadDeck([TestCards.Weak, TestCards.Strong]), new TriadDeck([TestCards.Weak]), TriadGameStatus.InProgressBlue);
        solver.FindAvailableActions(state, out _, out var cardsMask);
        Assert.Equal(1, cardsMask);
    }

    [Fact]
    public void FullBoardScoresTheWinner()
    {
        var simulation = Simulation();
        ushort[] blue = [TestCards.Ace, TestCards.Ace, TestCards.Ace, TestCards.Ace, TestCards.Ace];
        ushort[] red = [TestCards.Weak, TestCards.Weak, TestCards.Weak, TestCards.Weak, TestCards.Weak];
        var state = TestCards.Game(simulation, blue, red);
        int[] blueCells = [4, 0, 2, 6, 8];
        int[] redCells = [1, 3, 5, 7];
        for (var turn = 0; turn < 9; turn++)
        {
            var owner = turn % 2 == 0 ? TriadOwner.Blue : TriadOwner.Red;
            var deck = owner == TriadOwner.Blue ? state.DeckBlue : state.DeckRed;
            var cell = owner == TriadOwner.Blue ? blueCells[turn / 2] : redCells[turn / 2];
            Assert.True(simulation.PlaceCard(state, deck.FirstAvailableCard(), deck, owner, cell));
        }

        Assert.Equal(TriadGameStatus.BlueWins, state.Status);
    }

    [Fact]
    public void SuddenDeathRestartsADraw()
    {
        var simulation = Simulation(TriadRuleId.SuddenDeath);
        ushort[] blue = [TestCards.AllFives, TestCards.AllFives, TestCards.AllFives, TestCards.AllFives, TestCards.AllFives];
        ushort[] red = [TestCards.AllFives, TestCards.AllFives, TestCards.AllFives, TestCards.AllFives, TestCards.AllFives];
        var state = TestCards.Game(simulation, blue, red);
        for (var turn = 0; turn < 9; turn++)
        {
            var deck = state.TurnDeck;
            Assert.True(simulation.PlaceCard(state, deck.FirstAvailableCard(), deck, state.TurnOwner, turn));
        }

        Assert.Equal(1, state.NumRestarts);
        Assert.Equal(0, state.NumCardsPlaced);
        Assert.False(state.IsFinished);
    }

    [Fact]
    public void CopiedStateDoesNotShareTheBoard()
    {
        var simulation = Simulation();
        var state = TestCards.Game(simulation, [TestCards.Strong], [TestCards.Weak]);
        var copy = new TriadGameState(state);
        Place(simulation, copy, TriadOwner.Blue, TestCards.Strong, 0);
        Assert.True(state.Board[0].IsEmpty);
        Assert.False(copy.Board[0].IsEmpty);
    }
}
