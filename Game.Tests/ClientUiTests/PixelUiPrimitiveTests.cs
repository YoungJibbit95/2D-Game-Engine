using Game.Client.UI;
using Game.Core.Settings;
using Microsoft.Xna.Framework;
using Xunit;

namespace Game.Tests.ClientUiTests;

public sealed class PixelUiPrimitiveTests
{
    [Fact]
    public void Geometry_ProducesContainedNonOverlappingControlHitboxes()
    {
        var tokens = UiTheme.Contract.Controls;
        var bounds = new Rectangle(100, 40, 280, 28);

        var toggle = PixelUiGeometry.Toggle(bounds, tokens);
        var slider = PixelUiGeometry.Slider(bounds, 0.5f, tokens);
        var stepper = PixelUiGeometry.Stepper(bounds, tokens);

        AssertContained(bounds, toggle.Track);
        AssertContained(bounds, toggle.HitBounds);
        AssertContained(bounds, slider.HitBounds);
        AssertContained(bounds, slider.Thumb);
        AssertContained(bounds, stepper.Decrement);
        AssertContained(bounds, stepper.Value);
        AssertContained(bounds, stepper.Increment);
        Assert.True(stepper.Decrement.Right < stepper.Value.X);
        Assert.True(stepper.Value.Right < stepper.Increment.X);
    }

    [Fact]
    public void Segments_PartitionCompactBoundsWithStablePixelGaps()
    {
        var tokens = UiTheme.Contract.Controls;
        var bounds = new Rectangle(7, 11, 221, tokens.CompactHeight);
        var first = PixelUiGeometry.Segment(bounds, 0, 3, tokens);
        var middle = PixelUiGeometry.Segment(bounds, 1, 3, tokens);
        var last = PixelUiGeometry.Segment(bounds, 2, 3, tokens);

        Assert.Equal(bounds.X, first.X);
        Assert.Equal(tokens.Gap, middle.X - first.Right);
        Assert.Equal(tokens.Gap, last.X - middle.Right);
        Assert.Equal(bounds.Right, last.Right);
        Assert.Equal(Rectangle.Empty, PixelUiGeometry.Segment(bounds, 3, 3, tokens));
    }

    [Fact]
    public void ToggleGeometry_InterpolatesKnobWithoutLeavingTrack()
    {
        var geometry = PixelUiGeometry.Toggle(new Rectangle(0, 0, 160, 28), UiTheme.Contract.Controls);

        Assert.Equal(geometry.KnobOff, geometry.KnobAt(-1f));
        Assert.Equal(geometry.KnobOn, geometry.KnobAt(2f));
        var midpoint = geometry.KnobAt(0.5f);
        Assert.InRange(midpoint.X, geometry.KnobOff.X, geometry.KnobOn.X);
        AssertContained(geometry.Track, midpoint);
    }

    [Fact]
    public void SliderAndToggleInteraction_ClampEndpointsAndRespectActivation()
    {
        var hitbox = new Rectangle(30, 4, 101, 24);

        Assert.Equal(0f, PixelUiInteraction.SliderNormalizedAt(hitbox, -100));
        Assert.Equal(0.5f, PixelUiInteraction.SliderNormalizedAt(hitbox, 80), 4);
        Assert.Equal(1f, PixelUiInteraction.SliderNormalizedAt(hitbox, hitbox.Right - 1));
        Assert.True(PixelUiInteraction.ResolveToggle(false, activated: true));
        Assert.True(PixelUiInteraction.ResolveToggle(true, activated: true, enabled: false));
        Assert.False(PixelUiInteraction.ResolveToggle(false, activated: false));
    }

    [Fact]
    public void MotionState_IsDeterministicAcrossEquivalentTimeSlices()
    {
        var state = new PixelUiState(Hovered: true, Pressed: true, Focused: true, Selected: true);
        var singleStep = new PixelUiMotionState();
        var sliced = new PixelUiMotionState();

        singleStep.Update(state, 0.09f);
        for (var index = 0; index < 9; index++)
        {
            sliced.Update(state, 0.01f);
        }

        Assert.Equal(singleStep.Hover, sliced.Hover, 4);
        Assert.Equal(singleStep.Press, sliced.Press, 4);
        Assert.Equal(singleStep.Focus, sliced.Focus, 4);
        Assert.Equal(singleStep.Selection, sliced.Selection, 4);
        Assert.Equal(singleStep.Emphasis, sliced.Emphasis, 4);
    }

