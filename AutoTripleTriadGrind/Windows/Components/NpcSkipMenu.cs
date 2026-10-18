using AutoTripleTriadGrind.Core.Localization;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace AutoTripleTriadGrind.Windows.Components;

internal static class NpcSkipMenu
{
    private static uint menuTriadRowId;

    public static void OpenOnRightClick(string menuId, uint triadRowId, Vector2 rowMin, Vector2 rowMax, bool editable)
    {
        if (!editable || !Hit.HoveringRect(rowMin, rowMax) || !ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            return;
        }

        menuTriadRowId = triadRowId;
        ImGui.OpenPopup(menuId);
    }

    public static void Draw(string menuId, Configuration configuration)
    {
        using var menu = ContextMenu.Begin(menuId);
        if (!menu.Open)
        {
            return;
        }

        var skipped = configuration.SkippedNpcs.Contains(menuTriadRowId);
        if (!ImGui.MenuItem(Loc.T(skipped ? L.Triad.PlayNpcAgain : L.Triad.SkipNpcAlways)))
        {
            return;
        }

        if (skipped)
        {
            configuration.SkippedNpcs.Remove(menuTriadRowId);
        }
        else
        {
            configuration.SkippedNpcs.Add(menuTriadRowId);
        }

        TriadLauncher.MarkSelectionChanged();
        configuration.SaveDebounced();
    }
}
