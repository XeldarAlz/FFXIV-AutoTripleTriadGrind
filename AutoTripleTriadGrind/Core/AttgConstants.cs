namespace AutoTripleTriadGrind.Core;

internal static class AttgConstants
{
    public const string PrimaryCommand = "/attg";
    public const string AliasCommand = "/tripletriad";

    public const string LogPrefix = "[ATTG]";

    public const int SaveThrottleMs = 500;

    internal static class ThrottleKeys
    {
        public const string Save = "AutoTripleTriadGrind.Save";
    }
}
