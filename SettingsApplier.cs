using System;
using System.Linq;
using System.Reflection;

namespace OneClickLight;

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

        if (cfg.OOverrideDefaultEnvironments && playerData.overrideEnvironmentSettings != null)
            playerData.overrideEnvironmentSettings.overrideEnvironments = cfg.OverrideDefaultEnvironments;

        if (cfg.OOverrideDefaultColors && playerData.colorSchemesSettings != null)
            playerData.colorSchemesSettings.overrideDefaultColors = cfg.OverrideDefaultColors;

        // SongCore
        TrySetModProperty("SongCore", "SongCore.SConfiguration",
            cfg.OAllowCustomSongNoteColors, cfg.AllowCustomSongNoteColors, "CustomSongNoteColors");
        TrySetModProperty("SongCore", "SongCore.SConfiguration",
            cfg.OAllowCustomSongObstacleColors, cfg.AllowCustomSongObstacleColors, "CustomSongObstacleColors");
        TrySetModProperty("SongCore", "SongCore.SConfiguration",
            cfg.OAllowCustomSongEnvironmentColors, cfg.AllowCustomSongEnvironmentColors, "CustomSongEnvironmentColors");

        // Chroma
        ApplyChromaSetting(cfg.OChromaDisableChromaEvents, cfg.ChromaDisableChromaEvents, "ChromaEventsDisabledSetting");
        ApplyChromaSetting(cfg.OChromaDisableEnvironmentEnhancements, cfg.ChromaDisableEnvironmentEnhancements, "EnvironmentEnhancementsDisabledSetting");
        ApplyChromaSetting(cfg.OChromaDisableNoteColoring, cfg.ChromaDisableNoteColoring, "NoteColoringDisabledSetting");
        ApplyChromaSetting(cfg.OChromaForceZenModeWalls, cfg.ChromaForceZenModeWalls, "ForceZenWallsEnabledSetting");
        ApplyChromaSetting(cfg.OChromaUseCustomEnvironment, cfg.ChromaUseCustomEnvironment, "CustomEnvironmentEnabledSetting");

        // JDFixer
        TrySetModProperty("JDFixer", "JDFixer.PluginConfig",
            cfg.OJDFixerEnabled, cfg.JDFixerEnabled, "enabled");

        _playerDataModel.Save();
        Plugin.Log.Info("Applied " + (cfg == _pluginConfig.CfgOn ? "ON" : "OFF") + " config");
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
            Plugin.Log.Warn("[BaseGame] Field '" + fieldName + "' not found on " + obj.GetType().Name);
        }
    }

    /// <summary>Set a property/field on an IPA config singleton found via assembly scanning.</summary>
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

    /// <summary>Find config singleton by scanning the entire assembly for static references.</summary>
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
                try
                {
                    if (configType.IsAssignableFrom(p.PropertyType))
                    {
                        var v = p.GetValue(null);
                        if (v != null) return v;
                    }
                }
                catch { }
            }

            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                try
                {
                    if (configType.IsAssignableFrom(f.FieldType))
                    {
                        var v = f.GetValue(null);
                        if (v != null) return v;
                    }
                }
                catch { }
            }
        }

        return null;
    }

    private static bool TrySetAnyMember(object instance, Type type, string name, object value)
    {
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        var prop = type.GetProperty(name, flags);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(instance, value);
            return true;
        }

        var field = type.GetField(name, flags);
        if (field != null)
        {
            field.SetValue(instance, value);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Chroma stores config in static SettableSetting&lt;bool&gt; properties
    /// on Chroma.Settings.ChromaSettableSettings.
    /// </summary>
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

            var settingProp = settableType.GetProperty(propertyName,
                BindingFlags.NonPublic | BindingFlags.Static);
            if (settingProp == null) return;

            var settingObj = settingProp.GetValue(null);
            if (settingObj == null) return;

            var valueProp = settingObj.GetType().GetProperty("Value",
                BindingFlags.Public | BindingFlags.Instance);
            valueProp?.SetValue(settingObj, value);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warn("[Chroma] Failed to set '" + propertyName + "': " + ex.Message);
        }
    }
}
