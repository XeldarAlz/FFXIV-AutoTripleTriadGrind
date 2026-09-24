using AutoTripleTriadGrind.Core.Triad.Data;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using ECommons.DalamudServices;
using System.Numerics;

namespace AutoTripleTriadGrind.Windows.Components;

internal static class CardIcon
{
    public enum State : byte
    {
        Missing,
        Selected,
        Owned,
    }

    public static void Draw(ImDrawListPtr drawList, ushort cardId, Vector2 min, float size, State state, float hover = 0f)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var max = min + new Vector2(size, size);
        var rounding = 6f * scale;
        var card = TriadData.Set.Cards[cardId];
        var owned = state == State.Owned;
        if (Svc.Texture.GetFromGameIcon(new GameIconLookup(card.SmallIconId)).TryGetWrap(out var wrap, out _))
        {
            var tint = owned ? new Vector4(1f, 1f, 1f, 0.38f) : Vector4.One;
            drawList.AddImageRounded(wrap.Handle, min, max, Vector2.Zero, Vector2.One, Paint.Col(tint), rounding);
        }
        else
        {
            Paint.Fill(drawList, min, max, Styling.Surface2, rounding);
        }

        var border = state switch
        {
            State.Selected => Styling.AccentGlow,
            State.Owned    => Styling.WithAlpha(Styling.AccentMint, 0.55f),
            _              => Styling.WithAlpha(Styling.BorderDim, 0.8f + 0.2f * hover),
        };
        Paint.Stroke(drawList, min, max, border, rounding, state == State.Selected ? 2f * scale : 1f * scale);
        if (hover > 0.01f && !owned)
        {
            Paint.Fill(drawList, min, max, Styling.WithAlpha(Styling.Highlight, 1.5f * hover), rounding);
        }

        if (owned)
        {
            var badgeCenter = new Vector2(max.X - size * 0.2f, max.Y - size * 0.2f);
            var radius = size * 0.17f;
            drawList.AddCircleFilled(badgeCenter, radius, Paint.Col(Styling.AccentMint));
            Paint.Check(drawList, badgeCenter, radius * 1.1f, Styling.InkOnGlow, 1.6f * scale);
            return;
        }

        if (state == State.Selected)
        {
            var badgeCenter = new Vector2(max.X - size * 0.2f, min.Y + size * 0.2f);
            var radius = size * 0.17f;
            drawList.AddCircleFilled(badgeCenter, radius, Paint.Col(Styling.AccentGlow));
            Paint.Check(drawList, badgeCenter, radius * 1.1f, Styling.InkOnGlow, 1.6f * scale);
        }
    }
}
