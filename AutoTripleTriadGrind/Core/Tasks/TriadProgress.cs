namespace AutoTripleTriadGrind.Core.Tasks;

internal sealed class TriadProgress
{
    private ushort[] queue = [];
    private int queueNext;

    public TriadPhase Phase { get; private set; } = TriadPhase.Idle;

    public ushort CurrentNpcIndex { get; private set; } = Triad.Data.TriadData.NoNpc;

    public int NpcMatchesWon { get; private set; }

    public int NpcMatchesLost { get; private set; }

    public int NpcMatchesDrawn { get; private set; }

    public float OptimizerProgress { get; private set; }

    public IReadOnlyList<ushort> Queue => queue;

    public int QueueNext => queueNext;

    public void SetPhase(TriadPhase phase) => Phase = phase;

    public void SetQueue(ushort[] planned)
    {
        queue = planned;
        queueNext = 0;
    }

    public void SetQueueNext(int queueIndex) => queueNext = queueIndex;

    public void BeginNpc(ushort npcIndex)
    {
        CurrentNpcIndex = npcIndex;
        NpcMatchesWon = 0;
        NpcMatchesLost = 0;
        NpcMatchesDrawn = 0;
        OptimizerProgress = 0f;
    }

    public void RecordNpcMatch(Triad.Match.MatchOutcome outcome)
    {
        switch (outcome)
        {
            case Triad.Match.MatchOutcome.Won:
                NpcMatchesWon++;
                break;
            case Triad.Match.MatchOutcome.Lost:
                NpcMatchesLost++;
                break;
            case Triad.Match.MatchOutcome.Drawn:
                NpcMatchesDrawn++;
                break;
        }
    }

    public void SetOptimizerProgress(float fraction) => OptimizerProgress = fraction;

    public void Reset()
    {
        Phase = TriadPhase.Idle;
        CurrentNpcIndex = Triad.Data.TriadData.NoNpc;
        NpcMatchesWon = 0;
        NpcMatchesLost = 0;
        NpcMatchesDrawn = 0;
        OptimizerProgress = 0f;
        queue = [];
        queueNext = 0;
    }
}

internal enum TriadPhase : byte
{
    Idle,
    Registering,
    Planning,
    Optimizing,
    Travelling,
    Challenging,
    Playing,
    Rematch,
    Finishing,
    Paused,
}
