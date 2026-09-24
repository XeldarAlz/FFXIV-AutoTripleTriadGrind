using AutoTripleTriadGrind.Core.Ipc;
using AutoTripleTriadGrind.Core.External;
using AutoTripleTriadGrind.Core.Travel;
using ECommons.DalamudServices;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;

namespace AutoTripleTriadGrind.Core.Tasks;

internal sealed class AutoGoto(uint territoryId, Vector3 destination) : AutoCommon
{
    private const float ArriveWithinMeters = 3f;
    private const string StopArgument = "stop";
    private const string Usage = AttgConstants.LogPrefix + " Usage: /attg goto <territoryId> <x> <y> <z>, or /attg goto stop.";

    private static readonly char[] ArgumentSeparators = [' ', ','];

    public static void HandleCommand(string arguments, bool runActive)
    {
        var automation = clib.Services.Svc.Automation;
        if (arguments.Equals(StopArgument, StringComparison.OrdinalIgnoreCase))
        {
            if (automation.CurrentTask is not AutoGoto)
            {
                Svc.Chat.Print($"{AttgConstants.LogPrefix} No goto is running.");
                return;
            }

            automation.Stop();
            Svc.Chat.Print($"{AttgConstants.LogPrefix} Goto stopped.");
            return;
        }

        if (!TryParse(arguments, out var territoryId, out var destination))
        {
            Svc.Chat.PrintError(Usage);
            return;
        }

        if (automation.CurrentTask is AutoGoto)
        {
            Svc.Chat.PrintError($"{AttgConstants.LogPrefix} A goto is already running. /attg goto stop cancels it.");
            return;
        }

        if (runActive)
        {
            Svc.Chat.PrintError($"{AttgConstants.LogPrefix} Stop the run before using goto.");
            return;
        }

        if (!ExternalPlugins.IsInstalled(ExternalPlugin.Vnavmesh))
        {
            Svc.Chat.PrintError($"{AttgConstants.LogPrefix} Goto needs the pathfinding plugin listed on the Plugins page.");
            return;
        }

        Svc.Chat.Print($"{AttgConstants.LogPrefix} Goto: heading to {TerritoryNames.Of(territoryId)} ({territoryId}) at {destination.X:F1}, {destination.Y:F1}, {destination.Z:F1}.");
        automation.Start(new AutoGoto(territoryId, destination));
    }

    protected override async Task Execute()
    {
        var zoneName = TerritoryNames.Of(territoryId);
        Diag($"Goto: travelling to {zoneName} ({territoryId}) at {destination}.");
        try
        {
            var arrived = await TravelTo(territoryId, destination, ArriveWithinMeters);
            if (CancelToken.IsCancellationRequested)
            {
                Diag("Goto: cancelled.");
                return;
            }

            if (!arrived)
            {
                Svc.Chat.PrintError($"{AttgConstants.LogPrefix} Goto: could not reach the spot in {zoneName}. The log has the details.");
                return;
            }

            Status = "Arrived";
            Svc.Chat.Print($"{AttgConstants.LogPrefix} Goto: arrived in {zoneName}.");
        }
        finally
        {
            NavmeshIPC.Instance.Stop();
        }
    }

    private static bool TryParse(string arguments, out uint territoryId, out Vector3 destination)
    {
        territoryId = 0;
        destination = default;
        var parts = arguments.Split(ArgumentSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
        {
            return false;
        }

        if (!uint.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out territoryId))
        {
            return false;
        }

        if (!TryParseCoordinate(parts[1], out var x) || !TryParseCoordinate(parts[2], out var y) || !TryParseCoordinate(parts[3], out var z))
        {
            return false;
        }

        destination = new Vector3(x, y, z);
        return true;
    }

    private static bool TryParseCoordinate(string text, out float value)
        => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && float.IsFinite(value);
}
