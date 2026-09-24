using AutoTripleTriadGrind.Core.Localization;
using AutoTripleTriadGrind.Core.Planning;
using AutoTripleTriadGrind.Core.Tasks;
using AutoTripleTriadGrind.Core.Triad.Data;
using AutoTripleTriadGrind.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace AutoTripleTriadGrind.Windows.Sections;

internal static class CollectionLibrary
{
    private const float ToolbarGap = 8f;
    private const float SearchWidth = 240f;
    private const float SummaryRowHeight = 32f;
    private const float NpcRowHeight = 62f;
    private const float NpcRowGap = 6f;
    private const float CardSize = 40f;
    private const float CardGap = 5f;
    private const float CardTileWidth = 250f;
    private const float CardTileHeight = 56f;
    private const float SelectorRadius = 9f;
    private const float ListSlide = 8f;
    private const int ExpansionCount = 6;

    private static readonly LibraryView[] views = [LibraryView.ByNpc, LibraryView.ByCard];
    private static readonly LibraryFilter[] filters = [LibraryFilter.Missing, LibraryFilter.All, LibraryFilter.Owned];
    private static readonly Segmented.Item[] viewItems = new Segmented.Item[views.Length];
    private static readonly Segmented.Item[] filterItems = new Segmented.Item[filters.Length];
    private static readonly List<ushort> visibleNpcs = new(160);
    private static readonly List<ushort> visibleCards = new(480);
    private static readonly CachedText[] npcCounts = new CachedText[256];

    private static string search = string.Empty;
    private static bool searchFocused;
    private static CachedText summaryText;

    public static void Draw(Configuration configuration, AutoTriadController controller, bool scrollIntoView)
    {
        LibraryHeader.Draw(Loc.T(L.Collection.Title), scrollIntoView);
        Styling.VSpace(8f);
        if (!TriadData.Loaded)
        {
            TextDraw.Hint(TriadData.FailureReason.Length > 0 ? TriadData.FailureReason : Loc.T(L.Collection.Loading));
            return;
        }

        TriadOwnership.Refresh();
        DrawToolbar(configuration);
        Styling.VSpace(10f);
        var editable = !controller.Running;
        DrawSummaryRow(configuration, editable);
        Styling.VSpace(4f);

        using var reveal = Motion.PushSwitch("##attg_library_list", (int)configuration.LibraryView * 8 + (int)configuration.LibraryFilter, slide: ListSlide);
        if (configuration.LibraryView == LibraryView.ByCard)
        {
            DrawCards(configuration, editable);
            return;
        }

        DrawNpcs(configuration, editable);
    }

