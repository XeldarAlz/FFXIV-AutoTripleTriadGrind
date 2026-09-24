namespace AutoTripleTriadGrind;

public sealed partial class Configuration
{
    public bool DeclinePartyInvites { get; set; } = false;
    public int DeclineInviteDelayMinSeconds { get; set; } = 2;
    public int DeclineInviteDelayMaxSeconds { get; set; } = 6;
    public bool DeclineInviteReply { get; set; } = false;
    public PartyInviteReplyChannel DeclineInviteReplyChannel { get; set; } = PartyInviteReplyChannel.Tell;
    public string DeclineInviteReplyMessage { get; set; } = "";

    public bool GmAlertStopRun { get; set; } = true;
    public bool GmAlertToast { get; set; } = false;
    public bool GmAlertChat { get; set; } = false;
    public bool GmAlertSound { get; set; } = false;
    public int GmAlertBeepCount { get; set; } = 3;
    public int GmAlertBeepDurationMs { get; set; } = 250;
    public int GmAlertBeepFrequencyHz { get; set; } = 900;
    public bool GmAlertKillGame { get; set; } = false;
    public List<string> GmAlertCommands { get; set; } = [];
}
