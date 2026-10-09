using AutoTripleTriadGrind.Core.Travel;
using System.Numerics;

namespace AutoTripleTriadGrind.Tests;

public sealed class CustomTravelRoutesTests
{
    [Fact]
    public void LowerJeunoUsesYakTelPortalAndNotQuestInstance()
    {
        var configuration = new Configuration();
        Assert.True(CustomTravelRoutes.TryGet(1265, configuration, out var steps));
        Assert.True(CustomTravelRoutes.IsValid(1265, steps));
        var entrance = Assert.Single(steps);
        Assert.Equal(1189u, entrance.SourceTerritoryId);
        Assert.Equal(1265u, entrance.TargetTerritoryId);
        Assert.Equal(2014450u, entrance.EntranceBaseId);
        Assert.Equal(new Vector3(-528.37415f, -152.20874f, 671.13745f), entrance.Position);
        Assert.True(entrance.ConfirmEntry);
        Assert.False(CustomTravelRoutes.TryGet(1264, configuration, out _));
    }

    [Fact]
    public void ManorUsesBuiltInRouteUnlessOverridden()
    {
        var configuration = new Configuration();
        Assert.True(CustomTravelRoutes.TryGet(433, configuration, out var builtIn));
        Assert.True(CustomTravelRoutes.IsValid(433, builtIn));
        Assert.Equal(419u, builtIn[0].SourceTerritoryId);
        Assert.Equal(1011217u, builtIn[0].EntranceBaseId);

        CustomTravelStep[] replacement = [new() { SourceTerritoryId = 418, TargetTerritoryId = 433 }];
        configuration.CustomRoutes[433] = replacement;
        Assert.True(CustomTravelRoutes.TryGet(433, configuration, out var configured));
        Assert.Same(replacement, configured);
    }

    [Fact]
    public void ContinuousMultiStepRouteIsAccepted()
    {
        CustomTravelStep[] steps =
        [
            new() { SourceTerritoryId = 418, TargetTerritoryId = 419 },
            new() { SourceTerritoryId = 419, TargetTerritoryId = 433, EntranceBaseId = 1011217 },
        ];
        Assert.True(CustomTravelRoutes.IsValid(433, steps));
        Assert.False(CustomTravelRoutes.IsValid(419, steps));
        steps[1].SourceTerritoryId = 155;
        Assert.False(CustomTravelRoutes.IsValid(433, steps));
    }

    [Fact]
    public void InvalidStepsAreRejected()
    {
        Assert.False(CustomTravelRoutes.IsValid(433, null));
        Assert.False(CustomTravelRoutes.IsValid(433, []));
        Assert.False(CustomTravelRoutes.IsValid(433, [null!]));

        var step = new CustomTravelStep { SourceTerritoryId = 419, TargetTerritoryId = 433 };
        step.Position = new Vector3(float.NaN, 0, 0);
        Assert.False(CustomTravelRoutes.IsValid(433, [step]));
        step.Position = Vector3.Zero;
        step.TimeoutMs = 0;
        Assert.False(CustomTravelRoutes.IsValid(433, [step]));
        step.TimeoutMs = 30000;
        step.MenuEntryIndex = -1;
        Assert.False(CustomTravelRoutes.IsValid(433, [step]));
        step.MenuEntryIndex = null;
        step.SourceTerritoryId = 433;
        Assert.False(CustomTravelRoutes.IsValid(433, [step]));
    }
}
