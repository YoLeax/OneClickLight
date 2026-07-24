using System;
using System.Linq;
using System.Reflection;
using Zenject;

namespace OneClickLight;

internal class SettingsApplier
{
    private readonly PlayerDataModel _playerDataModel;
    private readonly DiContainer _container;

    public SettingsApplier(PlayerDataModel playerDataModel, DiContainer container)
    {
        _playerDataModel = playerDataModel;
        _container = container;
    }

    public void Apply(PluginConfig.LightConfig cfg)
    {
        var playerData = _playerDataModel.playerData;
        var settings = playerData.playerSpecificSettings;

        // Base Game — PlayerSpecificSettings
        TrySetField(settings, "_environmentEffectsFilterDefaultPreset", cfg.OEnvironmentEffects, cfg.EnvironmentEffects);
        TrySetField(settings, "_environmentEffectsFilterExpertPlusPreset", cfg.OEpEnvironmentEffects, cfg.EpEnvironmentEffects);
        TrySetField(settings, "_noTextsAndHuds", cfg.ONoTextsOrHUDs, cfg.NoTextsOrHUDs);
        TrySetField(settings, "_advancedHud", cfg.OAdvancedHUD, cfg.AdvancedHUD);
        TrySetField(settings, "_arcsVisible", cfg.OArcVisibility, cfg.ArcVisibility);

        if (cfg.OOverrideDefaultEnvironments && playerData.overrideEnvironmentSettings != null)
            playerData.overrideEnvironmentSettings.overrideEnvironments = cfg.OverrideDefaultEnvironments;

        if (cfg.OOverrideDefaultColors && playerData.colorSchemesSettings != null)
            playerData.colorSchemesSettings.overrideDefaultColors = cfg.OverrideDefaultColors;

        // ColorTypeOverride (1.40.0+ only)
        TrySetColorTypeOverride(playerData, cfg);

        // SongCore — config lives in Zenject container
        TrySetSongCoreSetting(cfg.OAllowCustomSongNoteColors, cfg.AllowCustomSongNoteColors, "CustomSongNoteColors");
        TrySetSongCoreSetting(cfg.OAllowCustomSongObstacleColors, cfg.AllowCustomSongObstacleColors, "CustomSongObstacleColors");
        TrySetSongCoreSetting(cfg.OAllowCustomSongEnvironmentColors, cfg.AllowCustomSongEnvironmentColors, "CustomSongEnvironmentColors");

        // Chroma
        ApplyChromaSetting(cfg.OChromaDisableChromaEvents, cfg.ChromaDisableChromaEvents, "ChromaEventsDisabledSetting");
        ApplyChromaSetting(cfg.OChromaDisableEnvironmentEnhancements, cfg.ChromaDisableEnvironmentEnhancements, "EnvironmentEnhancementsDisabledSetting");
        ApplyChromaSetting(cfg.OChromaDisableNoteColoring, cfg.ChromaDisableNoteColoring, "NoteColoringDisabledSetting");
        ApplyChromaSetting(cfg.OChromaForceZenModeWalls, cfg.ChromaForceZenModeWalls, "ForceZenWallsEnabledSetting");
        ApplyChromaSetting(cfg.OChromaUseCustomEnvironment, cfg.ChromaUseCustomEnvironment, "CustomEnvironmentEnabledSetting");

        // JDFixer
        TrySetModProperty("JDFixer", "JDFixer.PluginConfig",
            cfg.OJDFixerEnabled, cfg.JDFixerEnabled, "enabled");

        // NoAutoExposure
        TrySetModProperty("NoAutoExposure", "NoAutoExposure.Config",
            cfg.ONoAutoExposureEnabled, cfg.NoAutoExposureEnabled, "Enabled");

        _playerDataModel.Save();
        Plugin.Log.Info("Applied config");
    }

    // ── SongCore (Zenject + assembly scan fallback) ──

    private void TrySetSongCoreSetting(bool shouldOverride, bool value, string propertyName)
    {
        if (!shouldOverride) return;

        try
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "SongCore");
            if (assembly == null) return;

