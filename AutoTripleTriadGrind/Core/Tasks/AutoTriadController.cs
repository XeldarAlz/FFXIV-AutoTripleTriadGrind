using AutoTripleTriadGrind.Core.External;
using AutoTripleTriadGrind.Core.Ipc;
using clib.Services;

namespace AutoTripleTriadGrind.Core.Tasks;

internal sealed partial class AutoTriadController
{
    private readonly TriadProgress progress = new();

    private AutoTriadSession? session;
    private AutoCommon? currentTask;

    public bool Running => Svc.Automation.Running || Paused;

    public string Status => PauseReason switch
    {
        PauseReason.InContent => "Paused while you are in content",
        PauseReason.Manual    => "Paused",
        _                     => Svc.Automation.CurrentTask?.Status ?? "Idle",
    };

    public TriadPhase Phase => progress.Phase;

    public TriadProgress Progress => progress;

    public AutoTriadSession? SessionSnapshot => session;

    public TriadRunMode Mode => session?.Mode ?? Plugin.Instance.Configuration.RunMode;

    private static void Diag(string message)
        => RunLog.Info(message);

    public void Start(TriadRunMode mode)
    {
        if (!RequiredPluginsReady())
        {
            return;
        }

        PauseReason = PauseReason.None;
        ResetFaultBudget();
        session = new AutoTriadSession(mode);
        Diag($"Run starting: {mode}.");
        StartRun(session);
    }

    public void Stop()
    {
        var ending = session;
        var wasPaused = Paused;
        currentTask = null;
        PauseReason = PauseReason.None;
        Svc.Automation.Stop();
        if (ending is not null)
        {
            ReleaseHelpers();
        }

        FinalizeRun(ending);
        ClearRun();
        if (ending is not null)
        {
            Diag($"Stop requested{(wasPaused ? " while paused" : string.Empty)}; session cleared.");
        }
    }

    private static bool RequiredPluginsReady()
    {
        if (ExternalPlugins.AllRequiredInstalled())
        {
            return true;
        }

        var missing = ExternalPlugins.MissingRequiredNames();
        Diag($"Start aborted: required plugins missing ({missing}).");
        ECommons.DalamudServices.Svc.Chat.PrintError($"{AttgConstants.LogPrefix} Cannot start: install all required plugins first ({missing}).");
        return false;
    }

    private void StartRun(AutoTriadSession owningSession)
    {
        progress.Reset();
        progress.SetPhase(TriadPhase.Planning);
        RunTask(CreateRunTask(owningSession), () => OnRunEnded(owningSession));
    }

    private AutoCommon CreateRunTask(AutoTriadSession owningSession) => owningSession.Mode switch
    {
        TriadRunMode.Farm => new AutoFarm(owningSession, progress),
        _                 => new AutoCollect(owningSession, progress),
    };

    // The movement library fires OnCompleted off the game thread: its await of the task does not return to the framework
    // scheduler, and the runtime moves the continuation to the thread pool. Recording a run reads game state, which
    // Dalamud allows only on the game thread, so the hand-off is moved back there.
    private void RunTask(AutoCommon task, Action onCompleted)
    {
        currentTask = task;
        Svc.Automation.Start(task, OnCompleted: () => _ = ECommons.DalamudServices.Svc.Framework.RunOnFrameworkThread(() => HandOff(task, onCompleted)));
    }

    private void HandOff(AutoCommon task, Action onCompleted)
    {
        if (!ReferenceEquals(currentTask, task))
        {
            Diag($"{task.GetType().Name} finished but is no longer the current task (stopped, paused, or superseded); skipping hand-off.");
            return;
        }

        currentTask = null;
        onCompleted();
    }

    private void ClearRun()
    {
        session = null;
        progress.Reset();
    }

    // A stopped task unwinds on a later frame, and after an unload that frame may never come, so the pathfinder is
    // released here as well.
    private static void ReleaseHelpers() => NavmeshIPC.Instance.Stop();
}
