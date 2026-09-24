using AutoTripleTriadGrind.Core.Game.Ops;
using AutoTripleTriadGrind.Core.Ipc;
using AutoTripleTriadGrind.Core.Planning;
using AutoTripleTriadGrind.Core.Triad.Data;
using AutoTripleTriadGrind.Core.Triad.Decks;
using ECommons.DalamudServices;
using System.Threading.Tasks;

namespace AutoTripleTriadGrind.Core.Tasks;

// Plays the picked NPCs in turn, each for the set number of matches, or the first one until the run is stopped.
internal sealed class AutoFarm(AutoTriadSession session, TriadProgress progress) : AutoCommon
{
    private const int ReadyWaitMs = 30_000;

    private readonly AutoTriadSession session = session;
    private readonly TriadProgress progress = progress;

    protected override async Task Execute()
    {
        try
        {
            await Farm();
        }
        catch (Exception exception)
        {
            session.RecordFault(exception, CancelToken);
            throw;
        }
        finally
        {
            TriadDeckBuilder.Cancel();
            NavmeshIPC.Instance.Stop();
        }
    }

    private async Task Farm()
    {
        TriadData.EnsureLoaded();
        if (!TriadData.Loaded)
        {
            Svc.Chat.PrintError($"{AttgConstants.LogPrefix} {TriadData.FailureReason}");
            return;
        }

        if (!await WaitUntilTimed(NpcInteraction.PlayerReady, ReadyWaitMs, "farm-ready"))
        {
            return;
        }

        var configuration = Plugin.Instance.Configuration;
        var targets = EligibleTargets(configuration);
        if (targets.Length == 0)
        {
            Svc.Chat.PrintError($"{AttgConstants.LogPrefix} None of the picked NPCs can be played right now.");
            return;
        }

        progress.SetQueue(targets);
        var limit = Math.Max(1, configuration.FarmMatchLimit);
        var endless = configuration.FarmStop == FarmStopKind.Never;
        for (var index = 0; index < targets.Length && !CancelToken.IsCancellationRequested; index++)
        {
            progress.SetQueueNext(index);
            var run = new TriadNpcRun(targets[index], run => !endless && run.Matches >= limit);
            Status = $"Farming {run.Name}";
            var result = await PlayNpc(run, session, progress);
            switch (result.End)
            {
                case NpcRunEnd.GoalMet:
                    session.RecordNpcCompleted();
                    break;
                case NpcRunEnd.Skipped:
                    Diag($"Farm: skipping {run.Name} ({result.Reason}).");
                    session.RecordSkip(run.NpcIndex, result.Reason);
                    break;
                default:
                    return;
            }
        }

        progress.SetQueueNext(targets.Length);
        session.CompletedByStopCondition = !CancelToken.IsCancellationRequested;
    }

    private static ushort[] EligibleTargets(Configuration configuration)
    {
        var set = TriadData.Set;
        var targets = new List<ushort>(configuration.FarmNpcs.Count);
        for (var index = 0; index < configuration.FarmNpcs.Count; index++)
        {
            if (set.NpcIndexByTriadRowId.TryGetValue(configuration.FarmNpcs[index], out var npcIndex) && NpcEligibility.Check(npcIndex, configuration) == SkipReason.None)
            {
                targets.Add(npcIndex);
            }
        }

        return targets.ToArray();
    }
}
