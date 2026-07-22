namespace Game.Core.Settings;

public sealed class GameSettingsValidator
{
    public SettingsValidationResult Validate(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var result = new SettingsValidationResult();
        ValidateVideo(settings.Video, result);
        ValidateRendering(settings.Rendering, result);
        ValidateUi(settings.Ui, result);
        ValidateAudio(settings.Audio, result);
        ValidateGameplay(settings.Gameplay, result);
        ValidateWorld(settings.World, result);
        ValidateInput(settings.Input, result);
        ValidateDebug(settings.Debug, result);
        ValidateAccessibility(settings.Accessibility, result);
        return result;
    }

    private static void ValidateVideo(VideoSettings video, SettingsValidationResult result)
    {
        if (video.Width < 640 || video.Width > 7680)
        {
            result.Add("video.width", "Width must be between 640 and 7680.");
        }

        if (video.Height < 360 || video.Height > 4320)
        {
            result.Add("video.height", "Height must be between 360 and 4320.");
        }

        if (video.FrameRateLimit != 0 && video.FrameRateLimit is < 30 or > 360)
        {
            result.Add("video.frameRateLimit", "Frame-rate limit must be unlimited (0) or between 30 and 360 FPS.");
        }

        if (video.UiScale < 0.5f || video.UiScale > 4f)
        {
            result.Add("video.uiScale", "UI scale must be between 0.5 and 4.");
        }

        if (video.RenderScale < 0.25f || video.RenderScale > 2f)
        {
            result.Add("video.renderScale", "Render scale must be between 0.25 and 2.");
        }
    }

    private static void ValidateRendering(RenderingSettings rendering, SettingsValidationResult result)
    {
        ValidateUnitRange(rendering.BloomStrength, "rendering.bloomStrength", result);
        ValidateUnitRange(rendering.VignetteStrength, "rendering.vignetteStrength", result);
        ValidateUnitRange(rendering.ColorGradeIntensity, "rendering.colorGradeIntensity", result);

        if (rendering.ParticleQuality < 0 || rendering.ParticleQuality > 3)
        {
            result.Add("rendering.particleQuality", "Particle quality must be between 0 and 3.");
        }

        ValidateUnitRange(rendering.LiquidOpacity, "rendering.liquidOpacity", result);
        ValidateUnitRange(rendering.LightingBlendStrength, "rendering.lightingBlendStrength", result);
        ValidateQuality(rendering.LightingQuality, "rendering.lightingQuality", result);
        ValidateQuality(rendering.ShadowQuality, "rendering.shadowQuality", result);
        ValidateQuality(rendering.ReflectionQuality, "rendering.reflectionQuality", result);
        ValidateQuality(rendering.UiEffectQuality, "rendering.uiEffectQuality", result);
        ValidateUnitRange(rendering.CaveAmbientLight, "rendering.caveAmbientLight", result);
        ValidateUnitRange(rendering.TorchBloomStrength, "rendering.torchBloomStrength", result);
        ValidateUnitRange(rendering.ShadowSoftness, "rendering.shadowSoftness", result);
        ValidateUnitRange(rendering.ReflectionStrength, "rendering.reflectionStrength", result);

        if (rendering.RaymarchStepBudget < 4 || rendering.RaymarchStepBudget > 128)
        {
            result.Add("rendering.raymarchStepBudget", "Raymarch step budget must be between 4 and 128.");
        }

        if (rendering.BlurRadiusPixels < 0 || rendering.BlurRadiusPixels > 24)
        {
            result.Add("rendering.blurRadiusPixels", "Blur radius must be between 0 and 24 pixels.");
        }

        ValidatePresentationRate(rendering.LightingUpdateRateHz, "rendering.lightingUpdateRateHz", result);
        ValidatePresentationRate(rendering.ReflectionUpdateRateHz, "rendering.reflectionUpdateRateHz", result);
        ValidatePresentationRate(rendering.AtmosphereUpdateRateHz, "rendering.atmosphereUpdateRateHz", result);
        ValidatePresentationRate(rendering.SceneCaptureUpdateRateHz, "rendering.sceneCaptureUpdateRateHz", result);

        if (rendering.MaxChunkRenderCacheEntries < 32 || rendering.MaxChunkRenderCacheEntries > 4096)
        {
            result.Add("rendering.maxChunkRenderCacheEntries", "Max chunk render cache entries must be between 32 and 4096.");
        }
    }

