namespace AutoTripleTriadGrind.Core.Triad.Logic;

public sealed partial class TriadDeckOptimizer
{
    private readonly struct Candidate(ushort[] cards, int seed)
    {
        public readonly ushort[] Cards = cards;
        public readonly int Seed = seed;

        public bool IsValid
        {
            get
            {
                for (var first = 0; first < Cards.Length; first++)
                {
                    for (var second = first + 1; second < Cards.Length; second++)
                    {
                        if (Cards[first] == Cards[second])
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
        }
    }

    // Walks every deck the pool allows. Common slots pick increasing indices, so the same set of common cards is
    // never visited twice in a different order.
    private sealed class SlotIterator
    {
        private readonly ushort[][] slotLists = new ushort[TriadDeck.HandSize][];
        private readonly bool[] slotIsCommon = new bool[TriadDeck.HandSize];

        public SlotIterator(CardPool pool)
        {
            for (var slot = 0; slot < TriadDeck.HandSize; slot++)
            {
                var slotType = pool.SlotTypes[slot];
                slotIsCommon[slot] = slotType == DeckSlotCommon;
                slotLists[slot] = slotType == DeckSlotCommon ? pool.CommonList : pool.PriorityLists[slotType];
            }
        }

        public IEnumerable<Candidate> Decks()
        {
            var lists = slotLists;
            for (var first = 0; first < lists[0].Length; first++)
            {
                for (var second = LoopStart(1, first, -1, -1, -1); second < lists[1].Length; second++)
                {
                    for (var third = LoopStart(2, first, second, -1, -1); third < lists[2].Length; third++)
                    {
                        for (var fourth = LoopStart(3, first, second, third, -1); fourth < lists[3].Length; fourth++)
                        {
                            for (var fifth = LoopStart(4, first, second, third, fourth); fifth < lists[4].Length; fifth++)
                            {
                                ushort[] cards = [lists[0][first], lists[1][second], lists[2][third], lists[3][fourth], lists[4][fifth]];
                                yield return new Candidate(cards, Seed(first, second, third, fourth, fifth));
                            }
                        }
                    }
                }
            }
        }

        private int LoopStart(int slot, int first, int second, int third, int fourth)
        {
            if (!slotIsCommon[slot])
            {
                return 0;
            }

            if (slot >= 4 && slotIsCommon[3])
            {
                return fourth + 1;
            }

            if (slot >= 3 && slotIsCommon[2])
            {
                return third + 1;
            }

            if (slot >= 2 && slotIsCommon[1])
            {
                return second + 1;
            }

            return slot >= 1 && slotIsCommon[0] ? first + 1 : 0;
        }

        private static int Seed(int first, int second, int third, int fourth, int fifth)
        {
            var hash = 13;
            hash = hash * 37 + first;
            hash = hash * 37 + second;
            hash = hash * 37 + third;
            hash = hash * 37 + fourth;
            return hash * 37 + fifth;
        }
    }
}
