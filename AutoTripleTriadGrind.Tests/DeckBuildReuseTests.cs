using AutoTripleTriadGrind.Core.Triad.Decks;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTripleTriadGrind.Tests;

public sealed class DeckBuildReuseTests
{
    private const uint NpcRow = 0x230010;
    private const uint OtherNpcRow = 0x230011;
    private const ushort Rules = 0b0110;
    private const ushort OtherRules = 0b0010;

    [Fact]
    public void FinishedBuildServesTheSameNpcAndRules()
        => Assert.True(DeckBuildReuse.Serves(Task.FromResult(0), NpcRow, Rules, NpcRow, Rules));

    [Fact]
    public void RunningBuildServesTheSameNpcAndRules()
        => Assert.True(DeckBuildReuse.Serves(new TaskCompletionSource().Task, NpcRow, Rules, NpcRow, Rules));

    [Fact]
    public void FailedBuildStartsOver()
        => Assert.False(DeckBuildReuse.Serves(Task.FromException(new InvalidOperationException()), NpcRow, Rules, NpcRow, Rules));

    [Fact]
    public void CancelledBuildStartsOver()
        => Assert.False(DeckBuildReuse.Serves(Task.FromCanceled(new CancellationToken(true)), NpcRow, Rules, NpcRow, Rules));

    [Fact]
    public void BuildForAnotherNpcStartsOver()
        => Assert.False(DeckBuildReuse.Serves(Task.FromResult(0), OtherNpcRow, Rules, NpcRow, Rules));

    [Fact]
    public void BuildForOtherRulesStartsOver()
        => Assert.False(DeckBuildReuse.Serves(Task.FromResult(0), NpcRow, OtherRules, NpcRow, Rules));

    [Fact]
    public void NoBuildServesNothing()
        => Assert.False(DeckBuildReuse.Serves(null, NpcRow, Rules, NpcRow, Rules));
}
