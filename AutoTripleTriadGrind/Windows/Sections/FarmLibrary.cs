using AutoTripleTriadGrind.Core.Localization;
using AutoTripleTriadGrind.Core.Planning;
using AutoTripleTriadGrind.Core.Tasks;
using AutoTripleTriadGrind.Core.Triad.Data;
using AutoTripleTriadGrind.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoTripleTriadGrind.Windows.Sections;

internal static class FarmLibrary
{
    private const float RowHeight = 52f;
    private const float RowGap = 6f;
    private const float SelectorRadius = 9f;
    private const float StopRowHeight = 40f;
    private const float SearchWidth = 240f;
    private const float CardSize = 32f;
    private const float CardGap = 4f;
    private const int ExpansionCount = 6;

    private static readonly FarmStopKind[] stops = [FarmStopKind.Matches, FarmStopKind.Never];
    private static readonly Segmented.Item[] stopItems = new Segmented.Item[stops.Length];
    private static readonly List<ushort> visibleNpcs = new(160);

    private static string search = string.Empty;
    private static bool searchFocused;

    public static void Draw(Configuration configuration, AutoTriadController controller, bool scrollIntoView)
    {
        LibraryHeader.Draw(Loc.T(L.Farm.Title), scrollIntoView);
        Styling.VSpace(8f);
        if (!TriadData.Loaded)
        {
            TextDraw.Hint(TriadData.FailureReason.Length > 0 ? TriadData.FailureReason : Loc.T(L.Collection.Loading));
            return;
        }

        TriadOwnership.Refresh();
        var editable = !controller.Running;
        DrawStopRow(configuration, editable);
        Styling.VSpace(10f);

        var scale = ImGuiHelpers.GlobalScale;
        SearchField.Draw("##attg_farm_search", Loc.T(L.Collection.SearchHint), ref search, ref searchFocused, MathF.Min(SearchWidth * scale, ImGui.GetContentRegionAvail().X));
        Styling.VSpace(6f);

        var drawnAny = false;
        for (var expansion = 0; expansion < ExpansionCount; expansion++)
        {
            Collect((byte)expansion);
            if (visibleNpcs.Count == 0)
            {
                continue;
            }

            GroupLabel.Draw(TriadLabels.Expansion((byte)expansion), spaceBefore: drawnAny ? 10f : 4f, extraBelow: 6f);
            drawnAny = true;
            for (var index = 0; index < visibleNpcs.Count; index++)
            {
                DrawRow(configuration, visibleNpcs[index], editable);
                ImGui.Dummy(new Vector2(0f, RowGap * scale - ImGui.GetStyle().ItemSpacing.Y));
            }
        }

        if (!drawnAny)
        {
            TextDraw.Hint(Loc.T(L.Common.NoMatches, search));
        }
    }

    private static void DrawStopRow(Configuration configuration, bool editable)
    {
        var scale = ImGuiHelpers.GlobalScale;
        stopItems[0] = new Segmented.Item(null, Loc.T(L.Farm.StopAfterMatches));
        stopItems[1] = new Segmented.Item(null, Loc.T(L.Farm.StopNever));
        var origin = ImGui.GetCursorScreenPos();
        var height = StopRowHeight * scale;
        var labelText = Loc.T(L.Farm.StopLabel);
        var labelSize = TextDraw.Measure(labelText);
        TextDraw.At(labelText, new Vector2(origin.X + 2f * scale, origin.Y + (height - labelSize.Y) * 0.5f), Styling.TextSecondary);

        var segmentX = origin.X + labelSize.X + 14f * scale;
        var segmentWidth = Segmented.PreferredWidth(stopItems);
        ImGui.SetCursorScreenPos(new Vector2(segmentX, origin.Y));
        var stop = Math.Max(0, Array.IndexOf(stops, configuration.FarmStop));
        if (Segmented.Draw("##attg_farm_stop", stopItems, ref stop, editable, StopRowHeight, segmentWidth))
        {
            configuration.FarmStop = stops[stop];
            configuration.SaveDebounced();
        }

        if (configuration.FarmStop == FarmStopKind.Matches)
        {
            ImGui.SetCursorScreenPos(new Vector2(segmentX + segmentWidth + 12f * scale, origin.Y + (height - ImGui.GetFrameHeight()) * 0.5f));
            var limit = configuration.FarmMatchLimit;
            if (Stepper.Draw("##attg_farm_limit", ref limit, 5, 1, 9999, Loc.T(L.Farm.MatchesFormat)) && editable)
            {
                configuration.FarmMatchLimit = limit;
                configuration.SaveDebounced();
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, height));
    }

