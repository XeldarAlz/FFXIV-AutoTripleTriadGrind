using AutoTripleTriadGrind.Core.Triad.Data;

namespace AutoTripleTriadGrind.Core.Triad.Logic.Rules;

// Ascension and Descension shift every card of a type by the number of that type already on the board.
public abstract class TriadTypeShiftRule(TriadRuleId id, int step, float scoreMultiplier, bool favorsTyped)
    : TriadRule(id, TriadRuleFeatures.CardPlaced | TriadRuleFeatures.PostCapture)
{
    private readonly int step = step;
    private readonly float scoreMultiplier = scoreMultiplier;
    private readonly bool favorsTyped = favorsTyped;

    public override void OnCardPlaced(TriadGameState state, int boardPosition)
    {
        ref var placed = ref state.Board[boardPosition];
        var type = placed.Type;
        if (type == TriadCardType.None)
        {
            return;
        }

        var modifier = state.TypeModifier(type);
        if (modifier != 0)
        {
            placed.ScoreModifier = (sbyte)modifier;
        }
    }

    public override void OnPostCaptures(TriadGameState state, int boardPosition)
    {
        var board = state.Board;
        var type = board[boardPosition].Type;
        if (type == TriadCardType.None)
        {
            return;
        }

        var modifier = board[boardPosition].ScoreModifier + step;
        state.TypeModifiers[(int)type] = modifier;
        for (var cell = 0; cell < board.Length; cell++)
        {
            if (!board[cell].IsEmpty && board[cell].Type == type)
            {
                board[cell].ScoreModifier = (sbyte)modifier;
            }
        }
    }

    public override void OnScreenUpdate(TriadGameState state)
    {
        var board = state.Board;
        state.ClearTypeModifiers();
        for (var cell = 0; cell < board.Length; cell++)
        {
            if (!board[cell].IsEmpty && board[cell].Type != TriadCardType.None)
            {
                state.TypeModifiers[(int)board[cell].Type] += step;
            }
        }

        for (var cell = 0; cell < board.Length; cell++)
        {
            if (!board[cell].IsEmpty && board[cell].Type != TriadCardType.None)
            {
                board[cell].ScoreModifier = (sbyte)state.TypeModifiers[(int)board[cell].Type];
            }
        }
    }

    public override void OnScoreCard(ushort cardId, ref float score)
    {
        score *= scoreMultiplier;
        var typed = TriadCards.Get(cardId).Type != TriadCardType.None;
        if (typed == favorsTyped)
        {
            score += 1f - scoreMultiplier;
        }
    }
}

public sealed class TriadRuleAscension() : TriadTypeShiftRule(TriadRuleId.Ascension, 1, 0.8f, favorsTyped: true);

public sealed class TriadRuleDescension() : TriadTypeShiftRule(TriadRuleId.Descension, -1, 0.5f, favorsTyped: false);
