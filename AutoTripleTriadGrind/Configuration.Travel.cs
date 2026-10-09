using AutoTripleTriadGrind.Core.Travel;

namespace AutoTripleTriadGrind;

public sealed partial class Configuration
{
    // Keyed by destination territory; configured routes override built-in routes.
    public Dictionary<uint, CustomTravelStep[]> CustomRoutes { get; set; } = [];
}
