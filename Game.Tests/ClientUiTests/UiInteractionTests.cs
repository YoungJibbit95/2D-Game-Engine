using Game.Client.UI;
using Game.Core.Settings;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientUiTests;

public sealed class UiInteractionTests
{
    [Fact]
    public void PointerRouter_ClicksOnlyAfterReleaseInsideCapturedTarget()
    {
        var router = new UiPointerRouter();
        var regions = new[] { new UiHitRegion(7, new Rectangle(10, 10, 80, 30)) };

        router.Update(new Point(20, 20), isDown: false, regions);
        router.Update(new Point(20, 20), isDown: true, regions);

        Assert.Equal(7, router.HoveredId);
        Assert.Equal(7, router.PressedId);
        Assert.Equal(-1, router.ClickedId);
        Assert.True(router.IsPressed(7));

        router.Update(new Point(24, 20), isDown: false, regions);

        Assert.Equal(7, router.ClickedId);
        Assert.Equal(-1, router.PressedId);
    }

    [Fact]
    public void PointerRouter_CancelsReleaseOutsideAndNeverCapturesDisabledTarget()
    {
        var router = new UiPointerRouter();
        var enabled = new[] { new UiHitRegion(2, new Rectangle(0, 0, 40, 20)) };

        router.Update(new Point(10, 10), isDown: true, enabled);
        router.Update(new Point(60, 10), isDown: false, enabled);

        Assert.Equal(-1, router.ClickedId);

        var disabled = new[] { new UiHitRegion(3, new Rectangle(0, 0, 40, 20), IsEnabled: false) };
        router.Update(new Point(10, 10), isDown: true, disabled);

        Assert.Equal(3, router.HoveredId);
        Assert.Equal(-1, router.PressedId);
    }

    [Fact]
    public void PointerRouter_PrefersLastRegionForDropdownStyleLayering()
    {
        var router = new UiPointerRouter();
        var regions = new[]
        {
            new UiHitRegion(1, new Rectangle(0, 0, 100, 40)),
            new UiHitRegion(9, new Rectangle(20, 0, 80, 40))
        };

        router.Update(new Point(30, 10), isDown: false, regions);

        Assert.Equal(9, router.HoveredId);
    }

    [Fact]
    public void GamepadNavigator_UsesEdgesAndRepeatsHeldDirections()
    {
        var navigator = new UiGamepadNavigator();
        var down = new UiGamepadSnapshot(false, true, false, false, false, false, false, false);

        navigator.Update(down, 0.016);
        Assert.True(navigator.DownPressed);

        navigator.Update(down, 0.20);
        Assert.False(navigator.DownPressed);

        navigator.Update(down, 0.20);
        Assert.True(navigator.DownPressed);

        navigator.Update(default, 0.016);
        navigator.Update(down, 0.016);
        Assert.True(navigator.DownPressed);
    }

    [Fact]
    public void GamepadNavigator_ConfirmAndCancelAreSinglePressActions()
    {
        var navigator = new UiGamepadNavigator();
        var confirm = new UiGamepadSnapshot(false, false, false, false, true, false, false, false);
        var cancel = new UiGamepadSnapshot(false, false, false, false, false, true, false, false);

        navigator.Update(confirm, 0.016);
        Assert.True(navigator.ConfirmPressed);

        navigator.Update(confirm, 0.016);
        Assert.False(navigator.ConfirmPressed);

        navigator.Update(cancel, 0.016);
        Assert.True(navigator.CancelPressed);
    }

    [Fact]
    public void ScrollModel_ClampsWheelMovementAndKeepsFocusVisible()
    {
        var scroll = new UiScrollModel();
        scroll.Configure(itemCount: 12, viewCapacity: 4);

        scroll.ScrollBy(99);
        Assert.Equal(8, scroll.Offset);

        scroll.EnsureVisible(2);
        Assert.Equal(2, scroll.Offset);

        scroll.EnsureVisible(7);
        Assert.Equal(4, scroll.Offset);
    }

    [Fact]
    public void ThemeContract_ProvidesCompactPixelSurfaceAndBackdropTokens()
    {
        var contract = UiTheme.Contract;

        Assert.InRange(contract.Button.CornerRadius, 4, 8);
        Assert.InRange(contract.Panel.CornerRadius, 4, 8);
        Assert.InRange(contract.Tooltip.CornerRadius, 4, 8);
        Assert.True(contract.Backdrop.BlurRadius > 0);
        Assert.True(contract.Panel.GradientSteps >= 4);
        Assert.True(contract.Typography.TitleScale > contract.Typography.CaptionScale);
        Assert.True(contract.Spacing.Md <= contract.Spacing.Xl);
    }

    [Fact]
    public void ThemeContract_BindsPresentationQualityAndUiSettings()
    {
        var settings = GameSettings.CreateDefault() with
        {
            Rendering = new RenderingSettings { UiEffectQuality = 3, BlurRadiusPixels = 20 },
            Ui = new UiSettings
            {
                CornerRadiusPixels = 12,
                BackdropBlurStrength = 0.5f,
                GlowStrength = 0.75f,
                TextScale = 2f,
                HighContrastPanels = true,
                LargeCursor = true
            },
            Accessibility = new AccessibilitySettings
            {
                HighContrastInteractionOutline = true,
                InterfaceContrast = 1.4f
            }
        };

        var contract = UiTheme.ResolveContract(settings);

        Assert.Equal(12, contract.Panel.CornerRadius);
        Assert.Equal(8, contract.Button.CornerRadius);
        Assert.Equal(10, contract.Backdrop.BlurRadius);
        Assert.True(contract.Panel.GlowOpacity > 0f);
        Assert.True(contract.Panel.BorderThickness > UiTheme.Contract.Panel.BorderThickness);
        Assert.Equal(4, contract.Typography.TitleScale);
        Assert.Equal(2, contract.Typography.CaptionScale);
    }

    [Fact]
    public void ThemeContract_DisablesCostlyEffectsAtOffQuality()
    {
        var settings = GameSettings.CreateDefault() with
        {
            Rendering = new RenderingSettings { UiEffectQuality = 0, BlurRadiusPixels = 24 },
            Ui = new UiSettings { BackdropBlurStrength = 1f, GlowStrength = 1f }
        };

        var contract = UiTheme.ResolveContract(settings);

        Assert.Equal(0, contract.Backdrop.BlurRadius);
        Assert.Equal(0, contract.Panel.GlowSpread);
        Assert.Equal(0f, contract.Panel.GlowOpacity);
        Assert.Equal(1, contract.Panel.GradientSteps);
    }
}
