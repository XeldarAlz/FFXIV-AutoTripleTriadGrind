namespace AutoTripleTriadGrind.Core.Tasks;

internal static class PostRunCommandLines
{
    private const char CommandPrefix = '/';

    public static string[] Split(string commandBlock)
        => string.IsNullOrWhiteSpace(commandBlock)
            ? []
            : commandBlock.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static bool IsCommand(string line) => line.Length > 0 && line[0] == CommandPrefix;
}
