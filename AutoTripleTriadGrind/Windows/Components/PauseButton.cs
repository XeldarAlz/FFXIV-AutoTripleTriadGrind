using AutoTripleTriadGrind.Core.Localization;
using AutoTripleTriadGrind.Core.Tasks;
using Dalamud.Interface;

namespace AutoTripleTriadGrind.Windows.Components;

internal static class PauseButton
{
    public static bool Draw(PauseReason reason, float width = 0f) => reason switch
    {
        PauseReason.InContent => HeroButton.Draw(FontAwesomeIcon.Play, Loc.T(L.Triad.ResumeCaps), null, Styling.AccentMint, false, Loc.T(L.Triad.InContent), width),
        PauseReason.Manual    => HeroButton.Draw(FontAwesomeIcon.Play, Loc.T(L.Triad.ResumeCaps), null, Styling.AccentMint, true, null, width),
        _                     => HeroButton.Draw(FontAwesomeIcon.Pause, Loc.T(L.Triad.PauseCaps), null, Styling.AccentAmber, true, null, width),
    };
}
