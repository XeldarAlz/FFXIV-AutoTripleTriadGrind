namespace AutoTripleTriadGrind.Core.Planning;

public enum SkipReason : byte
{
    None,
    Locked,
    Unreachable,
    BattleHall,
    FeeTooHigh,
    NotEnoughMgp,
    InteractFailed,
    LossStreak,
    MatchLimit,
    InventoryFull,
}
