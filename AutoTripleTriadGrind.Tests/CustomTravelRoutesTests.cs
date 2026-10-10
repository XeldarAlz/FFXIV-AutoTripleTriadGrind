using AutoTripleTriadGrind.Core.Travel;
using System.Numerics;

namespace AutoTripleTriadGrind.Tests;

public sealed class CustomTravelRoutesTests
{
    [Theory]
    [InlineData(3f, 0f, 0f, true)]
    [InlineData(3.01f, 0f, 0f, false)]
    [InlineData(6.5f, 0f, 0f, false)]
    [InlineData(0f, 4f, 0f, false)]
    public void InteractionRequiresActualDistanceWithinThreeMeters(float x, float y, float z, bool expected)
    {
        var step = new CustomTravelStep { ApproachMeters = 10f };
        Assert.Equal(expected, step.CanInteract(Vector3.Zero, new Vector3(x, y, z)));
    }

    [Theory]
    [InlineData(1264u)]
    [InlineData(1265u)]
    public void LowerJeunoUsesYakTelPortalForBothTerritoryIds(uint territoryId)
    {
        var configuration = new Configuration();
        Assert.True(CustomTravelRoutes.TryGet(territoryId, configuration, out var steps));
        Assert.Equal(1265u, CustomTravelRoutes.ResolveTerritoryId(territoryId));
        Assert.True(CustomTravelRoutes.IsValid(1265, steps));
        var entrance = Assert.Single(steps);
        Assert.Equal(1189u, entrance.SourceTerritoryId);
        Assert.Equal(1265u, entrance.TargetTerritoryId);
        Assert.Equal(2014450u, entrance.EntranceBaseId);
        Assert.Equal(new Vector3(-528.37415f, -152.20874f, 671.13745f), entrance.Position);
        Assert.True(entrance.ConfirmEntry);
    }

    [Theory]
    [InlineData(433u)]
    [InlineData(419u)]
    [InlineData(1189u)]
    public void UnrelatedTerritoryIdsArePreserved(uint territoryId)
        => Assert.Equal(territoryId, CustomTravelRoutes.ResolveTerritoryId(territoryId));

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
    public void GangosUsesDomanEnclaveAirshipUnlessOverridden()
    {
        var configuration = new Configuration();
        Assert.True(CustomTravelRoutes.TryGet(915, configuration, out var steps));
        Assert.True(CustomTravelRoutes.IsValid(915, steps));
        var entrance = Assert.Single(steps);
        Assert.Equal(759u, entrance.SourceTerritoryId);
        Assert.Equal(915u, entrance.TargetTerritoryId);
        Assert.Equal(1032884u, entrance.EntranceBaseId);
        Assert.Equal(162u, entrance.AethernetDestinationId);
        Assert.Equal(new Vector3(127.27539f, -4.1787133f, 98.68005f), entrance.Position);
        Assert.True(entrance.ConfirmEntry);

        CustomTravelStep[] replacement = [new() { SourceTerritoryId = 759, TargetTerritoryId = 915, EntranceBaseId = 1032884 }];
        configuration.CustomRoutes[915] = replacement;
        Assert.True(CustomTravelRoutes.TryGet(915, configuration, out var configured));
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
        step.InteractMeters = float.NaN;
        Assert.False(CustomTravelRoutes.IsValid(433, [step]));
        step.InteractMeters = 0;
        Assert.False(CustomTravelRoutes.IsValid(433, [step]));
        step.InteractMeters = 3;
        step.SourceTerritoryId = 433;
        Assert.False(CustomTravelRoutes.IsValid(433, [step]));
    }
}
