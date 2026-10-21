using System.Threading.Tasks;

namespace AutoTripleTriadGrind.Core.Triad.Decks;

// A build keeps serving its NPC and rules after it finishes, until its deck is collected; only a failed or cancelled
// build has to start over.
internal static class DeckBuildReuse
{
    public static bool Serves(Task? build, uint builtForNpcRow, ushort builtForRules, uint npcRow, ushort ruleMask)
        => build is { IsFaulted: false, IsCanceled: false } && builtForNpcRow == npcRow && builtForRules == ruleMask;
}
