namespace AutoTripleTriadGrind.Core.Triad.Data;

public readonly struct TriadCard(ushort id, byte up, byte right, byte down, byte left, TriadCardType type, byte stars, ushort order, byte group)
{
    public const int SmallIconBase = 88000;
    public const int LargeIconBase = 87000;

    public readonly ushort Id = id;
    public readonly byte Up = up;
    public readonly byte Right = right;
    public readonly byte Down = down;
    public readonly byte Left = left;
    public readonly TriadCardType Type = type;
    public readonly byte Stars = stars;
    public readonly ushort Order = order;
    public readonly byte Group = group;

    public bool IsValid => Id != 0;

    public uint SmallIconId => (uint)(SmallIconBase + Id);

    public uint LargeIconId => (uint)(LargeIconBase + Id);

    public byte Side(int side) => side switch
    {
        TriadSide.Up    => Up,
        TriadSide.Right => Right,
        TriadSide.Down  => Down,
        _               => Left,
    };

    public int SideSum => Up + Right + Down + Left;
}

// Sides are indexed clockwise from the top, so the side facing a neighbour is always (side + 2) % 4.
public static class TriadSide
{
    public const int Up = 0;
    public const int Right = 1;
    public const int Down = 2;
    public const int Left = 3;
    public const int Count = 4;

    public static int Opposite(int side) => (side + 2) & 3;
}