    private static void ValidateUi(UiSettings ui, SettingsValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(ui.Theme))
        {
            result.Add("ui.theme", "UI theme must not be empty.");
        }

        ValidateUnitRange(ui.PanelOpacity, "ui.panelOpacity", result);
        ValidateUnitRange(ui.HudOpacity, "ui.hudOpacity", result);
        ValidateUnitRange(ui.MenuBackdropOpacity, "ui.menuBackdropOpacity", result);

        if (ui.AnimationSpeed < 0.1f || ui.AnimationSpeed > 4f)
        {
            result.Add("ui.animationSpeed", "UI animation speed must be between 0.1 and 4.");
        }


        if (ui.CornerRadiusPixels < 0 || ui.CornerRadiusPixels > 16)
        {
            result.Add("ui.cornerRadiusPixels", "UI corner radius must be between 0 and 16 pixels.");
        }

        ValidateUnitRange(ui.BackdropBlurStrength, "ui.backdropBlurStrength", result);
        ValidateUnitRange(ui.GlowStrength, "ui.glowStrength", result);
        if (ui.TextScale < 0.75f || ui.TextScale > 2f)
        {
            result.Add("ui.textScale", "UI text scale must be between 0.75 and 2.");
        }
    }

    private static void ValidateAudio(AudioSettings audio, SettingsValidationResult result)
    {
        ValidateVolume(audio.MasterVolume, "audio.masterVolume", result);
        ValidateVolume(audio.MusicVolume, "audio.musicVolume", result);
        ValidateVolume(audio.SfxVolume, "audio.sfxVolume", result);
        ValidateVolume(audio.UiVolume, "audio.uiVolume", result);
    }

    private static void ValidateGameplay(GameplaySettings gameplay, SettingsValidationResult result)
    {
        if (gameplay.AutosaveMinutes < 0.5f || gameplay.AutosaveMinutes > 60f)
        {
            result.Add("gameplay.autosaveMinutes", "Autosave interval must be between 0.5 and 60 minutes.");
        }

        if (gameplay.MaxActiveEnemies < 0 || gameplay.MaxActiveEnemies > 512)
        {
            result.Add("gameplay.maxActiveEnemies", "Max active enemies must be between 0 and 512.");
        }

        if (gameplay.CameraZoom < 0.5f || gameplay.CameraZoom > 6f)
        {
            result.Add("gameplay.cameraZoom", "Camera zoom must be between 0.5 and 6.");
        }

        if (gameplay.InteractionReachPixels < 16f || gameplay.InteractionReachPixels > 512f)
        {
            result.Add("gameplay.interactionReachPixels", "Interaction reach must be between 16 and 512 pixels.");
        }

        if (gameplay.EnemySpawnRateMultiplier < 0f || gameplay.EnemySpawnRateMultiplier > 5f)
        {
            result.Add("gameplay.enemySpawnRateMultiplier", "Enemy spawn rate multiplier must be between 0 and 5.");
        }

        if (gameplay.RespawnDelaySeconds < 0f || gameplay.RespawnDelaySeconds > 30f)
        {
            result.Add("gameplay.respawnDelaySeconds", "Respawn delay must be between 0 and 30 seconds.");
        }

        if (gameplay.CameraLookAheadPixels < 0f || gameplay.CameraLookAheadPixels > 256f)
        {
            result.Add("gameplay.cameraLookAheadPixels", "Camera look-ahead must be between 0 and 256 pixels.");
        }

        if (gameplay.ScreenShakeMultiplier < 0f || gameplay.ScreenShakeMultiplier > 4f)
        {
            result.Add("gameplay.screenShakeMultiplier", "Screen shake multiplier must be between 0 and 4.");
        }


        if (gameplay.EntitySimulationRadiusPixels < 256f || gameplay.EntitySimulationRadiusPixels > 8192f)
        {
            result.Add("gameplay.entitySimulationRadiusPixels", "Entity simulation radius must be between 256 and 8192 pixels.");
        }

        if (gameplay.SpawnMinimumDistancePixels < 64f ||
            gameplay.SpawnMinimumDistancePixels > gameplay.SpawnMaximumDistancePixels)
        {
            result.Add("gameplay.spawnMinimumDistancePixels", "Spawn minimum distance must be at least 64 pixels and not exceed the maximum.");
        }

        if (gameplay.SpawnMaximumDistancePixels > gameplay.EntitySimulationRadiusPixels ||
            gameplay.SpawnMaximumDistancePixels > 8192f)
        {
            result.Add("gameplay.spawnMaximumDistancePixels", "Spawn maximum distance must fit inside the entity simulation radius.");
        }
    }

    private static void ValidateInput(InputSettings input, SettingsValidationResult result)
    {
        if (input.MouseSensitivity < 0.1f || input.MouseSensitivity > 5f)
        {
            result.Add("input.mouseSensitivity", "Mouse sensitivity must be between 0.1 and 5.");
        }

        ValidateBinding(input.KeyBindings.MoveLeft, "input.keyBindings.moveLeft", result);
        ValidateBinding(input.KeyBindings.MoveRight, "input.keyBindings.moveRight", result);
        ValidateBinding(input.KeyBindings.Jump, "input.keyBindings.jump", result);
        ValidateBinding(input.KeyBindings.Fly, "input.keyBindings.fly", result);
        ValidateBinding(input.KeyBindings.Glide, "input.keyBindings.glide", result);
        ValidateBinding(input.KeyBindings.AttackPrimary, "input.keyBindings.attackPrimary", result);
        ValidateBinding(input.KeyBindings.AttackSecondary, "input.keyBindings.attackSecondary", result);
        ValidateBinding(input.KeyBindings.OpenInventory, "input.keyBindings.openInventory", result);
        ValidateBinding(input.KeyBindings.OpenCrafting, "input.keyBindings.openCrafting", result);
        ValidateBinding(input.KeyBindings.OpenCharacterEditor, "input.keyBindings.openCharacterEditor", result);
        ValidateBinding(input.KeyBindings.Interact, "input.keyBindings.interact", result);
        ValidateBinding(input.KeyBindings.Pause, "input.keyBindings.pause", result);
        ValidateBinding(input.KeyBindings.DebugConsole, "input.keyBindings.debugConsole", result);
        ValidateBinding(input.KeyBindings.DebugToggle, "input.keyBindings.debugToggle", result);
    }

    private static void ValidateWorld(WorldSettings world, SettingsValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(world.WorldProfileId))
        {
            result.Add("world.worldProfileId", "World profile id must not be empty.");
        }

        if (world.ChunkLoadMargin < 0 || world.ChunkLoadMargin > 16)
        {
            result.Add("world.chunkLoadMargin", "Chunk load margin must be between 0 and 16.");
        }

        if (world.ChunkUnloadMargin < world.ChunkLoadMargin || world.ChunkUnloadMargin > 32)
        {
            result.Add("world.chunkUnloadMargin", "Chunk unload margin must be between the load margin and 32.");
        }

        if (world.StreamingBudgetChunksPerFrame < 1 || world.StreamingBudgetChunksPerFrame > 512)
        {
            result.Add("world.streamingBudgetChunksPerFrame", "Streaming budget must be between 1 and 512 chunks per frame.");
        }

        if (world.StreamingConcurrentLoads is < 1 or > 32)
        {
            result.Add("world.streamingConcurrentLoads", "Concurrent streaming loads must be between 1 and 32.");
        }

        if (world.StreamingConcurrentSaves is < 1 or > 8)
        {
            result.Add("world.streamingConcurrentSaves", "Concurrent streaming saves must be between 1 and 8.");
        }

        if (world.StreamingApplyQueueLimit is < 8 or > 2048)
        {
            result.Add("world.streamingApplyQueueLimit", "Streaming apply queue limit must be between 8 and 2048.");
        }

        if (!float.IsFinite(world.StreamingApplyBudgetMilliseconds) ||
            world.StreamingApplyBudgetMilliseconds is < 0.25f or > 33f)
        {
            result.Add("world.streamingApplyBudgetMilliseconds", "Streaming apply time budget must be between 0.25 and 33 milliseconds.");
        }

        if (world.StreamingApplyBudgetKilobytes is < 64 or > 16384)
        {
            result.Add("world.streamingApplyBudgetKilobytes", "Streaming apply byte budget must be between 64 and 16384 KiB.");
        }

        if (world.StreamingRetryAttempts is < 1 or > 16)
        {
            result.Add("world.streamingRetryAttempts", "Streaming retry attempts must be between 1 and 16.");
        }

        if (world.StreamingRetryInitialBackoffUpdates is < 1 or > 256)
        {
            result.Add("world.streamingRetryInitialBackoffUpdates", "Initial retry backoff must be between 1 and 256 updates.");
        }

        if (world.StreamingRetryMaximumBackoffUpdates < world.StreamingRetryInitialBackoffUpdates ||
            world.StreamingRetryMaximumBackoffUpdates > 4096)
        {
            result.Add("world.streamingRetryMaximumBackoffUpdates", "Maximum retry backoff must be between the initial backoff and 4096 updates.");
        }
    }

    private static void ValidateDebug(DebugSettings debug, SettingsValidationResult result)
    {
        if (debug.ProfilerMetricLimit < 3 || debug.ProfilerMetricLimit > 32)
        {
            result.Add("debug.profilerMetricLimit", "Profiler metric limit must be between 3 and 32.");
        }
    }

    private static void ValidateAccessibility(AccessibilitySettings accessibility, SettingsValidationResult result)
    {
        if (accessibility.InterfaceContrast < 0.5f || accessibility.InterfaceContrast > 2f)
        {
            result.Add("accessibility.interfaceContrast", "Interface contrast must be between 0.5 and 2.");
        }

        if (accessibility.TooltipDelaySeconds < 0f || accessibility.TooltipDelaySeconds > 2f)
        {
            result.Add("accessibility.tooltipDelaySeconds", "Tooltip delay must be between 0 and 2 seconds.");
        }
    }

    private static void ValidateQuality(int value, string path, SettingsValidationResult result)
    {
        if (value < 0 || value > 3)
        {
            result.Add(path, "Quality must be between 0 and 3.");
        }
    }

    private static void ValidateVolume(float value, string path, SettingsValidationResult result)
    {
        if (value < 0 || value > 1)
        {
            result.Add(path, "Volume must be in the range 0..1.");
        }
    }

    private static void ValidateUnitRange(float value, string path, SettingsValidationResult result)
    {
        if (value < 0 || value > 1)
        {
            result.Add(path, "Value must be in the range 0..1.");
        }
    }

    private static void ValidatePresentationRate(int value, string path, SettingsValidationResult result)
    {
        if (value is < 10 or > 240)
        {
            result.Add(path, "Presentation update rate must be between 10 and 240 Hz.");
        }
    }

    private static void ValidateBinding(string binding, string path, SettingsValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(binding))
        {
            result.Add(path, "Key binding must not be empty.");
        }
    }
}
