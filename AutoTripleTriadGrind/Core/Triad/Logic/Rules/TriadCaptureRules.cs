using AutoTripleTriadGrind.Core.Triad.Data;

namespace AutoTripleTriadGrind.Core.Triad.Logic.Rules;

public sealed class TriadRuleSame() : TriadRule(TriadRuleId.Same, TriadRuleFeatures.CaptureNeighbors | TriadRuleFeatures.CardPlaced, allowsCombo: true)
{
    // Two or more matching sides make every matched enemy neighbour flip, and the flips then combo on.
    public override void OnCheckCaptureNeighbors(TriadGameState state, int boardPosition, ReadOnlySpan<int> neighbors, List<int> captures)
    {
        var board = state.Board;
        ref var placed = ref board[boardPosition];
        var sameCount = 0;
        var captureMask = 0;
        for (var side = 0; side < TriadSide.Count; side++)
        {
            var neighbor = neighbors[side];
            if (neighbor < 0 || board[neighbor].IsEmpty)
            {
                continue;
            }

            if (placed.Number(side) != board[neighbor].OppositeNumber(side))
            {
                continue;
            }

            sameCount++;
            if (board[neighbor].Owner != placed.Owner)
            {
                captureMask |= 1 << side;
            }
        }

        if (sameCount < 2)
        {
            return;
        }

        for (var side = 0; side < TriadSide.Count; side++)
        {
            if ((captureMask & (1 << side)) == 0)
            {
                continue;
            }

            board[neighbors[side]].Owner = placed.Owner;
            captures.Add(neighbors[side]);
        }
    }
}

public sealed class TriadRulePlus() : TriadRule(TriadRuleId.Plus, TriadRuleFeatures.CaptureNeighbors | TriadRuleFeatures.CardPlaced, allowsCombo: true)
{
    // Any two sides whose sums with their neighbours match flip both neighbours.
    public override void OnCheckCaptureNeighbors(TriadGameState state, int boardPosition, ReadOnlySpan<int> neighbors, List<int> captures)
    {
        var board = state.Board;
        var placedOwner = board[boardPosition].Owner;
        for (var side = 0; side < TriadSide.Count; side++)
        {
            var neighbor = neighbors[side];
            if (neighbor < 0 || board[neighbor].IsEmpty || board[neighbor].Owner == placedOwner)
            {
                continue;
            }

            var patternSum = board[boardPosition].Number(side) + board[neighbor].OppositeNumber(side);
            var captured = false;
            for (var otherSide = 0; otherSide < TriadSide.Count; otherSide++)
            {
                var other = neighbors[otherSide];
                if (other < 0 || otherSide == side || board[other].IsEmpty)
                {
                    continue;
                }

                if (board[boardPosition].Number(otherSide) + board[other].OppositeNumber(otherSide) != patternSum)
                {
                    continue;
                }

                captured = true;
                if (board[other].Owner != placedOwner)
                {
                    board[other].Owner = placedOwner;
                    captures.Add(other);
                }
            }

            if (!captured)
            {
                continue;
            }

            board[neighbor].Owner = placedOwner;
            captures.Add(neighbor);
        }
    }
}

public sealed class TriadRuleReverse() : TriadRule(TriadRuleId.Reverse, TriadRuleFeatures.CaptureMath)
{
    private const float MaxSideSum = 40f;

    public override void OnCheckCaptureMath(TriadGameState state, int boardPosition, int neighborPosition, int cardNumber, int neighborNumber, ref bool captured)
        => captured = cardNumber < neighborNumber;

    public override void OnScoreCard(ushort cardId, ref float score) => score = 1f - TriadCards.Get(cardId).SideSum / MaxSideSum;
}

public sealed class TriadRuleFallenAce() : TriadRule(TriadRuleId.FallenAce, TriadRuleFeatures.CaptureWeights)
{
    // A 1 beats an A; under Reverse the A loses to a 1 instead.
    public override void OnCheckCaptureWeights(TriadGameState state, int boardPosition, int neighborPosition, bool reverseActive, ref int cardNumber, ref int neighborNumber)
    {
        if (reverseActive)
        {
            if (cardNumber == TriadCards.MaxNumber && neighborNumber == TriadCards.MinNumber)
            {
                cardNumber = 0;
            }

            return;
        }

        if (cardNumber == TriadCards.MinNumber && neighborNumber == TriadCards.MaxNumber)
        {
            neighborNumber = 0;
        }
    }
}
