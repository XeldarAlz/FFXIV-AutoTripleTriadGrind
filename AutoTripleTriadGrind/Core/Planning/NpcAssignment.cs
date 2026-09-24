namespace AutoTripleTriadGrind.Core.Planning;

internal readonly record struct NpcAssignment(ushort NpcIndex, ushort[] Cards);

internal readonly record struct UnavailableCard(ushort CardId, SkipReason Reason);
