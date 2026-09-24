namespace AutoTripleTriadGrind;

public sealed partial class Configuration
{
    public DeckSource DeckSource { get; set; } = DeckSource.Optimized;
    // Zero-based profile deck slot; the optimized deck is written over this slot.
    public int OptimizedDeckSlot { get; set; } = 4;
    public int OwnDeckSlot { get; set; } = 0;
    public int OptimizerTimeoutSeconds { get; set; } = 60;
    // Zero lets the optimizer use every core but one.
    public int OptimizerThreads { get; set; } = 0;

    public int LossStreakSkip { get; set; } = 5;
    // Zero plays an NPC for as long as it still has a card to give.
    public int MaxMatchesPerNpc { get; set; } = 0;
    // Zero allows any fee.
    public int MaxMatchFee { get; set; } = 0;
    public int MinFreeBagSlots { get; set; } = 2;
    public int DelayBetweenMatchesMs { get; set; } = 0;
}
