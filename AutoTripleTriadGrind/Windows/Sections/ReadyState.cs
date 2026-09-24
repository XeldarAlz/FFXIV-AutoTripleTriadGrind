using AutoTripleTriadGrind.Core.External;
using AutoTripleTriadGrind.Core.Localization;
using AutoTripleTriadGrind.Core.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using System.Numerics;

namespace AutoTripleTriadGrind.Windows.Sections;

internal static class ReadyState
{
    public enum Kind { SetupNeeded, DataUnavailable, PickCards, PickNpcs, NothingReachable, AllDone, Ready, Running, Paused }

    public readonly record struct Info(Kind Kind, Vector4 Accent, Vector4 AccentSoft, FontAwesomeIcon Icon, string Title, string Detail);

    private static int cachedFrame = -1;
    private static Info cached;

    public static Info Resolve(Configuration configuration, AutoTriadController controller)
    {
        var frame = ImGui.GetFrameCount();
        if (frame == cachedFrame)
        {
            return cached;
        }

        cached = Compute(configuration, controller);
        cachedFrame = frame;
        return cached;
    }

    private static Info Compute(Configuration configuration, AutoTriadController controller)
    {
        if (controller.Running)
        {
            if (controller.Paused)
            {
                var detail = controller.PauseReason == PauseReason.InContent
                    ? Loc.T(L.Triad.DetailPausedInContent)
                    : Loc.T(L.Triad.DetailPausedManual);
                return new Info(Kind.Paused, Styling.AccentAmber, Styling.AccentAmberSoft, FontAwesomeIcon.Pause, Loc.T(L.Triad.TitlePaused), detail);
            }

            return new Info(Kind.Running, Styling.AccentBlue, Styling.AccentBlueSoft, FontAwesomeIcon.Play, Loc.T(L.Triad.TitleRunning), PhaseLabel(controller.Phase));
        }

        if (!ExternalPlugins.AllRequiredInstalled())
        {
            return new Info(Kind.SetupNeeded, Styling.AccentRose, Styling.AccentRoseSoft, FontAwesomeIcon.ExclamationTriangle,
                Loc.T(L.Triad.TitleSetupNeeded), Loc.T(L.Triad.DetailSetupNeeded));
        }

        var assessment = TriadLauncher.Assess(configuration);
        var farm = assessment.Mode == TriadRunMode.Farm;
        return assessment.Readiness switch
        {
            TriadLauncher.Readiness.DataUnavailable => new Info(Kind.DataUnavailable, Styling.AccentRose, Styling.AccentRoseSoft, FontAwesomeIcon.ExclamationTriangle,
                Loc.T(L.Triad.TitleDataUnavailable), Loc.T(L.Triad.DetailDataUnavailable)),
            TriadLauncher.Readiness.NothingPicked => farm
                ? new Info(Kind.PickNpcs, Styling.AccentAmber, Styling.AccentAmberSoft, FontAwesomeIcon.User, Loc.T(L.Triad.TitlePickNpcs), Loc.T(L.Triad.DetailPickNpcs))
                : new Info(Kind.PickCards, Styling.AccentAmber, Styling.AccentAmberSoft, FontAwesomeIcon.ThLarge, Loc.T(L.Triad.TitlePickCards), Loc.T(L.Triad.DetailPickCards)),
            TriadLauncher.Readiness.NothingReachable => new Info(Kind.NothingReachable, Styling.AccentAmber, Styling.AccentAmberSoft, FontAwesomeIcon.Ban,
                Loc.T(L.Triad.TitleNothingReachable), Loc.T(L.Triad.DetailNothingReachable)),
            TriadLauncher.Readiness.AllDone => new Info(Kind.AllDone, Styling.AccentMint, Styling.AccentMintSoft, FontAwesomeIcon.CheckDouble,
                Loc.T(L.Triad.TitleAllDone), Loc.T(L.Triad.DetailAllDone)),
            _ => new Info(Kind.Ready, Styling.AccentMint, Styling.AccentMintSoft, FontAwesomeIcon.CheckCircle,
                Loc.T(L.Triad.TitleReady), Loc.T(L.Triad.DetailReady)),
        };
    }

    public static string ShortLabel(Kind kind) => kind switch
    {
        Kind.Running          => Loc.T(L.Shell.StatusRunning),
        Kind.Paused           => Loc.T(L.Shell.StatusPaused),
        Kind.Ready            => Loc.T(L.Shell.StatusReady),
        Kind.PickCards        => Loc.T(L.Shell.StatusPickCards),
        Kind.PickNpcs         => Loc.T(L.Shell.StatusPickNpcs),
        Kind.NothingReachable => Loc.T(L.Shell.StatusBlocked),
        Kind.AllDone          => Loc.T(L.Shell.StatusAllDone),
        Kind.SetupNeeded      => Loc.T(L.Shell.StatusSetupNeeded),
        Kind.DataUnavailable  => Loc.T(L.Shell.StatusSetupNeeded),
        _                     => Loc.T(L.Shell.StatusIdle),
    };

    public static string PhaseLabel(TriadPhase phase) => phase switch
    {
        TriadPhase.Registering => Loc.T(L.Run.PhaseRegistering),
        TriadPhase.Planning    => Loc.T(L.Run.PhasePlanning),
        TriadPhase.Optimizing  => Loc.T(L.Run.PhaseOptimizing),
        TriadPhase.Travelling  => Loc.T(L.Run.PhaseTravelling),
        TriadPhase.Challenging => Loc.T(L.Run.PhaseChallenging),
        TriadPhase.Playing     => Loc.T(L.Run.PhasePlaying),
        TriadPhase.Rematch     => Loc.T(L.Run.PhaseRematch),
        TriadPhase.Finishing   => Loc.T(L.Run.PhaseFinishing),
        TriadPhase.Paused      => Loc.T(L.Run.PhasePaused),
        _                      => Loc.T(L.Run.PhaseStandingBy),
    };
}
