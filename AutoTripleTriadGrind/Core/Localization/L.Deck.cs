namespace AutoTripleTriadGrind.Core.Localization;

internal static partial class L
{
    internal static class Deck
    {
        public static readonly LocString Category = new("deck.cat", "Deck");
        public static readonly LocString CategorySub = new("deck.catSub", "Which deck each match is played with.");
        public static readonly LocString GroupSource = new("deck.group.source", "Deck");
        public static readonly LocString Source = new("deck.source", "Deck to play");
        public static readonly LocString SourceHelp = new("deck.sourceHelp", "An optimized deck is built for every NPC from the cards you own and that NPC's rules. Your own deck is used as it is.");
        public static readonly LocString SourceOptimized = new("deck.source.optimized", "Optimized per NPC");
        public static readonly LocString SourceOptimizedDetail = new("deck.source.optimizedDetail", "Build the strongest deck against each NPC from your collection.");
        public static readonly LocString SourceOwn = new("deck.source.own", "My own deck");
        public static readonly LocString SourceOwnDetail = new("deck.source.ownDetail", "Always play one of your saved decks.");
        public static readonly LocString OwnSlot = new("deck.ownSlot", "Saved deck");
        public static readonly LocString OwnSlotHelp = new("deck.ownSlotHelp", "The saved deck picked at the start of every match.");
        public static readonly LocString WriteSlot = new("deck.writeSlot", "Deck slot to use");
        public static readonly LocString WriteSlotHelp = new("deck.writeSlotHelp", "The optimized deck is put into this saved deck slot before each match.");
        public static readonly LocString WriteSlotWarning = new("deck.writeSlotWarning", "The cards in this slot are replaced while the plugin plays. Pick a slot you do not use.");
        public static readonly LocString SlotLabel = new("deck.slotLabel", "Deck {0}");
        public static readonly LocString GroupOptimizer = new("deck.group.optimizer", "Optimizer");
        public static readonly LocString Timeout = new("deck.timeout", "Time limit");
        public static readonly LocString TimeoutHelp = new("deck.timeoutHelp", "How long the optimizer may search for a deck. The best deck found so far is used when time runs out.");
        public static readonly LocString SecondsFormat = new("deck.secondsFormat", "%d s");
        public static readonly LocString Threads = new("deck.threads", "Worker threads");
        public static readonly LocString ThreadsHelp = new("deck.threadsHelp", "How many CPU threads the optimizer may use. Auto leaves one core free for the game.");
        public static readonly LocString ThreadsAuto = new("deck.threadsAuto", "Auto");
    }

    internal static class Play
    {
        public static readonly LocString Category = new("play.cat", "Play");
        public static readonly LocString CategorySub = new("play.catSub", "How long to stay with an NPC and when to move on.");
        public static readonly LocString GroupNpcs = new("play.group.npcs", "Matches");
        public static readonly LocString LossStreak = new("play.lossStreak", "Move on after losses in a row");
        public static readonly LocString LossStreakHelp = new("play.lossStreakHelp", "Skip an NPC after this many losses in a row and come back to it on a later run.");
        public static readonly LocString Never = new("play.never", "never");
        public static readonly LocString MatchLimit = new("play.matchLimit", "Most matches per NPC");
        public static readonly LocString MatchLimitHelp = new("play.matchLimitHelp", "Collect mode moves on after this many matches, even if a card has not dropped yet.");
        public static readonly LocString NoLimit = new("play.noLimit", "no limit");
        public static readonly LocString MaxFee = new("play.maxFee", "Highest match fee");
        public static readonly LocString MaxFeeHelp = new("play.maxFeeHelp", "NPCs asking more MGP per match than this are left out of the plan.");
        public static readonly LocString AnyFee = new("play.anyFee", "any fee");
        public static readonly LocString MgpFormat = new("play.mgpFormat", "%d MGP");
        public static readonly LocString Delay = new("play.delay", "Pause between matches");
        public static readonly LocString DelayHelp = new("play.delayHelp", "Wait this long after a match before asking for the next one.");
        public static readonly LocString MillisecondsFormat = new("play.msFormat", "%d ms");
        public static readonly LocString GroupInventory = new("play.group.inventory", "Inventory");
        public static readonly LocString FreeSlots = new("play.freeSlots", "Free bag slots to keep");
        public static readonly LocString FreeSlotsHelp = new("play.freeSlotsHelp", "Won cards land in your bags. Below this many free slots, new cards are registered first, and the run pauses if that does not free enough room.");
    }
}
