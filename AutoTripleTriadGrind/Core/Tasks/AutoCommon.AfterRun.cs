using Dalamud.Game.ClientState.Conditions;
using ECommons.Automation;
using ECommons.DalamudServices;
using System.Threading.Tasks;

namespace AutoTripleTriadGrind.Core.Tasks;

public abstract partial class AutoCommon
{
    protected const int AfterRunReadyWaitMs = 20_000;
    // Some plugin commands misbehave when several arrive in the same frame, so the player's commands are spaced out.
    private const int PostRunCommandSpacingMs = 500;

    protected Task<bool> WaitUntilSafeToFinish(string scope) => WaitUntilTimed(IsSafeToFinish, AfterRunReadyWaitMs, scope);

    // Each command is awaited in turn so a logout or a closed game that follows cannot cut the last ones off.
    protected async Task SendPostRunCommands(string commandBlock)
    {
        var lines = PostRunCommandLines.Split(commandBlock);
        if (lines.Length == 0)
        {
            return;
        }

        Status = "Sending your chat commands";
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            if (CancelToken.IsCancellationRequested)
            {
                return;
            }

            var command = lines[lineIndex];
            if (!PostRunCommandLines.IsCommand(command))
            {
                Warn($"Post-run line skipped, commands must start with '/': {command}");
                continue;
            }

            SendPostRunCommand(command);
            await DelayMs(PostRunCommandSpacingMs);
        }
    }

    private void SendPostRunCommand(string command)
    {
        try
        {
            Diag($"Post-run command: {command}");
            Chat.ExecuteCommand(command);
        }
        catch (Exception exception)
        {
            RunLog.Error($"Post-run command '{command}' failed: {exception.Message}");
        }
    }

    private static bool IsSafeToFinish()
        => Svc.Objects.LocalPlayer is not null
        && !Svc.Condition[ConditionFlag.InCombat]
        && !Svc.Condition[ConditionFlag.BetweenAreas]
        && !Svc.Condition[ConditionFlag.Casting];
}
