using AutoTripleTriadGrind.Core.Triad.Data;
using ECommons.GameHelpers;
using System.Numerics;

namespace AutoTripleTriadGrind.Core.Planning;

internal static class CollectPlanner
{
    public readonly record struct Plan(NpcAssignment[] Assignments, UnavailableCard[] Unavailable, int WantedCards)
    {
        public static readonly Plan Empty = new([], [], 0);
    }

    public static Plan Build(Configuration configuration, IReadOnlySet<int>? excludedNpcs = null)
    {
        var set = TriadData.Set;
        var wanted = WantedCards(configuration);
        var eligibility = new SkipReason[set.NpcCount];
        for (var npcIndex = 0; npcIndex < eligibility.Length; npcIndex++)
        {
            eligibility[npcIndex] = excludedNpcs is not null && excludedNpcs.Contains(npcIndex) ? SkipReason.Unreachable : NpcEligibility.Check(npcIndex, configuration);
        }

        var coverage = new int[set.NpcCount];
        for (var wantedIndex = 0; wantedIndex < wanted.Count; wantedIndex++)
        {
            var droppers = set.NpcsDropping(wanted[wantedIndex]);
            for (var dropperIndex = 0; dropperIndex < droppers.Length; dropperIndex++)
            {
                coverage[droppers[dropperIndex]]++;
            }
        }

        var cardsByNpc = new Dictionary<ushort, List<ushort>>();
        var unavailable = new List<UnavailableCard>();
        for (var wantedIndex = 0; wantedIndex < wanted.Count; wantedIndex++)
        {
            var cardId = wanted[wantedIndex];
            var chosen = PickNpc(set.NpcsDropping(cardId), eligibility, coverage, out var reason);
            if (chosen == TriadData.NoNpc)
            {
                unavailable.Add(new UnavailableCard(cardId, reason));
                continue;
            }

            if (!cardsByNpc.TryGetValue(chosen, out var cards))
            {
                cards = [];
                cardsByNpc[chosen] = cards;
            }

            cards.Add(cardId);
        }

        return new Plan(Order(cardsByNpc), unavailable.ToArray(), wanted.Count);
    }

    public static List<ushort> WantedCards(Configuration configuration)
    {
        var set = TriadData.Set;
        var wanted = new List<ushort>(configuration.SelectedCards.Count);
        var cardIds = set.CardIdsInOrder;
        for (var index = 0; index < cardIds.Length; index++)
        {
            var cardId = cardIds[index];
            if (configuration.SelectedCards.Contains(cardId) && set.IsNpcReward(cardId) && !TriadOwnership.IsOwned(cardId))
            {
                wanted.Add(cardId);
            }
        }

        return wanted;
    }

    // Prefers the NPC that also gives the most other wanted cards, so the queue visits as few NPCs as it can.
    private static ushort PickNpc(ReadOnlySpan<ushort> droppers, SkipReason[] eligibility, int[] coverage, out SkipReason reason)
    {
        var best = TriadData.NoNpc;
        reason = SkipReason.Unreachable;
        for (var index = 0; index < droppers.Length; index++)
        {
            var npcIndex = droppers[index];
            if (eligibility[npcIndex] != SkipReason.None)
            {
                reason = MoreTelling(reason, eligibility[npcIndex]);
                continue;
            }

            if (best == TriadData.NoNpc || coverage[npcIndex] > coverage[best])
            {
                best = npcIndex;
            }
        }

        return best;
    }

    private static SkipReason MoreTelling(SkipReason current, SkipReason candidate)
        => candidate == SkipReason.Locked || current == SkipReason.Unreachable ? candidate : current;

    private static NpcAssignment[] Order(Dictionary<ushort, List<ushort>> cardsByNpc)
    {
        var set = TriadData.Set;
        var remaining = new List<ushort>(cardsByNpc.Keys);
        var ordered = new NpcAssignment[remaining.Count];
        var territory = Player.Available ? Player.Territory.RowId : 0;
        var position = Player.Available ? Player.Position : Vector3.Zero;
        for (var slot = 0; slot < ordered.Length; slot++)
        {
            var pick = NearestNext(remaining, territory, position);
            var npcIndex = remaining[pick];
            remaining.RemoveAt(pick);
            ordered[slot] = new NpcAssignment(npcIndex, cardsByNpc[npcIndex].ToArray());
            territory = set.Npcs[npcIndex].TerritoryId;
            position = set.Npcs[npcIndex].Position;
        }

        return ordered;
    }

    // Same zone first, nearest by distance; otherwise the next zone in expansion and territory order, so zones are
    // visited one after another instead of teleporting back and forth.
    private static int NearestNext(List<ushort> remaining, uint territory, Vector3 position)
    {
        var npcs = TriadData.Set.Npcs;
        var best = 0;
        var bestSameZone = false;
        var bestDistance = float.MaxValue;
        for (var index = 0; index < remaining.Count; index++)
        {
            var npc = npcs[remaining[index]];
            if (npc.TerritoryId == territory)
            {
                var distance = Vector3.DistanceSquared(npc.Position, position);
                if (!bestSameZone || distance < bestDistance)
                {
                    best = index;
                    bestSameZone = true;
                    bestDistance = distance;
                }

                continue;
            }

            if (bestSameZone)
            {
                continue;
            }

            var current = npcs[remaining[best]];
            if (npc.Expansion < current.Expansion || (npc.Expansion == current.Expansion && npc.TerritoryId < current.TerritoryId))
            {
                best = index;
            }
        }

        return best;
    }
}
