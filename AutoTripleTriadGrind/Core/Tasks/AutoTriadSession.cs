using AutoTripleTriadGrind.Core.Planning;
using AutoTripleTriadGrind.Core.Triad.Match;
using System.Threading;

namespace AutoTripleTriadGrind.Core.Tasks;

public sealed class AutoTriadSession(TriadRunMode mode)
{
    private readonly List<ushort> cardsObtained = [];
    private readonly List<SkippedNpc> skippedNpcs = [];

    private long pausedMs;
    private long pauseStartedAtMs;
    private DateTime? endedAt;

    public TriadRunMode Mode { get; } = mode;

    public DateTime StartedAt { get; } = DateTime.UtcNow;

    public int MatchesWon { get; private set; }

    public int MatchesLost { get; private set; }

    public int MatchesDrawn { get; private set; }

    public int NpcsCompleted { get; private set; }

    public int CardsRegistered { get; private set; }

    public IReadOnlyList<ushort> CardsObtained => cardsObtained;

    public IReadOnlyList<SkippedNpc> SkippedNpcs => skippedNpcs;

    public bool EndedWithFault { get; private set; }

    public bool CompletedByStopCondition;

    public bool Recorded;

    public bool AfterActionDispatched;

    public int MatchesPlayed => MatchesWon + MatchesLost + MatchesDrawn;

    public bool DidNothing => MatchesPlayed == 0 && CardsRegistered == 0;

    public TimeSpan Elapsed => (endedAt ?? DateTime.UtcNow) - StartedAt - TimeSpan.FromMilliseconds(PausedTotalMs);

    private long PausedTotalMs => pausedMs + (pauseStartedAtMs == 0 ? 0 : Environment.TickCount64 - pauseStartedAtMs);

    public void RecordMatch(MatchOutcome outcome)
    {
        switch (outcome)
        {
            case MatchOutcome.Won:
                MatchesWon++;
                break;
            case MatchOutcome.Lost:
                MatchesLost++;
                break;
            case MatchOutcome.Drawn:
                MatchesDrawn++;
                break;
        }
    }

    public void RecordCardObtained(ushort cardId)
    {
        if (cardsObtained.Contains(cardId))
        {
            return;
        }

        cardsObtained.Add(cardId);
    }

    public void RecordCardRegistered() => CardsRegistered++;

    public void RecordNpcCompleted() => NpcsCompleted++;

    public void RecordSkip(ushort npcIndex, SkipReason reason) => skippedNpcs.Add(new SkippedNpc(npcIndex, reason));

    // A Stop unwinds the run loop through the same catch as a genuine fault, and only a genuine fault may resume the run.
    public void RecordFault(Exception exception, CancellationToken cancelToken)
    {
        if (cancelToken.IsCancellationRequested || exception is OperationCanceledException)
        {
            return;
        }

        EndedWithFault = true;
        // clib's task runner writes the same exception to dalamud.log when the task unwinds.
        RunLog.Record(RunLogLevel.Error, exception, "The triad task ended with an unexpected error");
    }

    internal void ClearFault() => EndedWithFault = false;

    public void BeginPause()
    {
        if (pauseStartedAtMs != 0)
        {
            return;
        }

        pauseStartedAtMs = Environment.TickCount64;
    }

    public void EndPause()
    {
        if (pauseStartedAtMs == 0)
        {
            return;
        }

        pausedMs += Environment.TickCount64 - pauseStartedAtMs;
        pauseStartedAtMs = 0;
    }

    // Freezes the clock, so a finished run left on screen during its after-run action stops counting.
    internal void End()
    {
        if (endedAt is not null)
        {
            return;
        }

        EndPause();
        endedAt = DateTime.UtcNow;
    }
}

public readonly record struct SkippedNpc(ushort NpcIndex, SkipReason Reason);
