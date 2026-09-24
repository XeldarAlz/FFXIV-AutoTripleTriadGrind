using AutoTripleTriadGrind.Core.Localization;

namespace AutoTripleTriadGrind.Core.Changelog;

internal readonly record struct ChangelogEntry(string Version, string Date, LocString[] Highlights);
