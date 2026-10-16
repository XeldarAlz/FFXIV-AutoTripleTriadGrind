namespace AutoTripleTriadGrind.Core;

internal static class AttgConstants
{
    public const string PrimaryCommand = "/attg";
    public const string AliasCommand = "/tripletriad";

    public const string LogPrefix = "[ATTG]";

    public const int SaveThrottleMs = 500;

    public static readonly int BackgroundWorkerThreads = Math.Max(1, Environment.ProcessorCount / 4);

    internal static class ThrottleKeys
    {
        public const string Save = "AutoTripleTriadGrind.Save";
    }
}
