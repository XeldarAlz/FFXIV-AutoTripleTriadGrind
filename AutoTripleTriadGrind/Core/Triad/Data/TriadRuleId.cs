namespace AutoTripleTriadGrind.Core.Triad.Data;

// Values are TripleTriadRule row ids, so the sheet maps straight onto the enum.
public enum TriadRuleId : byte
{
    None,
    Roulette,
    AllOpen,
    ThreeOpen,
    Same,
    SuddenDeath,
    Plus,
    Random,
    Order,
    Chaos,
    Reverse,
    FallenAce,
    Ascension,
    Descension,
    Swap,
    Draft,
}

public static class TriadRuleIds
{
    public const int Count = 16;

    public static bool Has(ushort ruleMask, TriadRuleId rule) => (ruleMask & (1 << (int)rule)) != 0;

    public static ushort With(ushort ruleMask, TriadRuleId rule) => (ushort)(ruleMask | (1 << (int)rule));
}
