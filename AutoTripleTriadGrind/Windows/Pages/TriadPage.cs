using AutoTripleTriadGrind.Core.Localization;
using AutoTripleTriadGrind.Core.Tasks;
using AutoTripleTriadGrind.Windows.Components;
using AutoTripleTriadGrind.Windows.Sections;
using AutoTripleTriadGrind.Windows.Shell;
using Dalamud.Interface;

namespace AutoTripleTriadGrind.Windows.Pages;

internal sealed class TriadPage
{
    private const float SwitchRevealMs = 320f;

    private static readonly TriadRunMode[] modes = [TriadRunMode.Collect, TriadRunMode.Farm];

    private readonly Segmented.Item[] modeItems = new Segmented.Item[modes.Length];

    private bool scrollToLibrary;

    public void Draw(Plugin plugin, AppWindow window)
    {
        var configuration = plugin.Configuration;
        var controller = plugin.Controller;
        var running = controller.Running;

        using var reveal = Motion.PushSwitch("##attg_triad_state", running, SwitchRevealMs);
        if (running)
        {
            RunningPanel.Draw(controller);
            return;
        }

        DrawIdle(plugin, window, configuration, controller);
    }

    private void DrawIdle(Plugin plugin, AppWindow window, Configuration configuration, AutoTriadController controller)
    {
        if (Headline.Draw(configuration, controller, plugin.History))
        {
            window.Show(AppWindow.Page.Plugins);
        }

        Styling.VSpace(20f);
        DrawModeSwitch(configuration, controller);

        Styling.VSpace(14f);
        if (PlanCard.Draw(configuration, controller))
        {
            scrollToLibrary = true;
        }

        Styling.VSpace(26f);
        using (Motion.PushSwitch("##attg_mode", (int)configuration.RunMode))
        {
            if (configuration.RunMode == TriadRunMode.Farm)
            {
                FarmLibrary.Draw(configuration, controller, scrollToLibrary);
            }
            else
            {
                CollectionLibrary.Draw(configuration, controller, scrollToLibrary);
            }
        }

        scrollToLibrary = false;
        Styling.VSpace(12f);
    }

    private void DrawModeSwitch(Configuration configuration, AutoTriadController controller)
    {
        modeItems[0] = new Segmented.Item(FontAwesomeIcon.ThLarge, Loc.T(L.Triad.ModeCollect));
        modeItems[1] = new Segmented.Item(FontAwesomeIcon.Redo, Loc.T(L.Triad.ModeFarm));
        var selected = Math.Max(0, Array.IndexOf(modes, configuration.RunMode));
        if (!Segmented.Draw("##attg_mode_switch", modeItems, ref selected, enabled: !controller.Running, height: Layout.SegmentHeight))
        {
            return;
        }

        configuration.RunMode = modes[selected];
        TriadLauncher.MarkSelectionChanged();
        configuration.SaveDebounced();
    }
}
