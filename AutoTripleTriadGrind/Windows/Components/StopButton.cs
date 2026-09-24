using AutoTripleTriadGrind.Core.Localization;
using Dalamud.Interface;

namespace AutoTripleTriadGrind.Windows.Components;

internal static class StopButton
{
    public static bool Draw(string? sublabel, float width = 0f)
        => HeroButton.Draw(FontAwesomeIcon.Stop, Loc.T(L.Triad.Stop), sublabel, Styling.AccentRose, true, null, width);
}
