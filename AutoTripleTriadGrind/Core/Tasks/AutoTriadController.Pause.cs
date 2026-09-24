using clib.Services;

namespace AutoTripleTriadGrind.Core.Tasks;

internal enum PauseReason { None, Manual, InContent }

internal sealed partial class AutoTriadController
{
    public PauseReason PauseReason { get; private set; } = PauseReason.None;

    public bool Paused => PauseReason != PauseReason.None;

    public bool CanPause => session is not null && Phase is not (TriadPhase.Idle or TriadPhase.Paused or TriadPhase.Finishing);

    public void Pause(PauseReason reason)
    {
        if (reason == PauseReason.None)
        {
            return;
        }

        if (Paused)
        {
            if (reason == PauseReason.Manual && PauseReason == PauseReason.InContent)
            {
                PauseReason = PauseReason.Manual;
                Diag("Auto-pause promoted to a manual pause; leaving content will no longer resume.");
            }

            return;
        }

        if (!CanPause)
        {
            if (reason == PauseReason.Manual)
            {
                ECommons.DalamudServices.Svc.Chat.Print($"{AttgConstants.LogPrefix} Nothing to pause.");
            }

            return;
        }

        var pausing = session!;
        PauseReason = reason;
        progress.SetPhase(TriadPhase.Paused);
        pausing.BeginPause();
        currentTask = null;
        Svc.Automation.Stop();
        ReleaseHelpers();

        Diag($"Run paused ({reason}); session kept at {pausing.MatchesPlayed} matches, {pausing.CardsObtained.Count} cards.");
        ECommons.DalamudServices.Svc.Chat.Print(reason == PauseReason.InContent
            ? $"{AttgConstants.LogPrefix} Paused: you are in instanced content. The run resumes once you are back outside."
            : $"{AttgConstants.LogPrefix} Paused. Your run and session stats are kept until you resume or stop.");
    }

    public void Resume()
    {
        if (!Paused)
        {
            return;
        }

        var resuming = session;
        if (resuming is null)
        {
            Diag("Resume requested with no session or nothing to resume; stopping instead.");
            Stop();
            return;
        }

        PauseReason = PauseReason.None;
        resuming.EndPause();
        Diag("Resuming the run.");
        ECommons.DalamudServices.Svc.Chat.Print($"{AttgConstants.LogPrefix} Resuming the run.");
        StartRun(resuming);
    }

    public void TogglePause()
    {
        if (Paused)
        {
            Resume();
            return;
        }

        Pause(PauseReason.Manual);
    }
}