    private static void DrawToolbar(Configuration configuration)
    {
        var scale = ImGuiHelpers.GlobalScale;
        viewItems[0] = new Segmented.Item(FontAwesomeIcon.User, Loc.T(L.Collection.ViewByNpc));
        viewItems[1] = new Segmented.Item(FontAwesomeIcon.ThLarge, Loc.T(L.Collection.ViewByCard));
        filterItems[0] = new Segmented.Item(null, Loc.T(L.Collection.FilterMissing));
        filterItems[1] = new Segmented.Item(null, Loc.T(L.Collection.FilterAll));
        filterItems[2] = new Segmented.Item(null, Loc.T(L.Collection.FilterOwned));

        var origin = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail().X;
        var gap = ToolbarGap * scale;
        var viewWidth = Segmented.PreferredWidth(viewItems);
        var filterWidth = Segmented.PreferredWidth(filterItems);
        var searchWidth = MathF.Max(120f * scale, MathF.Min(SearchWidth * scale, avail - viewWidth - filterWidth - gap * 2f));
        var height = Layout.ChipHeight + 4f;

        var view = Array.IndexOf(views, configuration.LibraryView);
        if (Segmented.Draw("##attg_library_view", viewItems, ref view, height: height, width: viewWidth))
        {
            configuration.LibraryView = views[view];
            configuration.SaveDebounced();
        }

        ImGui.SameLine(0f, gap);
        var filter = Array.IndexOf(filters, configuration.LibraryFilter);
        if (Segmented.Draw("##attg_library_filter", filterItems, ref filter, height: height, width: filterWidth))
        {
            configuration.LibraryFilter = filters[filter];
            configuration.SaveDebounced();
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X + avail - searchWidth, origin.Y + (height * scale - 36f * scale) * 0.5f));
        SearchField.Draw("##attg_library_search", Loc.T(L.Collection.SearchHint), ref search, ref searchFocused, searchWidth);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(avail, MathF.Max(height * scale, 36f * scale)));
    }

    private static void DrawSummaryRow(Configuration configuration, bool editable)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var set = TriadData.Set;
        var origin = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail().X;
        var rowHeight = SummaryRowHeight * scale;
        CountRewards(configuration, out var total, out var owned, out var selectedMissing);

        using (Fonts.PushCaption())
        {
            var key = ((long)selectedMissing << 40) | ((long)owned << 20) | (uint)total;
            var summary = summaryText.Get(key, static packed => Loc.T(L.Collection.Summary, (int)(packed >> 40), (int)((packed >> 20) & 0xFFFFF), (int)(packed & 0xFFFFF)));
            var summarySize = TextDraw.Measure(summary);
            TextDraw.At(summary, new Vector2(origin.X + 2f * scale, origin.Y + (rowHeight - summarySize.Y) * 0.5f), Styling.TextDim);
        }

        var missing = total - owned;
        var allSelected = missing > 0 && selectedMissing >= missing;
        var clearLabel = Loc.T(L.Common.Clear);
        var selectLabel = Loc.T(L.Collection.SelectAllMissing);
        var clearWidth = PillButton.Width(clearLabel);
        var selectWidth = PillButton.Width(selectLabel, FontAwesomeIcon.CheckDouble);
        var right = origin.X + avail;
        ImGui.SetCursorScreenPos(new Vector2(right - clearWidth, origin.Y));
        if (PillButton.Draw("##attg_library_clear", clearLabel, Styling.AccentRose, PillButton.Emphasis.Ghost,
                enabled: editable && configuration.SelectedCards.Count > 0, height: SummaryRowHeight))
        {
            configuration.SelectedCards.Clear();
            OnSelectionChanged(configuration);
        }

        ImGui.SetCursorScreenPos(new Vector2(right - clearWidth - 6f * scale - selectWidth, origin.Y));
        if (PillButton.Draw("##attg_library_all", selectLabel, Styling.AccentGlow, PillButton.Emphasis.Tinted, FontAwesomeIcon.CheckDouble,
                enabled: editable && !allSelected && missing > 0, height: SummaryRowHeight))
        {
            var cardIds = set.CardIdsInOrder;
            for (var index = 0; index < cardIds.Length; index++)
            {
                var cardId = cardIds[index];
                if (set.IsNpcReward(cardId) && !TriadOwnership.IsOwned(cardId))
                {
                    configuration.SelectedCards.Add(cardId);
                }
            }

            OnSelectionChanged(configuration);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(avail, rowHeight));
    }

    private static void CountRewards(Configuration configuration, out int total, out int owned, out int selectedMissing)
    {
        var set = TriadData.Set;
        total = 0;
        owned = 0;
        selectedMissing = 0;
        var cardIds = set.CardIdsInOrder;
        for (var index = 0; index < cardIds.Length; index++)
        {
            var cardId = cardIds[index];
            if (!set.IsNpcReward(cardId))
            {
                continue;
            }

            total++;
            if (TriadOwnership.IsOwned(cardId))
            {
                owned++;
                continue;
            }

            if (configuration.SelectedCards.Contains(cardId))
            {
                selectedMissing++;
            }
        }
    }

    private static void DrawNpcs(Configuration configuration, bool editable)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawnAny = false;
        for (var expansion = 0; expansion < ExpansionCount; expansion++)
        {
            CollectNpcs(configuration, (byte)expansion);
            if (visibleNpcs.Count == 0)
            {
                continue;
            }

            GroupLabel.Draw(TriadLabels.Expansion((byte)expansion), spaceBefore: drawnAny ? 10f : 4f, extraBelow: 6f);
            drawnAny = true;
            for (var index = 0; index < visibleNpcs.Count; index++)
            {
                DrawNpcRow(configuration, visibleNpcs[index], editable);
                ImGui.Dummy(new Vector2(0f, NpcRowGap * scale - ImGui.GetStyle().ItemSpacing.Y));
            }
        }

        if (!drawnAny)
        {
            TextDraw.Hint(search.Length > 0 ? Loc.T(L.Common.NoMatches, search) : Loc.T(L.Collection.NothingHere));
        }
    }

    private static void CollectNpcs(Configuration configuration, byte expansion)
    {
        visibleNpcs.Clear();
        var set = TriadData.Set;
        for (var npcIndex = 0; npcIndex < set.NpcCount; npcIndex++)
        {
            var npc = set.Npcs[npcIndex];
            if (npc.Expansion != expansion && !(expansion == ExpansionCount - 1 && npc.Expansion >= ExpansionCount))
            {
                continue;
            }

            if (npc.RewardCount == 0)
            {
                continue;
            }

            var missing = TriadOwnership.MissingRewardCount(npcIndex);
            var passes = configuration.LibraryFilter switch
            {
                LibraryFilter.Missing => missing > 0,
                LibraryFilter.Owned   => missing == 0,
                _                     => true,
            };
            if (!passes || !MatchesSearch(npcIndex))
            {
                continue;
            }

            visibleNpcs.Add((ushort)npcIndex);
        }
    }

    private static bool MatchesSearch(int npcIndex)
    {
        if (search.Length == 0)
        {
            return true;
        }

        if (TriadLabels.NpcSearchKey(npcIndex).Contains(search, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var rewards = TriadData.Set.RewardsOf(npcIndex);
        for (var index = 0; index < rewards.Length; index++)
        {
            if (TriadLabels.CardSearchKey(rewards[index]).Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void DrawNpcRow(Configuration configuration, ushort npcIndex, bool editable)
    {
        var set = TriadData.Set;
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var size = new Vector2(width, NpcRowHeight * scale);
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var rewards = set.RewardsOf(npcIndex);
        var eligibility = NpcEligibility.Check(npcIndex, configuration);
        var available = eligibility == SkipReason.None;
        var missing = TriadOwnership.MissingRewardCount(npcIndex);
        var selectedMissing = CountSelectedMissing(configuration, rewards);
        var fullySelected = missing > 0 && selectedMissing == missing;
        var drawList = ImGui.GetWindowDrawList();

        ImGui.PushID(npcIndex);
        var cardsWidth = rewards.Length * (CardSize + CardGap) * scale - CardGap * scale;
        var cardsLeft = end.X - 14f * scale - cardsWidth;
        var rowHit = Hit.Area("##npc", new Vector2(cardsLeft - origin.X - 8f * scale, size.Y), editable && missing > 0);
        var hover = Motion.Hover(Motion.Key("##npc"), rowHit.Hovered);
        var active = Motion.Approach(Motion.Key("##npc", 1), fullySelected ? 1f : selectedMissing > 0 ? 0.5f : 0f, 14f);
        if (rowHit.Clicked)
        {
            SetCards(configuration, rewards, !fullySelected);
            OnSelectionChanged(configuration);
        }

        var accent = available ? Styling.AccentGlow : Styling.AccentAmber;
        Paint.Glass(drawList, origin, end, Styling.CardRounding * scale, accent, 0.02f + 0.12f * active, hover);

        var midY = origin.Y + size.Y * 0.5f;
        var discCenter = new Vector2(origin.X + 14f * scale + SelectorRadius * scale, midY);
        DrawSelector(drawList, discCenter, SelectorRadius * scale, missing == 0, active);

        var textX = discCenter.X + SelectorRadius * scale + 12f * scale;
        var textRight = cardsLeft - 12f * scale;
        var lineHeight = ImGui.GetTextLineHeight();
        float captionHeight;
        using (Fonts.PushCaption())
        {
            captionHeight = ImGui.GetTextLineHeight();
        }

        var top = midY - (lineHeight + 3f * scale + captionHeight) * 0.5f;
        var countLabel = npcCounts[npcIndex % npcCounts.Length].Get(((long)npcIndex << 32) | ((long)(rewards.Length - missing) << 16) | (uint)rewards.Length,
            static key => string.Concat(((int)((key >> 16) & 0xFFFF)).ToString(Loc.Culture), "/", ((int)(key & 0xFFFF)).ToString(Loc.Culture)));
        var badgeRight = textRight;
        if (!available)
        {
            badgeRight -= Badge.Draw(drawList, TriadLabels.Skip(eligibility), Styling.AccentAmber, badgeRight, top + lineHeight * 0.5f) + 8f * scale;
        }

        using (Fonts.PushCaption())
        {
            var countSize = TextDraw.Measure(countLabel);
            TextDraw.At(countLabel, new Vector2(badgeRight - countSize.X, top + (lineHeight - countSize.Y) * 0.5f), missing == 0 ? Styling.AccentMint : Styling.TextDim);
            badgeRight -= countSize.X + 10f * scale;
        }

        var nameColor = Vector4.Lerp(Styling.TextSecondary, Styling.TextStrong, MathF.Max(active, hover));
        TextDraw.At(TextDraw.Truncate(set.NpcNames[npcIndex], badgeRight - textX), new Vector2(textX, top), missing == 0 ? Styling.TextDim : nameColor);
        using (Fonts.PushCaption())
        {
            TextDraw.At(TextDraw.Truncate(TriadLabels.NpcLocation(npcIndex), textRight - textX), new Vector2(textX, top + lineHeight + 3f * scale), Styling.TextMuted);
        }

        if (rowHit.Hovered)
        {
            DrawNpcTooltip(npcIndex, eligibility);
        }

        var cardY = midY - CardSize * scale * 0.5f;
        for (var rewardIndex = 0; rewardIndex < rewards.Length; rewardIndex++)
        {
            var cardX = cardsLeft + rewardIndex * (CardSize + CardGap) * scale;
            ImGui.SetCursorScreenPos(new Vector2(cardX, cardY));
            DrawCard(configuration, rewards[rewardIndex], editable);
        }

        ImGui.PopID();
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
    }

    private static void DrawCard(Configuration configuration, ushort cardId, bool editable)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = CardSize * scale;
        var origin = ImGui.GetCursorScreenPos();
        var owned = TriadOwnership.IsOwned(cardId);
        var selected = configuration.SelectedCards.Contains(cardId);
        ImGui.PushID(cardId);
        var hit = Hit.Area("##card", new Vector2(size, size), editable && !owned);
        var hover = Motion.Hover(Motion.Key("##card"), Hit.HoveringRect(origin, origin + new Vector2(size, size)));
        ImGui.PopID();
        if (hit.Clicked)
        {
            ToggleCard(configuration, cardId, !selected);
        }

        var state = owned ? CardIcon.State.Owned : selected ? CardIcon.State.Selected : CardIcon.State.Missing;
        CardIcon.Draw(ImGui.GetWindowDrawList(), cardId, origin, size, state, hover);
        if (Hit.HoveringRect(origin, origin + new Vector2(size, size)))
        {
            DrawCardTooltip(cardId, owned, selected);
        }
    }

    private static void DrawCards(Configuration configuration, bool editable)
    {
        CollectCards(configuration);
        if (visibleCards.Count == 0)
        {
            TextDraw.Hint(search.Length > 0 ? Loc.T(L.Common.NoMatches, search) : Loc.T(L.Collection.NothingHere));
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var gap = NpcRowGap * scale;
        var avail = ImGui.GetContentRegionAvail().X;
        var columns = Math.Max(1, (int)MathF.Floor((avail + gap) / (CardTileWidth * scale + gap)));
        var tileWidth = (avail - gap * (columns - 1)) / columns;
        using var spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap));
        for (var index = 0; index < visibleCards.Count; index++)
        {
            if (index % columns != 0)
            {
                ImGui.SameLine(0f, gap);
            }

            DrawCardTile(configuration, visibleCards[index], tileWidth, editable);
        }
    }

    private static void CollectCards(Configuration configuration)
    {
        visibleCards.Clear();
        var set = TriadData.Set;
        var cardIds = set.CardIdsInOrder;
        for (var index = 0; index < cardIds.Length; index++)
        {
            var cardId = cardIds[index];
            if (!set.IsNpcReward(cardId))
            {
                continue;
            }

            var owned = TriadOwnership.IsOwned(cardId);
            var passes = configuration.LibraryFilter switch
            {
                LibraryFilter.Missing => !owned,
                LibraryFilter.Owned   => owned,
                _                     => true,
            };
            if (!passes || (search.Length > 0 && !CardMatchesSearch(cardId)))
            {
                continue;
            }

            visibleCards.Add(cardId);
        }
    }

    private static bool CardMatchesSearch(ushort cardId)
    {
        if (TriadLabels.CardSearchKey(cardId).Contains(search, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var droppers = TriadData.Set.NpcsDropping(cardId);
        for (var index = 0; index < droppers.Length; index++)
        {
            if (TriadLabels.NpcSearchKey(droppers[index]).Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void DrawCardTile(Configuration configuration, ushort cardId, float width, bool editable)
    {
        var set = TriadData.Set;
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(width, CardTileHeight * scale);
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var owned = TriadOwnership.IsOwned(cardId);
        var selected = configuration.SelectedCards.Contains(cardId);
        ImGui.PushID(cardId);
        var hit = Hit.Area("##tile", size, editable && !owned);
        var hover = Motion.Hover(Motion.Key("##tile"), hit.Hovered);
        var active = Motion.Approach(Motion.Key("##tile", 1), selected && !owned ? 1f : 0f, 14f);
        ImGui.PopID();
        if (hit.Clicked)
        {
            ToggleCard(configuration, cardId, !selected);
        }

        var drawList = ImGui.GetWindowDrawList();
        Paint.Glass(drawList, origin, end, Styling.CardRounding * scale, owned ? Styling.AccentMint : Styling.AccentGlow, 0.02f + 0.14f * active, hover);
        var iconSize = CardTileHeight * scale - 12f * scale;
        var iconMin = new Vector2(origin.X + 6f * scale, origin.Y + 6f * scale);
        CardIcon.Draw(drawList, cardId, iconMin, iconSize, owned ? CardIcon.State.Owned : selected ? CardIcon.State.Selected : CardIcon.State.Missing, hover);

        var textX = iconMin.X + iconSize + 10f * scale;
        var textWidth = end.X - 10f * scale - textX;
        var lineHeight = ImGui.GetTextLineHeight();
        var midY = origin.Y + size.Y * 0.5f;
        float captionHeight;
        using (Fonts.PushCaption())
        {
            captionHeight = ImGui.GetTextLineHeight();
        }

        var top = midY - (lineHeight + 2f * scale + captionHeight) * 0.5f;
        TextDraw.At(TextDraw.Truncate(set.CardNames[cardId], textWidth), new Vector2(textX, top), owned ? Styling.TextDim : Vector4.Lerp(Styling.TextSecondary, Styling.TextStrong, MathF.Max(active, hover)));
        using (Fonts.PushCaption())
        {
            TextDraw.At(TextDraw.Truncate(TriadLabels.CardStats(cardId), textWidth), new Vector2(textX, top + lineHeight + 2f * scale), Styling.TextMuted);
        }

        if (hit.Hovered || Hit.HoveringRect(origin, end))
        {
            DrawCardTooltip(cardId, owned, selected);
        }
    }

    private static void DrawSelector(ImDrawListPtr drawList, Vector2 center, float radius, bool complete, float active)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (complete)
        {
            drawList.AddCircleFilled(center, radius, Paint.Col(Styling.WithAlpha(Styling.AccentMint, 0.85f)));
            Paint.Check(drawList, center, radius * 1.1f, Styling.InkOnGlow, 1.8f * scale);
            return;
        }

        var ring = Vector4.Lerp(Styling.WithAlpha(Styling.BorderDim, 0.9f), Styling.AccentGlowSoft, active);
        drawList.AddCircle(center, radius, Paint.Col(ring), 0, 1.4f * scale);
        if (active <= 0.01f)
        {
            return;
        }

        drawList.AddCircleFilled(center, radius * active, Paint.Col(Styling.AccentGlow));
        if (active > 0.75f)
        {
            Paint.Check(drawList, center, radius * 1.1f, Styling.WithAlpha(Styling.InkOnGlow, (active - 0.75f) * 4f), 1.8f * scale);
        }
    }

    private static void DrawNpcTooltip(int npcIndex, SkipReason eligibility)
    {
        var set = TriadData.Set;
        var npc = set.Npcs[npcIndex];
        using (Tooltip.Begin())
        {
            Tooltip.Text(set.NpcNames[npcIndex], Styling.TextStrong);
            Tooltip.Text(TriadLabels.NpcLocation(npcIndex), Styling.TextDim);
            Tooltip.Text(Loc.T(L.Collection.TooltipFee, npc.Fee), Styling.TextSecondary);
            if (npc.RuleMask != 0)
            {
                Tooltip.Text(Loc.T(L.Collection.TooltipRules, RuleList(npc.RuleMask)), Styling.TextSecondary);
            }

            if (npc.UsesRegionalRules)
            {
                Tooltip.Text(Loc.T(L.Collection.TooltipRegional), Styling.TextDim);
            }

            if (eligibility == SkipReason.Locked && set.NpcUnlockQuestNames[npcIndex].Length > 0)
            {
                Tooltip.Text(Loc.T(L.Collection.TooltipLockedQuest, set.NpcUnlockQuestNames[npcIndex]), Styling.AccentAmber);
            }
            else if (eligibility != SkipReason.None)
            {
                Tooltip.Text(TriadLabels.Skip(eligibility), Styling.AccentAmber);
            }

            Tooltip.Text(Loc.T(L.Collection.TooltipSelectNpc), Styling.TextMuted);
        }
    }

    private static void DrawCardTooltip(ushort cardId, bool owned, bool selected)
    {
        var set = TriadData.Set;
        using (Tooltip.Begin())
        {
            Tooltip.Text(set.CardNames[cardId], Styling.TextStrong);
            Tooltip.Text(TriadLabels.CardStats(cardId), Styling.TextDim);
            Tooltip.Text(owned ? Loc.T(L.Collection.TooltipOwned) : selected ? Loc.T(L.Collection.TooltipSelected) : Loc.T(L.Collection.TooltipMissing),
                owned ? Styling.AccentMint : selected ? Styling.AccentGlowSoft : Styling.TextSecondary);
            var droppers = set.NpcsDropping(cardId);
            for (var index = 0; index < droppers.Length; index++)
            {
                Tooltip.Text(Loc.T(L.Collection.TooltipDroppedBy, set.NpcNames[droppers[index]], TriadLabels.NpcLocation(droppers[index])), Styling.TextSecondary);
            }
        }
    }

    private static string RuleList(ushort ruleMask)
    {
        var names = TriadData.Set.RuleNames;
        var text = string.Empty;
        for (var ruleIndex = 1; ruleIndex < TriadRuleIds.Count; ruleIndex++)
        {
            if (!TriadRuleIds.Has(ruleMask, (TriadRuleId)ruleIndex))
            {
                continue;
            }

            text = text.Length == 0 ? names[ruleIndex] : string.Concat(text, ", ", names[ruleIndex]);
        }

        return text;
    }

    private static int CountSelectedMissing(Configuration configuration, ReadOnlySpan<ushort> rewards)
    {
        var count = 0;
        for (var index = 0; index < rewards.Length; index++)
        {
            if (!TriadOwnership.IsOwned(rewards[index]) && configuration.SelectedCards.Contains(rewards[index]))
            {
                count++;
            }
        }

        return count;
    }

    private static void SetCards(Configuration configuration, ReadOnlySpan<ushort> rewards, bool selected)
    {
        for (var index = 0; index < rewards.Length; index++)
        {
            var cardId = rewards[index];
            if (TriadOwnership.IsOwned(cardId))
            {
                continue;
            }

            if (selected)
            {
                configuration.SelectedCards.Add(cardId);
                continue;
            }

            configuration.SelectedCards.Remove(cardId);
        }
    }

    private static void ToggleCard(Configuration configuration, ushort cardId, bool selected)
    {
        if (selected)
        {
            configuration.SelectedCards.Add(cardId);
        }
        else
        {
            configuration.SelectedCards.Remove(cardId);
        }

        OnSelectionChanged(configuration);
    }

    private static void OnSelectionChanged(Configuration configuration)
    {
        TriadLauncher.MarkSelectionChanged();
        configuration.SaveDebounced();
    }
}