    private static void Collect(byte expansion)
    {
        visibleNpcs.Clear();
        var set = TriadData.Set;
        for (var npcIndex = 0; npcIndex < set.NpcCount; npcIndex++)
        {
            var npc = set.Npcs[npcIndex];
            var npcExpansion = Math.Min(npc.Expansion, (byte)(ExpansionCount - 1));
            if (npcExpansion != expansion)
            {
                continue;
            }

            if (search.Length > 0 && !TriadLabels.NpcSearchKey(npcIndex).Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            visibleNpcs.Add((ushort)npcIndex);
        }
    }

    private static void DrawRow(Configuration configuration, ushort npcIndex, bool editable)
    {
        var set = TriadData.Set;
        var npc = set.Npcs[npcIndex];
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(ImGui.GetContentRegionAvail().X, RowHeight * scale);
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var eligibility = NpcEligibility.Check(npcIndex, configuration);
        var selected = configuration.FarmNpcs.Contains(npc.TriadRowId);
        var interactive = editable && eligibility == SkipReason.None;

        ImGui.PushID(npcIndex);
        var hit = Hit.Area("##farm", size, interactive || (editable && selected));
        var hover = Motion.Hover(Motion.Key("##farm"), hit.Hovered);
        var active = Motion.Approach(Motion.Key("##farm", 1), selected ? 1f : 0f, 14f);
        ImGui.PopID();
        if (hit.Clicked)
        {
            if (selected)
            {
                configuration.FarmNpcs.Remove(npc.TriadRowId);
            }
            else
            {
                configuration.FarmNpcs.Add(npc.TriadRowId);
            }

            TriadLauncher.MarkSelectionChanged();
            configuration.SaveDebounced();
        }

        var drawList = ImGui.GetWindowDrawList();
        Paint.Glass(drawList, origin, end, Styling.CardRounding * scale, Styling.AccentGlow, 0.02f + 0.14f * active, hover);
        var midY = origin.Y + size.Y * 0.5f;
        var discCenter = new Vector2(origin.X + 14f * scale + SelectorRadius * scale, midY);
        var ring = Vector4.Lerp(Styling.WithAlpha(Styling.BorderDim, 0.9f), Styling.AccentGlowSoft, active);
        drawList.AddCircle(discCenter, SelectorRadius * scale, Paint.Col(ring), 0, 1.4f * scale);
        if (active > 0.01f)
        {
            drawList.AddCircleFilled(discCenter, SelectorRadius * scale * active, Paint.Col(Styling.AccentGlow));
        }

        var rewards = set.RewardsOf(npcIndex);
        var cardsWidth = rewards.Length * (CardSize + CardGap) * scale;
        var cardsLeft = end.X - 12f * scale - cardsWidth;
        for (var rewardIndex = 0; rewardIndex < rewards.Length; rewardIndex++)
        {
            var cardId = rewards[rewardIndex];
            CardIcon.Draw(drawList, cardId, new Vector2(cardsLeft + rewardIndex * (CardSize + CardGap) * scale, midY - CardSize * scale * 0.5f), CardSize * scale,
                TriadOwnership.IsOwned(cardId) ? CardIcon.State.Owned : CardIcon.State.Missing);
        }

        var textX = discCenter.X + SelectorRadius * scale + 12f * scale;
        var textRight = cardsLeft - 10f * scale;
        if (eligibility != SkipReason.None)
        {
            textRight -= Badge.Draw(drawList, TriadLabels.Skip(eligibility), Styling.AccentAmber, textRight, midY) + 8f * scale;
        }

        var lineHeight = ImGui.GetTextLineHeight();
        var name = TextDraw.Truncate(set.NpcNames[npcIndex], textRight - textX);
        TextDraw.At(name, new Vector2(textX, midY - lineHeight + 1f * scale), Vector4.Lerp(Styling.TextSecondary, Styling.TextStrong, MathF.Max(active, hover)));
        using (Fonts.PushCaption())
        {
            TextDraw.At(TextDraw.Truncate(TriadLabels.NpcLocation(npcIndex), textRight - textX), new Vector2(textX, midY + 2f * scale), Styling.TextMuted);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
    }
}
