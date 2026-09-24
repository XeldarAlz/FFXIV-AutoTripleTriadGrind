using AutoTripleTriadGrind.Core.External;
using AutoTripleTriadGrind.Core.Localization;
using AutoTripleTriadGrind.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoTripleTriadGrind.Windows.Shell;

internal static class ActionDock
{
    private const float PadX = 18f;
    private const float ButtonGap = 8f;

    public static void Draw(Plugin plugin, Vector2 size, float windowRounding)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        Dock.Background(drawList, origin, end, windowRounding);

        var padX = PadX * scale;
        var buttonHeight = Layout.HeroButtonHeight * scale;
        var innerWidth = size.X - padX * 2f;
        ImGui.SetCursorScreenPos(new Vector2(origin.X + padX, origin.Y + (size.Y - buttonHeight) * 0.5f));

        if (plugin.Controller.Running)
        {
            DrawRunControls(plugin, innerWidth);
        }
        else
        {
            DrawStart(plugin, innerWidth);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
    }

    private static void DrawRunControls(Plugin plugin, float innerWidth)
    {
        var controller = plugin.Controller;
        var gap = ButtonGap * ImGuiHelpers.GlobalScale;
        var half = (innerWidth - gap) * 0.5f;

        if (PauseButton.Draw(controller.PauseReason, half))
        {
            controller.TogglePause();
        }

        ImGui.SameLine(0f, gap);

        var session = controller.SessionSnapshot;
        var state = controller.Paused ? Loc.T(L.Triad.StatePaused) : Loc.T(L.Triad.StateRunning);
        var stopSub = session is null ? state : Loc.T(L.Triad.StopSub, state, Formatting.Elapsed(session.Elapsed));
        if (StopButton.Draw(stopSub, half))
        {
            controller.Stop();
        }
    }

    private static void DrawStart(Plugin plugin, float innerWidth)
    {
        var configuration = plugin.Configuration;
        var assessment = TriadLauncher.Assess(configuration);
        var dependenciesReady = ExternalPlugins.AllRequiredInstalled();
        var canStart = assessment.Readiness == TriadLauncher.Readiness.Ready && dependenciesReady;
        var reason = !dependenciesReady ? Loc.T(L.Triad.ReasonInstall) : TriadLauncher.Reason(assessment);

        if (StartButton.Draw(TriadLauncher.Sublabel(assessment), canStart, reason, innerWidth))
        {
            TriadLauncher.Start(assessment.Mode);
        }
    }
}
