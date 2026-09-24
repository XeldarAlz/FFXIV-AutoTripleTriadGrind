using AutoTripleTriadGrind.Core.Game.Ops;
using AutoTripleTriadGrind.Core.Ipc;
using AutoTripleTriadGrind.Core.Planning;
using AutoTripleTriadGrind.Core.Triad.Data;
using AutoTripleTriadGrind.Core.Triad.Decks;
using ECommons.DalamudServices;
using System.Threading.Tasks;

namespace AutoTripleTriadGrind.Core.Tasks;

// Plans the route, plays each NPC until the picked cards it gives are owned, and plans again after every pass so a
// skipped NPC's cards move to another NPC that also gives them.
internal sealed class AutoCollect(AutoTriadSession session, TriadProgress progress) : AutoCommon
{
    private const int ReadyWaitMs = 30_000;

    private readonly AutoTriadSession session = session;
    private readonly TriadProgress progress = progress;
    private readonly HashSet<int> excluded = [];

    protected override async Task Execute()
    {
        try
        {
            await Collect();
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

    private async Task Collect()
    {
        TriadData.EnsureLoaded();
        if (!TriadData.Loaded)
        {
            Svc.Chat.PrintError($"{AttgConstants.LogPrefix} {TriadData.FailureReason}");
            return;
        }

        if (!await WaitUntilTimed(NpcInteraction.PlayerReady, ReadyWaitMs, "collect-ready"))
        {
            return;
        }

        var configuration = Plugin.Instance.Configuration;
        while (!CancelToken.IsCancellationRequested)
        {
            progress.SetPhase(TriadPhase.Planning);
            TriadOwnership.Refresh(force: true);
            var plan = CollectPlanner.Build(configuration, excluded);
            var assignments = plan.Assignments;
            if (assignments.Length == 0)
            {
                Diag($"Collect: nothing left to play ({plan.WantedCards} picked cards missing, {plan.Unavailable.Length} unavailable).");
                session.CompletedByStopCondition = true;
                return;
            }

            var queue = new ushort[assignments.Length];
            for (var index = 0; index < assignments.Length; index++)
            {
                queue[index] = assignments[index].NpcIndex;
            }

            progress.SetQueue(queue);
            for (var index = 0; index < assignments.Length && !CancelToken.IsCancellationRequested; index++)
            {
                progress.SetQueueNext(index);
                var cards = assignments[index].Cards;
                var run = new TriadNpcRun(assignments[index].NpcIndex, _ => AllOwned(cards)) { MatchLimit = configuration.MaxMatchesPerNpc };
                Status = $"Collecting from {run.Name}";
                var result = await PlayNpc(run, session, progress);
                switch (result.End)
                {
                    case NpcRunEnd.GoalMet:
                        session.RecordNpcCompleted();
                        break;
                    case NpcRunEnd.Skipped:
                        Diag($"Collect: skipping {run.Name} ({result.Reason}).");
                        session.RecordSkip(run.NpcIndex, result.Reason);
                        excluded.Add(run.NpcIndex);
                        break;
                    default:
                        return;
                }
            }

            progress.SetQueueNext(assignments.Length);
        }
    }

    private static bool AllOwned(ushort[] cards)
    {
        for (var index = 0; index < cards.Length; index++)
        {
            if (!TriadOwnership.IsOwned(cards[index]))
            {
                return false;
            }
        }

        return true;
    }
}
