using AutoTripleTriadGrind.Core.Localization;
using AutoTripleTriadGrind.Windows.Components;
using AutoTripleTriadGrind.Windows.Sections.Config;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace AutoTripleTriadGrind.Windows.Pages;

internal sealed class SettingsPage
{
    private enum Tab { General, Deck, Play, PartyInvites, GmAlert }

    private readonly record struct Entry(Tab Tab, LocString Label, FontAwesomeIcon Icon, LocString Subtitle);

    // Ordered like Tab, because the active tab indexes this array.
    private static readonly Entry[] entries =
    [
        new(Tab.General, L.Settings.CatGeneral, FontAwesomeIcon.Cog, L.Settings.CatGeneralSub),
        new(Tab.Deck, L.Deck.Category, FontAwesomeIcon.LayerGroup, L.Deck.CategorySub),
        new(Tab.Play, L.Play.Category, FontAwesomeIcon.Gamepad, L.Play.CategorySub),
        new(Tab.PartyInvites, L.Safety.CatPartyInvites, FontAwesomeIcon.UserSlash, L.Safety.CatPartyInvitesSub),
        new(Tab.GmAlert, L.Safety.CatGmAlert, FontAwesomeIcon.UserSecret, L.Safety.CatGmAlertSub),
    ];

    private Tab activeTab = Tab.General;
    private bool resetScroll;

    public void Draw(Plugin plugin)
    {
        var configuration = plugin.Configuration;
        var scale = ImGuiHelpers.GlobalScale;
        var navWidth = Layout.SettingsNavWidth * scale;

        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        {
            using (var nav = ImRaii.Child("##attg_settings_nav", new Vector2(navWidth, -1f), false, ImGuiWindowFlags.NoScrollbar))
            {
                if (nav)
                {
                    DrawNav();
                }
            }

            ImGui.SameLine(0f, 18f * scale);

            using (var content = ImRaii.Child("##attg_settings_content", new Vector2(-1f, -1f), false, ImGuiWindowFlags.None))
            {
                if (content)
                {
                    DrawContent(configuration);
                }
            }
        }
    }

    private void DrawNav()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var title = Loc.T(L.Settings.Title);
        using (Fonts.PushTitle())
        {
            TextDraw.At(title, new Vector2(origin.X + 6f * scale, origin.Y), Styling.TextStrong);
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, TextDraw.Measure(title).Y + 10f * scale));
        }

        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (SidebarTab.Draw(Loc.T(entry.Label), entry.Icon, Styling.AccentGlow, activeTab == entry.Tab))
            {
                Select(entry.Tab);
            }
        }
    }

    private void Select(Tab tab)
    {
        if (activeTab == tab)
        {
            return;
        }

        activeTab = tab;
        resetScroll = true;
    }

    private void DrawContent(Configuration configuration)
    {
        if (resetScroll)
        {
            ImGui.SetScrollY(0f);
            resetScroll = false;
        }

        var entry = entries[(int)activeTab];
        var scale = ImGuiHelpers.GlobalScale;

        using var reveal = Motion.PushSwitch("##attg_settings_tab", (int)activeTab);
        using var group = ImRaii.Group();
        ImGui.Dummy(new Vector2(0f, 2f * scale));
        PageHeader.Draw(Loc.T(entry.Label), Loc.T(entry.Subtitle));

        switch (activeTab)
        {
            case Tab.General:
                GeneralSettings.Draw(configuration);
                break;
            case Tab.Deck:
                DeckSettings.Draw(configuration);
                break;
            case Tab.Play:
                PlaySettings.Draw(configuration);
                break;
            case Tab.PartyInvites:
                PartyInviteSettings.Draw(configuration);
                break;
            case Tab.GmAlert:
                GmAlertSettings.Draw(configuration);
                break;
        }
    }
}