            // Try known type names (differs between SongCore versions)
            foreach (var typeName in new[] { "SongCore.PluginConfig", "SongCore.SConfiguration" })
            {
                var configType = assembly.GetType(typeName);
                if (configType == null) continue;

                // 1.40.x: config lives in Zenject container
                var instance = _container.TryResolve(configType);
                // 1.39.x: config exposed via static reference
                if (instance == null) instance = FindConfigInstance(assembly, configType);
                if (instance == null) continue;

                if (TrySetAnyMember(instance, configType, propertyName, value))
                {
                    var changed = configType.GetMethod("Changed", BindingFlags.Public | BindingFlags.Instance);
                    changed?.Invoke(instance, null);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Warn("[SongCore] Failed to set '" + propertyName + "': " + ex.Message);
        }
    }

    // ── ColorTypeOverride (1.40.0+) ──

    private static void TrySetColorTypeOverride(PlayerData playerData, PluginConfig.LightConfig cfg)
    {
        if (!cfg.OColorTypeOverride || playerData.colorSchemesSettings == null) return;

        try
        {
            var prop = playerData.colorSchemesSettings.GetType()
                .GetProperty("colorOverrideType", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) return;

            prop.SetValue(playerData.colorSchemesSettings, Enum.ToObject(prop.PropertyType, cfg.ColorTypeOverride));
        }
        catch (Exception ex)
        {
            Plugin.Log.Warn("[BaseGame] Failed to set colorTypeOverride: " + ex.Message);
        }
    }

    // ── Base Game helpers ──

    private static void TrySetField<T>(object obj, string fieldName, bool shouldOverride, T value)
    {
        if (!shouldOverride) return;

        var field = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
            field.SetValue(obj, value);
        else
            Plugin.Log.Warn("[BaseGame] Field '" + fieldName + "' not found on " + obj.GetType().Name);
    }

    // ── Generic mod config helpers ──

    private static void TrySetModProperty(
        string assemblyName, string configTypeFullName,
        bool shouldOverride, object value, string propertyName)
    {
        if (!shouldOverride) return;

        try
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);
            if (assembly == null) return;

            var configType = assembly.GetType(configTypeFullName);
            if (configType == null) return;

            var instance = FindConfigInstance(assembly, configType);
            if (instance == null) return;

            if (!TrySetAnyMember(instance, configType, propertyName, value)) return;

            var changed = configType.GetMethod("Changed", BindingFlags.Public | BindingFlags.Instance);
            changed?.Invoke(instance, null);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warn("[" + assemblyName + "] Failed to set '" + propertyName + "': " + ex.Message);
        }
    }

    private static object? FindConfigInstance(Assembly assembly, Type configType)
    {
        foreach (var p in configType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (configType.IsAssignableFrom(p.PropertyType))
            {
                var v = p.GetValue(null);
                if (v != null) return v;
            }
        }

        foreach (var type in assembly.GetTypes())
        {
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                try { if (configType.IsAssignableFrom(p.PropertyType)) { var v = p.GetValue(null); if (v != null) return v; } }
                catch { }
            }
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                try { if (configType.IsAssignableFrom(f.FieldType)) { var v = f.GetValue(null); if (v != null) return v; } }
                catch { }
            }
        }

        return null;
    }

    private static bool TrySetAnyMember(object instance, Type type, string name, object value)
    {
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        var prop = type.GetProperty(name, flags);
        if (prop != null && prop.CanWrite) { prop.SetValue(instance, value); return true; }

        var field = type.GetField(name, flags);
        if (field != null) { field.SetValue(instance, value); return true; }

        return false;
    }

    // ── Chroma ──

    private static void ApplyChromaSetting(bool shouldOverride, bool value, string propertyName)
    {
        if (!shouldOverride) return;

        try
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Chroma");
            if (assembly == null) return;

            var settableType = assembly.GetType("Chroma.Settings.ChromaSettableSettings");
            if (settableType == null) return;

            var settingProp = settableType.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Static);
            if (settingProp == null) return;

            var settingObj = settingProp.GetValue(null);
            if (settingObj == null) return;

            var valueProp = settingObj.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
            valueProp?.SetValue(settingObj, value);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warn("[Chroma] Failed to set '" + propertyName + "': " + ex.Message);
        }
    }
}
