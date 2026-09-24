using AutoTripleTriadGrind.Core.Localization;
using AutoTripleTriadGrind.Core.Tasks;
using AutoTripleTriadGrind.Core.Triad.Data;
using AutoTripleTriadGrind.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoTripleTriadGrind.Windows.Sections;

internal static class RunningPanel
{
    private const float PadX = 18f;
    private const float HeroCardSize = 34f;
    private const float QueueCardSize = 30f;
    private const float CardGap = 4f;
    private const int QueueLength = 6;

    private static readonly CachedText[] rowMeta = new CachedText[QueueLength];
    private static CachedText footerText;
    private static CachedText matchesText;
    private static CachedText recordText;
    private static CachedText goalText;

    public static void Draw(AutoTriadController controller)
    {
        TriadOwnership.Refresh();
        var paused = controller.Paused;
        var (accent, accentSoft, label) = PhasePalette(controller);
        var workload = RunWorkload.Measure(controller);

        DrawHeaderStrip(Footer(controller), accent, accentSoft, paused);
        Styling.VSpace(6f);
        DrawHeroCard(controller, workload, accent, accentSoft, label);

        Styling.VSpace(10f);
        DrawStatTiles(controller);

        Styling.VSpace(10f);
        DrawQueue(controller);
        DrawSkipped(controller);
    }

    private static string Footer(AutoTriadController controller)
    {
        var progress = controller.Progress;
        var key = ((long)progress.QueueNext << 20) | (uint)progress.Queue.Count;
        return footerText.Get(key, static packed => Loc.T(L.Run.Footer, Math.Min((int)(packed >> 20) + 1, (int)(packed & 0xFFFFF)), (int)(packed & 0xFFFFF)));
    }

    private static void DrawHeaderStrip(string footer, Vector4 accent, Vector4 accentSoft, bool paused)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var available = ImGui.GetContentRegionAvail().X;
        var lineHeight = ImGui.GetTextLineHeight();
        var midY = origin.Y + lineHeight * 0.5f;

        var dot = paused ? accent : Styling.PulseColor(accent, accentSoft, Styling.PulseMedium);
        var radius = 4f * scale;
        Paint.Dot(drawList, new Vector2(origin.X + radius + 3f * scale, midY), radius, dot);

        var status = paused ? Loc.T(L.Shell.StatusPaused) : Loc.T(L.Shell.StatusRunning);
        var statusSize = TextDraw.SmallCapsSize(status);
        TextDraw.SmallCaps(status, new Vector2(origin.X + radius * 2f + 12f * scale, midY - statusSize.Y * 0.5f), Styling.TextSecondary);

        using (Fonts.PushCaption())
        {
            var footerSize = TextDraw.Measure(footer);
            TextDraw.At(footer, new Vector2(origin.X + available - footerSize.X, midY - footerSize.Y * 0.5f), Styling.TextMuted);
        }

