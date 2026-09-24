using AutoTripleTriadGrind.Core.Changelog;
using Dalamud.Configuration;
using ECommons.Throttlers;
using Newtonsoft.Json;

namespace AutoTripleTriadGrind;

[Serializable]
public sealed partial class Configuration : IPluginConfiguration
{
    [JsonIgnore]
    private bool savePending;

    public int Version { get; set; }

    public bool AutoShowOnLogin { get; set; } = false;

    public string Language { get; set; } = "";

    public TriadRunMode RunMode { get; set; } = TriadRunMode.Collect;

    // Never fires on a manual Stop or a fault.
    public AfterRunAction AfterRun { get; set; } = AfterRunAction.StayLoggedIn;

    public bool AutoPauseInContent { get; set; } = true;

    public string LastSeenChangelogVersion { get; set; } = string.Empty;

    [JsonIgnore]
    public bool HasUnseenChangelog => !string.Equals(LastSeenChangelogVersion, ChangelogData.LatestVersion, StringComparison.Ordinal);

    public void MarkChangelogSeen()
    {
        if (!HasUnseenChangelog)
        {
            return;
        }

        LastSeenChangelogVersion = ChangelogData.LatestVersion;
        Save();
    }

    public void Save()
    {
        savePending = false;
        Plugin.PluginInterface.SavePluginConfig(this);
    }

    // A slider or a text box changes on every frame it is edited, so the throttle drops most calls; the change stays
    // pending, and FlushPendingSave writes the last one once the throttle window has passed.
    public void SaveDebounced()
    {
        savePending = true;
        FlushPendingSave();
    }

    public void FlushPendingSave()
    {
        if (savePending && EzThrottler.Throttle(Core.AttgConstants.ThrottleKeys.Save, Core.AttgConstants.SaveThrottleMs))
        {
            Save();
        }
    }

    public void SaveIfPending()
    {
        if (savePending)
        {
            Save();
        }
    }
}
