using Game.Core.Combat;
using Game.Core.Data;
using Xunit;

namespace Game.Tests.DataTests;

public sealed class CombatMobilityRepositoryContentTests
{
    [Fact]
    public void RepositoryData_ResolvesMobilityManaAndMagicStatusContracts()
    {
        var result = new GameContentLoader().LoadWithMods(
            FindRepositoryGameData(),
            modsRoot: null);

        Assert.False(
            result.Report.HasErrors,
            string.Join(
                Environment.NewLine,
                result.Report.Issues.Select(issue => issue.Message)));

        var boots = result.Database.Items.GetById("double_jump_boots");
        var wings = result.Database.Items.GetById("skyward_wings");
        var glider = result.Database.Items.GetById("ether_glider");
        Assert.NotNull(boots.Mobility);
        Assert.Equal(1, boots.Mobility!.ExtraJumpCount);
        Assert.NotNull(wings.Mobility);
        Assert.Equal(2.4f, wings.Mobility!.FlightDurationSeconds);
        Assert.NotNull(glider.Mobility);
        Assert.True(glider.Mobility!.GlideEnabled);

        var sparkAction = result.Database.Items.GetById("spark_wand").Actions[0];
        Assert.Equal(0.95f, sparkAction.ManaRegenerationDelaySeconds);
        Assert.Equal(ManaRefundPolicy.BeforeEffect, sparkAction.ManaRefundPolicy);

        var glimmer = result.Database.Items.GetById("glimmer_rod");
        Assert.Contains(
            glimmer.OnHitEffects,
            effect => string.Equals(
                effect.EffectId,
                "arcane_chill",
                StringComparison.OrdinalIgnoreCase));
        Assert.True(result.Database.StatusEffects.TryGetById("arcane_chill", out _));
    }

    private static string FindRepositoryGameData()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Game.Data");
            if (File.Exists(Path.Combine(directory.FullName, "YjsE.sln")) &&
                Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository Game.Data directory.");
    }
}