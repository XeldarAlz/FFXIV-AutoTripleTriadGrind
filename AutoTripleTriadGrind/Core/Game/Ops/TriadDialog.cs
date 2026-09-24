using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoTripleTriadGrind.Core.Game.Ops;

// Walks an NPC's dialogue to the Triple Triad challenge: talk lines, the menu entry, and the Triple Triad yes/no.
internal static unsafe class TriadDialog
{
    private const string TalkAddon = "Talk";
    private const string SelectStringAddon = "SelectString";
    private const string SelectIconStringAddon = "SelectIconString";
    private const string SelectYesnoAddon = "SelectYesno";
    private const uint MenuListNodeId = 3;
    private const int ClickThrottleMs = 400;
    private const string TalkThrottleKey = "ATTG.Triad.Talk";
    private const string MenuThrottleKey = "ATTG.Triad.Menu";
    private const string YesnoThrottleKey = "ATTG.Triad.Yesno";

    // Map icons the menu shows beside the Triple Triad entry.
    private static readonly uint[] triadEntryIcons = [60091u, 61721u, 61723u];
    private static readonly string[] triadEntryWords = ["Triple Triad", "Triple-Triad", "triad", "triade", "triplo", "トリプル", "幻卡"];

    public enum Step : byte
    {
        Nothing,
        Handled,
        UnknownMenu,
    }

    // Toward the challenge the menus pick Triple Triad; otherwise they are backed out of with their last entry.
    public static Step Advance(bool towardChallenge = true)
    {
        if (TryGetReady(SelectYesnoAddon, out var yesno))
        {
            if (!IsOwnedByTriad(yesno))
            {
                return Step.UnknownMenu;
            }

            if (EzThrottler.Throttle(YesnoThrottleKey, ClickThrottleMs))
            {
                var prompt = new AddonMaster.SelectYesno(yesno);
                if (towardChallenge)
                {
                    prompt.Yes();
                }
                else
                {
                    prompt.No();
                }
            }

            return Step.Handled;
        }

        if (TryGetReady(SelectIconStringAddon, out var iconMenu))
        {
            return towardChallenge ? SelectTriadEntry(iconMenu, isIconMenu: true) : SelectLastEntry(iconMenu, isIconMenu: true);
        }

        if (TryGetReady(SelectStringAddon, out var menu))
        {
            return towardChallenge ? SelectTriadEntry(menu, isIconMenu: false) : SelectLastEntry(menu, isIconMenu: false);
        }

        if (TryGetReady(TalkAddon, out var talk))
        {
            if (EzThrottler.Throttle(TalkThrottleKey, ClickThrottleMs))
            {
                new AddonMaster.Talk(talk).Click();
            }

            return Step.Handled;
        }

        return Step.Nothing;
    }

    public static bool AnyOpen()
        => TryGetReady(TalkAddon, out _) || TryGetReady(SelectStringAddon, out _) || TryGetReady(SelectIconStringAddon, out _) || TryGetReady(SelectYesnoAddon, out _);

    private static Step SelectTriadEntry(AtkUnitBase* menu, bool isIconMenu)
    {
        var index = FindTriadEntry(menu, isIconMenu);
        if (index < 0)
        {
            return Step.UnknownMenu;
        }

        if (EzThrottler.Throttle(MenuThrottleKey, ClickThrottleMs))
        {
            Svc.Log.Info($"{AttgConstants.LogPrefix} Choosing menu entry {index} ({EntryText(menu, isIconMenu, index)}).");
            Callback.Fire(menu, true, index);
        }

        return Step.Handled;
    }

    private static Step SelectLastEntry(AtkUnitBase* menu, bool isIconMenu)
    {
        var entries = EntryCount(menu, isIconMenu);
        if (entries == 0)
        {
            return Step.UnknownMenu;
        }

        if (EzThrottler.Throttle(MenuThrottleKey, ClickThrottleMs))
        {
            Callback.Fire(menu, true, entries - 1);
        }

        return Step.Handled;
    }

    private static int FindTriadEntry(AtkUnitBase* menu, bool isIconMenu)
    {
        var list = menu->GetComponentListById(MenuListNodeId);
        var count = list is null ? 0 : list->GetItemCount();
        for (var index = 0; index < count; index++)
        {
            if (Array.IndexOf(triadEntryIcons, list->ItemRendererList[index].IconId) >= 0)
            {
                return index;
            }
        }

        var entries = EntryCount(menu, isIconMenu);
        for (var index = 0; index < entries; index++)
        {
            if (IsTriadText(EntryText(menu, isIconMenu, index)))
            {
                return index;
            }
        }

        return entries == 1 ? 0 : -1;
    }

    private static int EntryCount(AtkUnitBase* menu, bool isIconMenu)
        => isIconMenu ? new AddonMaster.SelectIconString(menu).EntryCount : new AddonMaster.SelectString(menu).EntryCount;

    private static string EntryText(AtkUnitBase* menu, bool isIconMenu, int index)
        => isIconMenu ? new AddonMaster.SelectIconString(menu).Entries[index].Text : new AddonMaster.SelectString(menu).Entries[index].Text;

    private static bool IsTriadText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        for (var index = 0; index < triadEntryWords.Length; index++)
        {
            if (text.Contains(triadEntryWords[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOwnedByTriad(AtkUnitBase* addon)
    {
        var atkModule = RaptureAtkModule.Instance();
        var agents = AgentModule.Instance();
        if (atkModule is null || agents is null || !atkModule->AddonCallbackMapping.TryGetValue(addon->Id, out var entry, false))
        {
            return false;
        }

        return entry.AgentInterface == agents->GetAgentByInternalId(AgentId.TripleTriad);
    }

    private static bool TryGetReady(string name, out AtkUnitBase* addon)
        => GenericHelpers.TryGetAddonByName(name, out addon) && GenericHelpers.IsAddonReady(addon);
}
