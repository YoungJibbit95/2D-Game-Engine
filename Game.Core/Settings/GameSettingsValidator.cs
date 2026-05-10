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
        ValidateBinding(input.KeyBindings.AttackPrimary, "input.keyBindings.attackPrimary", result);
        ValidateBinding(input.KeyBindings.AttackSecondary, "input.keyBindings.attackSecondary", result);
        ValidateBinding(input.KeyBindings.OpenInventory, "input.keyBindings.openInventory", result);
        ValidateBinding(input.KeyBindings.OpenCrafting, "input.keyBindings.openCrafting", result);
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

    private static void ValidateBinding(string binding, string path, SettingsValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(binding))
        {
            result.Add(path, "Key binding must not be empty.");
        }
    }
}
