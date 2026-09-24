using AutoTripleTriadGrind.Core.Triad.Addons;
using AutoTripleTriadGrind.Core.Triad.Data;
using AutoTripleTriadGrind.Core.Triad.Logic;
using ECommons.DalamudServices;
using System.Text;

namespace AutoTripleTriadGrind.Core.Debug;

// Prints what the plugin reads off the open Triple Triad windows, to check the readers against a live match.
internal static class TriadBoardDumper
{
    public static void DumpBoard()
    {
        TriadData.EnsureLoaded();
        var screen = new TriadScreenState();
        if (!TriadMatchOps.TryReadBoard(screen, out var frame))
        {
            Svc.Chat.PrintError($"{AttgConstants.LogPrefix} No Triple Triad board is open.");
            return;
        }

        var builder = new StringBuilder(512);
        builder.Append("Board: canAct=").Append(frame.CanAct).Append(" playerTurn=").Append(frame.PlayerTurn).Append(" turnState=").Append(frame.TurnState)
            .Append(" banner=").Append(frame.BannerVisible).Append(" readFailed=").Append(frame.ReadFailed).Append(" pvp=").Append(frame.IsPvP);
        Report(builder);
        Report(builder.Append("Rules: ").Append(RuleList(screen)));
        Report(builder.Append("Blue: ").Append(Hand(screen.BlueHand)).Append(" forced=").Append(CardLabel(screen.ForcedBlueCard)));
        Report(builder.Append("Red: ").Append(Hand(screen.RedHand)));
        for (var row = 0; row < TriadGameState.BoardSize; row++)
        {
            builder.Append("Row ").Append(row).Append(':');
            for (var column = 0; column < TriadGameState.BoardSize; column++)
            {
                var cell = row * TriadGameState.BoardSize + column;
                builder.Append(' ').Append(CardLabel(screen.Board[cell])).Append('/').Append(screen.BoardOwners[cell]);
            }

            Report(builder);
        }
    }

    public static void DumpRequest()
    {
        TriadData.EnsureLoaded();
        Span<TriadRuleId> regional = stackalloc TriadRuleId[2];
        if (!TriadMatchOps.TryReadRegionalRules(regional))
        {
            Svc.Chat.PrintError($"{AttgConstants.LogPrefix} No Triple Triad challenge window is open, or its layout was not recognised.");
            return;
        }

        Svc.Chat.Print($"{AttgConstants.LogPrefix} Regional rules: {regional[0]}, {regional[1]}");
    }

    private static void Report(StringBuilder builder)
    {
        var line = builder.ToString();
        builder.Clear();
        Svc.Chat.Print($"{AttgConstants.LogPrefix} {line}");
        Svc.Log.Info($"{AttgConstants.LogPrefix} {line}");
    }

    private static string RuleList(TriadScreenState screen)
    {
        if (screen.Rules.Count == 0)
        {
            return "none";
        }

        var text = string.Empty;
        for (var index = 0; index < screen.Rules.Count; index++)
        {
            var rule = screen.Rules[index];
            var name = rule is Triad.Logic.Rules.TriadRuleRoulette roulette && roulette.Resolved is { } resolved ? $"Roulette({resolved.Id})" : rule.Id.ToString();
            text = index == 0 ? name : string.Concat(text, ", ", name);
        }

        return text;
    }

    private static string Hand(ushort[] hand)
    {
        var text = string.Empty;
        for (var index = 0; index < hand.Length; index++)
        {
            text = index == 0 ? CardLabel(hand[index]) : string.Concat(text, ", ", CardLabel(hand[index]));
        }

        return text;
    }

    private static string CardLabel(ushort cardId) => cardId switch
    {
        TriadCards.None   => "-",
        TriadCards.Hidden => "?",
        _                 => TriadData.Set.CardName(cardId),
    };
}
