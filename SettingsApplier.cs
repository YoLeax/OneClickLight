using System.Reflection;

namespace OneClickLight;

/// <summary>
/// Applies LightConfig settings to the actual game state.
/// Writes to PlayerSpecificSettings / OverrideEnvironmentSettings / ColorSchemesSettings via reflection,
/// then persists via PlayerDataModel.Save().
/// </summary>
internal class SettingsApplier
{
    private readonly PlayerDataModel _playerDataModel;
    private readonly PluginConfig _pluginConfig;

    public SettingsApplier(PlayerDataModel playerDataModel, PluginConfig pluginConfig)
    {
        _playerDataModel = playerDataModel;
        _pluginConfig = pluginConfig;
    }

    public void ApplyOn() => Apply(_pluginConfig.CfgOn);
    public void ApplyOff() => Apply(_pluginConfig.CfgOff);

    private void Apply(PluginConfig.LightConfig cfg)
    {
        var playerData = _playerDataModel.playerData;
        var settings = playerData.playerSpecificSettings;

        // Base Game — PlayerSpecificSettings
        TrySetField(settings, "_environmentEffectsFilterDefaultPreset", cfg.OEnvironmentEffects, cfg.EnvironmentEffects);
        TrySetField(settings, "_environmentEffectsFilterExpertPlusPreset", cfg.OEpEnvironmentEffects, cfg.EpEnvironmentEffects);
        TrySetField(settings, "_noTextsAndHuds", cfg.ONoTextsOrHUDs, cfg.NoTextsOrHUDs);
        TrySetField(settings, "_advancedHud", cfg.OAdvancedHUD, cfg.AdvancedHUD);
        TrySetField(settings, "_arcsVisible", cfg.OArcVisibility, cfg.ArcVisibility);

        // Base Game — Environment Override
        if (cfg.OOverrideDefaultEnvironments && playerData.overrideEnvironmentSettings != null)
            playerData.overrideEnvironmentSettings.overrideEnvironments = cfg.OverrideDefaultEnvironments;

        // Base Game — Color Override
        if (cfg.OOverrideDefaultColors && playerData.colorSchemesSettings != null)
            playerData.colorSchemesSettings.overrideDefaultColors = cfg.OverrideDefaultColors;

        // TODO: SongCore / Chroma / JDFixer (optional dependencies)

        _playerDataModel.Save();
        Plugin.Log.Info($"Applied {(cfg == _pluginConfig.CfgOn ? "ON" : "OFF")} config");
    }

    private static void TrySetField<T>(object obj, string fieldName, bool shouldOverride, T value)
    {
        if (!shouldOverride) return;

        var field = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(obj, value);
        }
        else
        {
            Plugin.Log.Warn($"Field '{fieldName}' not found on {obj.GetType().Name}");
        }
    }
}
