namespace AutoTripleTriadGrind.Core.Localization;

internal static partial class L
{
    internal static class Collection
    {
        public static readonly LocString Title = new("collection.title", "NPC cards");
        public static readonly LocString Loading = new("collection.loading", "Reading the Triple Triad tables…");
        public static readonly LocString ViewByNpc = new("collection.view.byNpc", "By NPC");
        public static readonly LocString ViewByCard = new("collection.view.byCard", "By card");
        public static readonly LocString FilterMissing = new("collection.filter.missing", "Missing");
        public static readonly LocString FilterAll = new("collection.filter.all", "All");
        public static readonly LocString FilterOwned = new("collection.filter.owned", "Owned");
        public static readonly LocString SearchHint = new("collection.searchHint", "Search NPCs, zones, or cards");
        public static readonly LocString Summary = new("collection.summary", "{0} picked  ·  {1} of {2} NPC cards owned");
        public static readonly LocString SelectAllMissing = new("collection.selectAllMissing", "Pick every missing card");
        public static readonly LocString NothingHere = new("collection.nothingHere", "Nothing to show with this filter.");
        public static readonly LocString TooltipFee = new("collection.tooltip.fee", "Match fee: {0} MGP");
        public static readonly LocString TooltipRules = new("collection.tooltip.rules", "Rules: {0}");
        public static readonly LocString TooltipRegional = new("collection.tooltip.regional", "Also plays the regional rules of its area.");
        public static readonly LocString TooltipLockedQuest = new("collection.tooltip.lockedQuest", "Locked: complete “{0}” to unlock this NPC.");
        public static readonly LocString TooltipSelectNpc = new("collection.tooltip.selectNpc", "Click to pick or drop every card you are missing from this NPC.");
        public static readonly LocString TooltipOwned = new("collection.tooltip.owned", "Owned");
        public static readonly LocString TooltipSelected = new("collection.tooltip.selected", "Picked. Click to drop it from the plan.");
        public static readonly LocString TooltipMissing = new("collection.tooltip.missing", "Missing. Click to pick it.");
        public static readonly LocString TooltipDroppedBy = new("collection.tooltip.droppedBy", "{0}  ·  {1}");
    }

    internal static class Farm
    {
        public static readonly LocString Title = new("farm.title", "NPCs to farm");
        public static readonly LocString StopLabel = new("farm.stopLabel", "Play each NPC");
        public static readonly LocString StopAfterMatches = new("farm.stop.matches", "A set number of times");
        public static readonly LocString StopNever = new("farm.stop.never", "Until I stop");
        public static readonly LocString MatchesFormat = new("farm.matchesFormat", "{0} matches");
    }
}
