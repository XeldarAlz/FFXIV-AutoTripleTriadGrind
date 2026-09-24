using AutoTripleTriadGrind.Core.Tasks;

namespace AutoTripleTriadGrind.Windows.Sections;

internal static class RunWorkload
{
    public readonly record struct Workload(int Done, int Needed)
    {
        public float Fraction => Needed > 0 ? Math.Clamp(Done / (float)Needed, 0f, 1f) : 0f;
    }

    public static Workload Measure(AutoTriadController controller)
    {
        var session = controller.SessionSnapshot;
        if (session is null)
        {
            return default;
        }

        if (session.Mode == TriadRunMode.Farm)
        {
            var configuration = Plugin.Instance.Configuration;
            return configuration.FarmStop == FarmStopKind.Matches
                ? new Workload(session.MatchesPlayed, Math.Max(1, configuration.FarmMatchLimit) * Math.Max(1, controller.Progress.Queue.Count))
                : default;
        }

        var progress = controller.Progress;
        return new Workload(progress.QueueNext, progress.Queue.Count);
    }
}
