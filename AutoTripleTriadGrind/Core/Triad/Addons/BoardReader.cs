using AutoTripleTriadGrind.Core.Triad.Data;
using AutoTripleTriadGrind.Core.Triad.Logic;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoTripleTriadGrind.Core.Triad.Addons;

internal readonly record struct BoardFrame(bool CanAct, bool PlayerTurn, byte TurnState, bool BannerVisible, int CardsInHands, bool IsPvP, bool ReadFailed);

internal static unsafe class BoardReader
{
    // Index 23 of the board's atk values is 1 while the player is the one to move.
    private const int PlayerTurnValueIndex = 23;
    private const int RootChildren = 12;
    private const int RulesNodeIndex = 4;
    private const int RulesChildren = 5;
    private const int PvpNodeIndex = 11;
    private const int MaxRules = 4;
    // The card art is the image three components down; a dimmed art means Chaos locked that card.
    private const byte LockedTint = 100;

    private static readonly string[] turnBannerMarkers =
    [
        " TURN", " TOUR ", " TOUR DE ", " ZUG", " AM ZUG", " TURNO", " VEZ DE ", "のターン", "回合", " ход",
    ];

    public static BoardFrame Read(AddonTripleTriad* board, TriadScreenState screen)
    {
        var unit = &board->AtkUnitBase;
        var turnState = (byte)board->TurnState;
        var playerTurn = unit->AtkValuesCount > PlayerTurnValueIndex
            && unit->AtkValues[PlayerTurnValueIndex].Type == FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType.Int
            && unit->AtkValues[PlayerTurnValueIndex].Int == 1;
        var banner = NodeWalk.AnyVisibleText(unit->RootNode, IsTurnBanner);

        var failed = false;
        var inHands = 0;
        var forced = TriadCards.None;
        var forcedPhase = turnState == (byte)TurnState.MaskedMove;
        for (var slot = 0; slot < TriadDeck.HandSize; slot++)
        {
            screen.BlueHand[slot] = ReadCard(board->BlueDeck[slot], out var blueLocked, ref failed);
            screen.RedHand[slot] = ReadCard(board->RedDeck[slot], out _, ref failed);
            inHands += board->BlueDeck[slot].HasCard ? 1 : 0;
            inHands += board->RedDeck[slot].HasCard ? 1 : 0;
            if (forcedPhase && board->BlueDeck[slot].HasCard && !blueLocked)
            {
                forced = screen.BlueHand[slot];
            }
        }

        for (var cell = 0; cell < TriadGameState.BoardCells; cell++)
        {
            var card = board->Board[cell];
            screen.Board[cell] = ReadCard(card, out _, ref failed);
            screen.BoardOwners[cell] = (byte)card.CardOwner switch
            {
                1 => TriadOwner.Blue,
                2 => TriadOwner.Red,
                _ => TriadOwner.Unknown,
            };
        }

        screen.ForcedBlueCard = forced;
        failed |= !ReadRules(unit->RootNode, screen.Rules);
        var pvp = NodeWalk.IsVisible(NodeWalk.Child(unit->RootNode, PvpNodeIndex, RootChildren));
        var canAct = forcedPhase || (turnState == (byte)TurnState.NormalMove && playerTurn && !banner);
        screen.PlayerTurn = canAct;
        return new BoardFrame(canAct, playerTurn, turnState, banner, inHands, pvp, failed);
    }

    public static bool HandsPopulated(AddonTripleTriad* board)
    {
        var blue = 0;
        var red = 0;
        for (var slot = 0; slot < TriadDeck.HandSize; slot++)
        {
            blue += board->BlueDeck[slot].HasCard ? 1 : 0;
            red += board->RedDeck[slot].HasCard ? 1 : 0;
        }

        return blue > 0 && red > 0;
    }

    private static ushort ReadCard(AddonTripleTriad.TripleTriadCard card, out bool locked, ref bool failed)
    {
        locked = false;
        if (!card.HasCard)
        {
            return TriadCards.None;
        }

        if (card.NumSideU == 0)
        {
            return TriadCards.Hidden;
        }

        var art = NodeWalk.ComponentChild(NodeWalk.ComponentChild(card.CardDropControl, 1, 3), 0, 2);
        locked = art is not null && art->MultiplyRed < LockedTint;
        var texture = NodeWalk.TexturePath(NodeWalk.ComponentChild(art, 3, 21));
        var cardId = TriadCardMatcher.Match(card.NumSideU, card.NumSideL, card.NumSideD, card.NumSideR, (byte)card.CardType, card.CardRarity, texture);
        if (cardId == TriadCards.None)
        {
            failed = true;
        }

        return cardId;
    }

    private static bool ReadRules(AtkResNode* root, List<Logic.Rules.TriadRule> rules)
    {
        rules.Clear();
        var rulesNode = NodeWalk.Child(root, RulesNodeIndex, RootChildren);
        var count = NodeWalk.ChildCount(rulesNode);
        if (count == 0)
        {
            return true;
        }

        // With the usual five children the first is the list frame, so only the last four carry rule names.
        var allParsed = true;
        var textCount = count == RulesChildren ? MaxRules : count;
        for (var index = 0; index < textCount; index++)
        {
            var node = NodeWalk.Child(rulesNode, count == RulesChildren ? RulesChildren - 1 - index : index, count);
            var text = NodeWalk.Text(node);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (TriadRuleParser.Parse(text) is { } rule)
            {
                rules.Add(rule);
                continue;
            }

            allParsed = false;
        }

        return allParsed;
    }

    private static bool IsTurnBanner(string text)
    {
        if (text.Length > 96)
        {
            return false;
        }

        for (var index = 0; index < turnBannerMarkers.Length; index++)
        {
            if (text.Contains(turnBannerMarkers[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
