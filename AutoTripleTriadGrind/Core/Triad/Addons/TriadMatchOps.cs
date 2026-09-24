using AutoTripleTriadGrind.Core.Triad.Data;
using AutoTripleTriadGrind.Core.Triad.Logic;
using AutoTripleTriadGrind.Core.Triad.Match;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.UIInput;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoTripleTriadGrind.Core.Triad.Addons;

// The single-frame window actions a match is made of, kept out of the async run loop because they touch game memory.
internal static unsafe class TriadMatchOps
{
    // The challenge window's Challenge button.
    private const uint ChallengeButtonId = 41;
    // The board's callback for playing a card: value 14 with the cell in the low half and the hand slot in the high.
    private const int PlaceCardEvent = 14;
    private const int ResultRematch = 0;
    private const int WindowQuit = 1;

    private static readonly uint[] deckConfirmButtons = [5, 1];

    public static bool TryReadRegionalRules(Span<TriadRuleId> regional)
        => TriadAddons.TryGet(TriadAddons.Request, out var request) && RequestReader.TryReadRegionalRules(request, regional);

    public static void ClickChallenge()
    {
        if (!TriadAddons.TryGet(TriadAddons.Request, out var request) || !GenericHelpers.IsAddonReady(request))
        {
            return;
        }

        var button = request->GetComponentButtonById(ChallengeButtonId);
        if (button is null || button->AtkResNode is null || !button->AtkResNode->IsVisible())
        {
            return;
        }

        button->ClickAddonButton(request);
        request->Update(0);
    }

    public static bool DeckChosen()
        => TriadAddons.TryGetBoard(out var board, requireVisible: false) && BoardReader.HandsPopulated(board) && !TriadAddons.IsVisible(TriadAddons.DeckSelect);

    // The deck window takes the slot as its one callback value; the confirm buttons are the fallback.
    public static void ChooseDeck(int slot, int attempt)
    {
        if (!TriadAddons.TryGet(TriadAddons.DeckSelect, out var addon, requireVisible: false))
        {
            return;
        }

        if (attempt % 2 == 0)
        {
            var values = stackalloc AtkValue[1];
            values[0] = new AtkValue { Type = AtkValueType.Int, Int = slot };
            addon->FireCallback(1, values, true);
        }
        else
        {
            ClickFirstButton(addon);
        }

        addon->Update(0);
    }

    public static bool TryReadBoard(TriadScreenState screen, out BoardFrame frame)
    {
        if (!TriadAddons.TryGetBoard(out var board))
        {
            frame = default;
            return false;
        }

        frame = BoardReader.Read(board, screen);
        return true;
    }

    public static void PlaceCard(int handSlot, int cell)
    {
        if (!TriadAddons.TryGetBoard(out var board))
        {
            return;
        }

        Callback.Fire(&board->AtkUnitBase, true, PlaceCardEvent, (uint)cell + ((uint)handSlot << 16));
        board->AtkUnitBase.Update(0);
        // Cleared so the same turn is not acted on twice before the board catches up.
        board->TurnState = 0;
    }

    public static bool PlayAnyCard(TriadScreenState screen)
    {
        var slot = -1;
        for (var index = 0; index < screen.BlueHand.Length; index++)
        {
            if (screen.BlueHand[index] is not (TriadCards.None or TriadCards.Hidden))
            {
                slot = index;
                break;
            }
        }

        var cell = Array.IndexOf(screen.Board, TriadCards.None);
        if (slot < 0 || cell < 0)
        {
            return false;
        }

        PlaceCard(slot, cell);
        return true;
    }

    public static bool ResultReady() => TriadAddons.TryGet(TriadAddons.Result, out var result) && ResultReader.IsReady(result);

    public static MatchOutcome ReadOutcome() => TriadAddons.TryGet(TriadAddons.Result, out var result) ? ResultReader.ReadOutcome(result) : MatchOutcome.Unknown;

    public static void Rematch()
    {
        if (!TriadAddons.TryGet(TriadAddons.Result, out var result) || !ResultReader.IsReady(result))
        {
            return;
        }

        result->FireCallbackInt(ResultRematch);
        result->Update(0);
    }

    // Closes the frontmost match window; false once none is open.
    public static bool CloseOneWindow()
    {
        if (TriadAddons.TryGet(TriadAddons.Result, out var result))
        {
            result->FireCallbackInt(WindowQuit);
            result->Update(0);
            return true;
        }

        if (TriadAddons.TryGet(TriadAddons.Request, out var request))
        {
            request->FireCallbackInt(WindowQuit);
            request->Update(0);
            return true;
        }

        if (TriadAddons.TryGet(TriadAddons.DeckSelect, out var deckSelect))
        {
            deckSelect->Close(true);
            return true;
        }

        return false;
    }

    private static void ClickFirstButton(AtkUnitBase* addon)
    {
        for (var index = 0; index < deckConfirmButtons.Length; index++)
        {
            var button = addon->GetComponentButtonById(deckConfirmButtons[index]);
            if (button is null || !button->IsEnabled || button->AtkResNode is null || !button->AtkResNode->IsVisible())
            {
                continue;
            }

            button->ClickAddonButton(addon);
            return;
        }
    }
}
