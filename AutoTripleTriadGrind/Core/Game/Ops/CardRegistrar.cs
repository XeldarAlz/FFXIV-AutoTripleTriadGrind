using AutoTripleTriadGrind.Core.Triad.Data;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AutoTripleTriadGrind.Core.Game.Ops;

// Won cards arrive as items in the bags and only count as owned once used.
internal static unsafe class CardRegistrar
{
    private static readonly InventoryType[] bags = [InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4];

    public static int FreeBagSlots()
    {
        var manager = InventoryManager.Instance();
        return manager is null ? 0 : (int)manager->GetEmptySlotsInBag();
    }

    public static int Mgp()
    {
        var manager = InventoryManager.Instance();
        return manager is null ? 0 : (int)manager->GetGoldSaucerCoin();
    }

    // The first card item in the bags whose card is not registered yet, or 0.
    public static uint FindUnregisteredCardItem(IReadOnlySet<uint> skipped)
    {
        var manager = InventoryManager.Instance();
        if (manager is null)
        {
            return 0;
        }

        var byItem = TriadData.Set.CardIdByItemId;
        for (var bagIndex = 0; bagIndex < bags.Length; bagIndex++)
        {
            var container = manager->GetInventoryContainer(bags[bagIndex]);
            if (container is null)
            {
                continue;
            }

            for (var slot = 0; slot < container->Size; slot++)
            {
                var item = container->GetInventorySlot(slot);
                if (item is null || item->ItemId == 0)
                {
                    continue;
                }

                if (byItem.TryGetValue(item->ItemId, out var cardId) && !TriadOwnership.IsOwned(cardId) && !skipped.Contains(item->ItemId))
                {
                    return item->ItemId;
                }
            }
        }

        return 0;
    }

    public static bool Use(uint itemId)
    {
        var context = AgentInventoryContext.Instance();
        if (context is null)
        {
            return false;
        }

        context->UseItem(itemId);
        return true;
    }
}