    [Fact]
    public void MotionState_ReducedMotionSnapsAndDisabledStateClearsChannels()
    {
        var motion = new PixelUiMotionState();
        motion.Update(new PixelUiState(Hovered: true, Focused: true, Selected: true), 0f, reducedMotion: true);

        Assert.Equal(1f, motion.Hover);
        Assert.Equal(1f, motion.Focus);
        Assert.Equal(1f, motion.Selection);

        motion.Update(new PixelUiState(Hovered: true, Focused: true, Selected: true, Enabled: false), 0f, reducedMotion: true);
        Assert.Equal(0f, motion.Hover);
        Assert.Equal(0f, motion.Focus);
        Assert.Equal(0f, motion.Selection);
    }

    [Theory]
    [InlineData("Midnight")]
    [InlineData("Forest")]
    [InlineData("Ember")]
    public void Theme_TextMaintainsStrongContrastAgainstPrimarySurfaces(string theme)
    {
        var settings = GameSettings.CreateDefault() with { Ui = new UiSettings { Theme = theme } };
        var palette = UiTheme.Resolve(settings);

        Assert.True(UiTheme.ContrastRatio(palette.Text, palette.Surface) >= 7f);
        Assert.True(UiTheme.ContrastRatio(palette.Text, palette.SurfaceRaised) >= 4.5f);
    }

    [Fact]
    public void Theme_ControlTokensScaleLargePointerGeometryWithoutBreakingCompactLayout()
    {
        var defaults = UiTheme.Contract.Controls;
        var settings = GameSettings.CreateDefault() with
        {
            Ui = new UiSettings { LargeCursor = true, TextScale = 1.25f },
            Accessibility = new AccessibilitySettings { HighContrastInteractionOutline = true }
        };
        var accessible = UiTheme.ResolveContract(settings).Controls;

        Assert.True(accessible.ToggleWidth > defaults.ToggleWidth);
        Assert.True(accessible.SliderThumbWidth > defaults.SliderThumbWidth);
        Assert.True(accessible.FocusRingOffset > defaults.FocusRingOffset);
        Assert.InRange(accessible.CompactHeight, 21, accessible.MinimumHeight);
    }

    [Theory]
    [InlineData(PresentationCadenceUi.Eco, 30, 15, 15, 20)]
    [InlineData(PresentationCadenceUi.Balanced, 45, 24, 24, 30)]
    [InlineData(PresentationCadenceUi.Fast, 120, 60, 60, 60)]
    public void CadencePresets_ApplyCoordinatedRatesAndPreserveAdaptiveChoice(
        string preset,
        int lighting,
        int reflections,
        int atmosphere,
        int sceneCapture)
    {
        var source = new RenderingSettings { AdaptivePresentationCadence = false, BloomStrength = 0.27f };

        var result = PresentationCadenceUi.ApplyPreset(source, preset);

        Assert.Equal(preset, PresentationCadenceUi.ResolvePreset(result));
        Assert.Equal(lighting, result.LightingUpdateRateHz);
        Assert.Equal(reflections, result.ReflectionUpdateRateHz);
        Assert.Equal(atmosphere, result.AtmosphereUpdateRateHz);
        Assert.Equal(sceneCapture, result.SceneCaptureUpdateRateHz);
        Assert.False(result.AdaptivePresentationCadence);
        Assert.Equal(0.27f, result.BloomStrength);
    }

    [Fact]
    public void CadencePreset_ReportsCustomAfterIndividualFineTuning()
    {
        var settings = PresentationCadenceUi.ApplyPreset(new RenderingSettings(), PresentationCadenceUi.Balanced) with
        {
            ReflectionUpdateRateHz = 35
        };

        Assert.Equal(PresentationCadenceUi.Custom, PresentationCadenceUi.ResolvePreset(settings));
        Assert.Same(settings, PresentationCadenceUi.ApplyPreset(settings, PresentationCadenceUi.Custom));
    }

    private static void AssertContained(Rectangle outer, Rectangle inner)
    {
        Assert.True(
            inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom,
            $"Expected {inner} to be contained by {outer}.");
    }
}
