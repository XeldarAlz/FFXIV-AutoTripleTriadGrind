using System.Threading.Tasks;

namespace AutoTripleTriadGrind.Core.Tasks;

internal sealed class AutoCollect(AutoTriadSession session, TriadProgress progress) : AutoCommon
{
    private readonly AutoTriadSession session = session;
    private readonly TriadProgress progress = progress;

    protected override async Task Execute()
    {
        progress.SetPhase(TriadPhase.Planning);
        Warn($"Collect runs are not available yet (session {session.StartedAt:u}).");
        await DelayMs(0);
    }
}
