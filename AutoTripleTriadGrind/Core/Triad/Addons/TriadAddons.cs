using ECommons;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Runtime.InteropServices;

namespace AutoTripleTriadGrind.Core.Triad.Addons;

internal static unsafe class TriadAddons
{
    public const string Request = "TripleTriadRequest";
    public const string DeckSelect = "TripleTriadSelDeck";
    public const string Board = "TripleTriad";
    public const string Result = "TripleTriadResult";

    public static bool TryGet(string name, out AtkUnitBase* addon, bool requireVisible = true)
    {
        if (!GenericHelpers.TryGetAddonByName(name, out addon) || addon is null)
        {
            return false;
        }

        return !requireVisible || addon->IsVisible;
    }

    public static bool IsVisible(string name) => TryGet(name, out _);

    public static bool IsReady(string name) => TryGet(name, out var addon) && GenericHelpers.IsAddonReady(addon);

    public static bool TryGetBoard(out AddonTripleTriad* board, bool requireVisible = true)
    {
        board = null;
        if (!TryGet(Board, out var addon, requireVisible))
        {
            return false;
        }

        board = (AddonTripleTriad*)addon;
        return true;
    }

    public static bool AnyMatchWindowVisible() => IsVisible(Request) || IsVisible(DeckSelect) || IsVisible(Board) || IsVisible(Result);
}

// The client structs do not name the reward field; 0x1C8 holds the item id of the card the last match gave.
[StructLayout(LayoutKind.Explicit, Size = 0x1D0)]
internal unsafe struct TriadAgent
{
    [FieldOffset(0x00)] public AgentInterface AgentInterface;
    [FieldOffset(0x1C8)] public uint RewardItemId;

    public static uint ReadRewardItemId()
    {
        var module = AgentModule.Instance();
        if (module is null)
        {
            return 0;
        }

        var agent = (TriadAgent*)module->GetAgentByInternalId(AgentId.TripleTriad);
        return agent is null ? 0 : agent->RewardItemId;
    }
}