        ImGui.Dummy(new Vector2(available, lineHeight));
    }

    private static void DrawHeroCard(AutoTriadController controller, RunWorkload.Workload workload, Vector4 accent, Vector4 accentSoft, string label)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(ImGui.GetContentRegionAvail().X, Layout.HeroCardHeight * scale);
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        var active = controller.Running && !controller.Paused;
        var rounding = Styling.PanelRounding * scale;

        Paint.Glass(drawList, origin, end, rounding, accent, active ? 0.10f : 0.03f, 0f, elevated: true);
        if (active)
        {
            Paint.Stroke(drawList, origin, end, Styling.PulseColor(Styling.WithAlpha(accent, 0.5f), accentSoft, Styling.PulseMedium), rounding, 1.6f);
        }

        var padX = PadX * scale;
        var ringRadius = size.Y * 0.5f - 18f * scale;
        var ringCenter = new Vector2(origin.X + padX + ringRadius, origin.Y + size.Y * 0.5f);
        DrawRing(ringCenter, ringRadius, accent, active, workload);

        var columnX = ringCenter.X + ringRadius + 20f * scale;
        var columnWidth = end.X - padX - columnX;
        var y = origin.Y + 16f * scale;
        y += DrawPhaseChip(columnX, y, label, accent, accentSoft) + 10f * scale;

        var npcIndex = controller.Progress.CurrentNpcIndex;
        var set = TriadData.Set;
        var lineHeight = ImGui.GetTextLineHeight();
        if (npcIndex != TriadData.NoNpc && npcIndex < set.NpcCount)
        {
            var progress = controller.Progress;
            var record = recordText.Get(((long)progress.NpcMatchesWon << 32) | ((long)progress.NpcMatchesLost << 16) | (uint)progress.NpcMatchesDrawn,
                static key => Loc.T(L.Run.Record, (int)(key >> 32), (int)((key >> 16) & 0xFFFF), (int)(key & 0xFFFF)));
            var recordWidth = TextDraw.Measure(record).X;
            TextDraw.At(record, new Vector2(columnX + columnWidth - recordWidth, y), accentSoft);
            TextDraw.At(TextDraw.Truncate(set.NpcNames[npcIndex], columnWidth - recordWidth - 12f * scale), new Vector2(columnX, y), Styling.TextStrong);
            y += lineHeight + 3f * scale;
            using (Fonts.PushCaption())
            {
                var location = TextDraw.Truncate(TriadLabels.NpcLocation(npcIndex), columnWidth);
                TextDraw.At(location, new Vector2(columnX, y), Styling.TextDim);
                TextDraw.Trailing(controller.Status, columnX + TextDraw.Measure(location).X, columnX + columnWidth, y, Styling.TextMuted);
                y += ImGui.GetTextLineHeight();
            }
        }
        else
        {
            var status = string.IsNullOrWhiteSpace(controller.Status) ? Loc.T(L.Common.Working) : controller.Status;
            TextDraw.At(TextDraw.Truncate(status, columnWidth), new Vector2(columnX, y), Styling.TextSecondary);
            y += lineHeight;
        }

        y += 10f * scale;
        var barHeight = 8f * scale;
        var barOrigin = new Vector2(columnX, y);
        if (controller.Phase == TriadPhase.Optimizing)
        {
            Paint.Bar(drawList, barOrigin, columnWidth, barHeight, controller.Progress.OptimizerProgress, accent);
        }
        else if (active)
        {
            Paint.IndeterminateBar(drawList, barOrigin, columnWidth, barHeight, accent);
        }
        else
        {
            Paint.Bar(drawList, barOrigin, columnWidth, barHeight, 0f, accent);
        }

        y += barHeight + 8f * scale;
        if (npcIndex != TriadData.NoNpc && npcIndex < set.NpcCount)
        {
            DrawCardStrip(drawList, set.RewardsOf(npcIndex), columnX, y, HeroCardSize * scale, end.X - padX);
        }

        ImGui.Dummy(size);
    }

    private static void DrawCardStrip(ImDrawListPtr drawList, ReadOnlySpan<ushort> cards, float x, float y, float size, float rightX)
    {
        var gap = CardGap * ImGuiHelpers.GlobalScale;
        for (var index = 0; index < cards.Length; index++)
        {
            var cardX = x + index * (size + gap);
            if (cardX + size > rightX)
            {
                return;
            }

            var cardId = cards[index];
            CardIcon.Draw(drawList, cardId, new Vector2(cardX, y), size, TriadOwnership.IsOwned(cardId) ? CardIcon.State.Owned : CardIcon.State.Missing);
        }
    }

    private static void DrawRing(Vector2 center, float radius, Vector4 accent, bool active, RunWorkload.Workload workload)
    {
        var thickness = 6f * ImGuiHelpers.GlobalScale;
        ProgressRing.Track(center, radius, thickness, Styling.WithAlpha(Styling.BorderDim, 0.7f));
        if (workload.Needed == 0)
        {
            if (active)
            {
                ProgressRing.Sweep(center, radius, thickness, accent, Styling.PulseOrbit, MathF.PI * 0.6f, 1f);
            }

            ProgressRing.CenterIcon(center, FontAwesomeIcon.ThLarge, Styling.TextDim, radius * 0.55f);
            return;
        }

        var fraction = Motion.Approach(Motion.Key("##attg_run_ring"), workload.Fraction, 6f);
        ProgressRing.Fill(center, radius, thickness, fraction, accent);
        var goal = goalText.Get(workload.Needed, static key => Loc.T(L.Run.GoalOf, (int)key));
        ProgressRing.CenterValue(center, workload.Done.ToString(Loc.Culture), goal, Styling.TextStrong, Styling.TextDim);
    }

    private static float DrawPhaseChip(float x, float y, string text, Vector4 accent, Vector4 accentSoft)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var padX = 9f * scale;
        var padY = 3f * scale;

        using (Fonts.PushCaption())
        {
            var label = TextDraw.Upper(text);
            var textSize = TextDraw.Measure(label);
            var chipMin = new Vector2(x, y);
            var chipMax = chipMin + new Vector2(padX * 2f + textSize.X, textSize.Y + padY * 2f);
            Paint.Pill(drawList, chipMin, chipMax, Styling.WithAlpha(accent, 0.28f), Styling.WithAlpha(accent, 0.65f));
            TextDraw.At(label, new Vector2(x + padX, y + padY), accentSoft);
            return chipMax.Y - chipMin.Y;
        }
    }

    private static (Vector4 Accent, Vector4 AccentSoft, string Label) PhasePalette(AutoTriadController controller)
    {
        if (!controller.Running)
        {
            return (Styling.TextDim, Styling.TextSecondary, Loc.T(L.Run.PhaseReady));
        }

        if (controller.Paused)
        {
            return controller.PauseReason == PauseReason.InContent
                ? (Styling.AccentAmber, Styling.AccentAmberSoft, Loc.T(L.Run.PhasePausedInContent))
                : (Styling.AccentAmber, Styling.AccentAmberSoft, Loc.T(L.Run.PhasePaused));
        }

        return controller.Phase switch
        {
            TriadPhase.Playing   => (Styling.AccentGlow, Styling.AccentGlowSoft, ReadyState.PhaseLabel(controller.Phase)),
            TriadPhase.Finishing => (Styling.AccentMint, Styling.AccentMintSoft, ReadyState.PhaseLabel(controller.Phase)),
            TriadPhase.Idle      => (Styling.TextDim,    Styling.TextSecondary,  ReadyState.PhaseLabel(controller.Phase)),
            _                    => (Styling.AccentBlue, Styling.AccentBlueSoft, ReadyState.PhaseLabel(controller.Phase)),
        };
    }

    private static void DrawStatTiles(AutoTriadController controller)
    {
        var session = controller.SessionSnapshot;
        var scale = ImGuiHelpers.GlobalScale;
        var gap = 8f * scale;
        var tileWidth = (ImGui.GetContentRegionAvail().X - gap * 3f) / 4f;
        var won = session?.MatchesWon ?? 0;
        var lost = session?.MatchesLost ?? 0;
        var drawn = session?.MatchesDrawn ?? 0;
        var matches = matchesText.Get(((long)won << 32) | ((long)lost << 16) | (uint)drawn,
            static key => Loc.T(L.Run.Record, (int)(key >> 32), (int)((key >> 16) & 0xFFFF), (int)(key & 0xFFFF)));

        StatTile.Draw(Loc.T(L.Run.TileMatches), (won + lost + drawn).ToString(Loc.Culture), matches, Styling.AccentGlow, tileWidth);
        ImGui.SameLine(0, gap);
        StatTile.Draw(Loc.T(L.Run.TileCards), (session?.CardsObtained.Count ?? 0).ToString(Loc.Culture), null, Styling.AccentMint, tileWidth);
        ImGui.SameLine(0, gap);
        StatTile.Draw(Loc.T(L.Run.TileNpcs), (session?.NpcsCompleted ?? 0).ToString(Loc.Culture), null, Styling.AccentAmber, tileWidth);
        ImGui.SameLine(0, gap);
        StatTile.Draw(Loc.T(L.Run.TileElapsed), Formatting.Elapsed(session?.Elapsed ?? TimeSpan.Zero), null, Styling.AccentBlue, tileWidth);
    }

    private static void DrawQueue(AutoTriadController controller)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var heading = Loc.T(L.Run.UpNext);
        var labelSize = TextDraw.SectionTitleSize(heading);
        TextDraw.SectionTitle(heading, origin, Styling.TextStrong);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, labelSize.Y + 8f * scale));

        var progress = controller.Progress;
        var queue = progress.Queue;
        var start = Math.Min(progress.QueueNext, queue.Count);
        if (start >= queue.Count)
        {
            TextDraw.Hint(Loc.T(queue.Count == 0 ? L.Run.QueuePlanning : L.Run.QueueDone));
            return;
        }

        var shown = Math.Min(QueueLength, queue.Count - start);
        for (var offset = 0; offset < shown; offset++)
        {
            DrawQueueRow(offset, queue[start + offset], offset == 0);
        }
    }

    private static void DrawQueueRow(int slot, ushort npcIndex, bool emphasize)
    {
        var set = TriadData.Set;
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(ImGui.GetContentRegionAvail().X, Layout.QueueRowHeight * scale);
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        Paint.Glass(drawList, origin, end, Styling.CardRounding * scale, Styling.AccentGlow, emphasize ? 0.10f : 0.03f);

        var padX = 13f * scale;
        var midY = origin.Y + size.Y * 0.5f;
        var rewards = set.RewardsOf(npcIndex);
        var cardSize = QueueCardSize * scale;
        var cardsWidth = rewards.Length * (cardSize + CardGap * scale);
        var cardsLeft = end.X - padX - cardsWidth;
        DrawCardStrip(drawList, rewards, cardsLeft, midY - cardSize * 0.5f, cardSize, end.X);

        var iconSize = TextDraw.IconSize(FontAwesomeIcon.User);
        var lineHeight = ImGui.GetTextLineHeight();
        TextDraw.Icon(FontAwesomeIcon.User, new Vector2(origin.X + padX, midY - lineHeight + (lineHeight - iconSize.Y) * 0.5f), emphasize ? Styling.AccentGlow : Styling.TextDim);
        var textX = origin.X + padX + iconSize.X + 10f * scale;
        var textWidth = cardsLeft - 12f * scale - textX;
        TextDraw.At(TextDraw.Truncate(set.NpcNames[npcIndex], textWidth), new Vector2(textX, midY - lineHeight), emphasize ? Styling.TextStrong : Styling.TextSecondary);
        var meta = rowMeta[slot].Get(npcIndex, static key => TriadLabels.NpcLocation((int)key));
        using (Fonts.PushCaption())
        {
            TextDraw.At(TextDraw.Truncate(meta, textWidth), new Vector2(textX, midY + 2f * scale), Styling.TextDim);
        }

        ImGui.Dummy(size);
    }

    private static void DrawSkipped(AutoTriadController controller)
    {
        var session = controller.SessionSnapshot;
        if (session is null || session.SkippedNpcs.Count == 0)
        {
            return;
        }

        var set = TriadData.Set;
        GroupLabel.Draw(Loc.T(L.Run.Skipped), spaceBefore: 10f, extraBelow: 4f);
        var skipped = session.SkippedNpcs;
        for (var index = 0; index < skipped.Count; index++)
        {
            var entry = skipped[index];
            if (entry.NpcIndex >= set.NpcCount)
            {
                continue;
            }

            using (Fonts.PushCaption())
            {
                ImGui.TextColored(Styling.AccentAmber, string.Concat(set.NpcNames[entry.NpcIndex], "  ·  ", TriadLabels.Skip(entry.Reason)));
            }
        }
    }
}
