using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.GameplaySetup;
using UnityEngine;
using Zenject;

namespace OneClickLight;

internal class SettingsApplier
{
    private readonly PlayerDataModel _playerDataModel;
    private readonly DiContainer _container;
    private readonly GameplaySetup _gameplaySetup;
    private readonly GameplaySetupViewController _gameplaySetupViewController;
    private readonly StandardLevelDetailViewController _levelDetailViewController;

    public SettingsApplier(
        PlayerDataModel playerDataModel,
        DiContainer container,
        GameplaySetup gameplaySetup,
        GameplaySetupViewController gameplaySetupViewController,
        StandardLevelDetailViewController levelDetailViewController)
    {
        _playerDataModel = playerDataModel;
        _container = container;
        _gameplaySetup = gameplaySetup;
        _gameplaySetupViewController = gameplaySetupViewController;
        _levelDetailViewController = levelDetailViewController;
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
        TrySetSongCoreSetting(
            cfg.OAllowCustomSongNoteColors,
            cfg.AllowCustomSongNoteColors,
            "CustomSongNoteColors",
            "NoteColors");
        TrySetSongCoreSetting(
            cfg.OAllowCustomSongObstacleColors,
            cfg.AllowCustomSongObstacleColors,
            "CustomSongObstacleColors",
            "ObstacleColors");
        TrySetSongCoreSetting(
            cfg.OAllowCustomSongEnvironmentColors,
            cfg.AllowCustomSongEnvironmentColors,
            "CustomSongEnvironmentColors",
            "EnvironmentColors");

        // Chroma
        ApplyChromaSetting(
            cfg.OChromaDisableChromaEvents,
            cfg.ChromaDisableChromaEvents,
            "ChromaEventsDisabled",
            "ChromaEventsDisabledSetting");
        ApplyChromaSetting(
            cfg.OChromaDisableEnvironmentEnhancements,
            cfg.ChromaDisableEnvironmentEnhancements,
            "EnvironmentEnhancementsDisabled",
            "EnvironmentEnhancementsDisabledSetting");
        ApplyChromaSetting(
            cfg.OChromaDisableNoteColoring,
            cfg.ChromaDisableNoteColoring,
            "NoteColoringDisabled",
            "NoteColoringDisabledSetting");
        ApplyChromaSetting(
            cfg.OChromaForceZenModeWalls,
            cfg.ChromaForceZenModeWalls,
            "ForceZenWallsEnabled",
            "ForceZenWallsEnabledSetting");
        ApplyChromaSetting(
            cfg.OChromaUseCustomEnvironment,
            cfg.ChromaUseCustomEnvironment,
            "CustomEnvironmentEnabled",
            "CustomEnvironmentEnabledSetting");

        // JDFixer
        if (cfg.OJDFixerEnabled &&
            !TrySetModUiProperty(
                "JDFixer",
                new[]
                {
                    "JDFixer.UI.ModifierUI",
                    "JDFixer.UI.LegacyModifierUI",
                    "JDFixer.UI.CustomOnlineUI",
                },
                "Enabled",
                cfg.JDFixerEnabled))
        {
            TrySetModProperty(
                "JDFixer",
                "JDFixer.PluginConfig",
                cfg.JDFixerEnabled,
                "enabled");
        }

        // NoAutoExposure
        if (cfg.ONoAutoExposureEnabled &&
            !TrySetModUiProperty(
                "NoAutoExposure",
                new[] { "NoAutoExposure.Menu.GameplayMenu" },
                "Enabled",
                cfg.NoAutoExposureEnabled))
        {
            TrySetModProperty(
                "NoAutoExposure",
                "NoAutoExposure.Config",
                cfg.NoAutoExposureEnabled,
                "Enabled");
        }

        RefreshBaseGameUi();
        RefreshModToggleUi();
        _playerDataModel.Save();
        _levelDetailViewController.RefreshContentLevelDetailView();
        Plugin.Log.Info("Applied config");
    }

    // ── SongCore ──

    private void TrySetSongCoreSetting(
        bool shouldOverride,
        bool value,
        string configPropertyName,
        string uiPropertyName)
    {
        if (!shouldOverride) return;

        if (TrySetModUiProperty(
                "SongCore",
                new[] { "SongCore.UI.SettingsController" },
                uiPropertyName,
                value))
        {
            return;
        }

        foreach (var typeName in new[] { "SongCore.PluginConfig", "SongCore.SConfiguration" })
        {
            if (TrySetModProperty("SongCore", typeName, value, configPropertyName))
            {
                return;
            }
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

    private bool TrySetModProperty(
        string assemblyName,
        string configTypeFullName,
        object value,
        string propertyName)
    {
        try
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);
            if (assembly == null) return false;

            var configType = assembly.GetType(configTypeFullName);
            if (configType == null) return false;

            var instance = _container.TryResolve(configType) ??
                           FindConfigInstance(assembly, configType);
            if (instance == null) return false;

            if (!TrySetAnyMember(instance, configType, propertyName, value)) return false;

            var changed = configType.GetMethod(
                "Changed",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            changed?.Invoke(instance, null);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warn("[" + assemblyName + "] Failed to set '" + propertyName + "': " + ex.Message);
            return false;
        }
    }

    private bool TrySetModUiProperty(
        string assemblyName,
        IReadOnlyCollection<string> uiTypeFullNames,
        string propertyName,
        object value)
    {
        try
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);
            if (assembly == null) return false;

            var targetTypes = uiTypeFullNames
                .Select(assembly.GetType)
                .Where(type => type != null)
                .Cast<Type>()
                .ToArray();
            if (targetTypes.Length == 0) return false;

            var instances = new List<object>();
            foreach (var targetType in targetTypes)
            {
                var resolved = _container.TryResolve(targetType);
                if (resolved != null && !instances.Contains(resolved))
                {
                    instances.Add(resolved);
                }
            }

            foreach (var host in GetGameplaySetupHosts())
            {
                if (targetTypes.Any(type => type.IsInstanceOfType(host)) &&
                    !instances.Contains(host))
                {
                    instances.Add(host);
                }
            }

            var changedAny = false;
            foreach (var instance in instances)
            {
                changedAny |= TrySetAnyMember(
                    instance,
                    instance.GetType(),
                    propertyName,
                    value);
            }

            return changedAny;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warn("[" + assemblyName + "] Failed to set UI property '" +
                            propertyName + "': " + ex.Message);
            return false;
        }
    }

    private IEnumerable<object> GetGameplaySetupHosts()
    {
        var menusField = _gameplaySetup.GetType().GetField(
            "menus",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (!(menusField?.GetValue(_gameplaySetup) is IEnumerable menus))
        {
            yield break;
        }

        foreach (var menu in menus)
        {
            if (menu == null) continue;

            var menuType = menu.GetType();
            object? host = menuType
                .GetProperty("Host", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(menu);

            // BSML versions before GameplaySetupMenu exposed Host as a property
            // used a lower-case field instead.
            host ??= menuType
                .GetField("host", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(menu);

            if (host != null)
            {
                yield return host;
            }
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

    // ── Base Game UI ──

    private void RefreshBaseGameUi()
    {
        try
        {
            var panelField = _gameplaySetupViewController.GetType().GetField(
                "_playerSettingsPanelController",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (!(panelField?.GetValue(_gameplaySetupViewController) is
                    PlayerSettingsPanelController playerSettingsPanelController))
            {
                Plugin.Log.Warn("[BaseGame] Player settings panel was not found.");
                return;
            }

            // PlayerSettingsPanelController caches both PlayerSpecificSettings and
            // a one-shot _refreshed flag. SetData resets that cache and calls the
            // game's own Refresh implementation for every Player Options control.
            //
            // Beat Saber 1.39.1 and 1.40.8 both take PlayerData here. Some older
            // versions (including 1.29.1) take PlayerSpecificSettings instead, so
            // keep a defensive fallback while resolving the runtime overload.
            var setDataMethod = playerSettingsPanelController.GetType().GetMethod(
                "SetData",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(PlayerData) },
                null);
            object setDataArgument = _playerDataModel.playerData;

            if (setDataMethod == null)
            {
                setDataMethod = playerSettingsPanelController.GetType().GetMethod(
                    "SetData",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new[] { _playerDataModel.playerData.playerSpecificSettings.GetType() },
                    null);
                setDataArgument = _playerDataModel.playerData.playerSpecificSettings;
            }

            if (setDataMethod == null)
            {
                Plugin.Log.Warn("[BaseGame] Player settings SetData method was not found.");
                return;
            }

            setDataMethod.Invoke(playerSettingsPanelController, new[] { setDataArgument });
        }
        catch (Exception ex)
        {
            Plugin.Log.Warn("[BaseGame] Failed to refresh player settings UI: " + ex.Message);
        }
    }

    // ── Chroma ──

    private void ApplyChromaSetting(
        bool shouldOverride,
        bool value,
        string configPropertyName,
        string settablePropertyName)
    {
        if (!shouldOverride) return;

        // The Config setters contain Chroma's runtime side effects. In particular,
        // ChromaEventsDisabled registers or deregisters the "Chroma" SongCore
        // capability. Writing SettableSetting.Value directly bypasses that logic.
        if (TrySetModUiProperty(
                "Chroma",
                new[] { "Chroma.Settings.ChromaSettingsUI" },
                configPropertyName,
                value))
        {
            return;
        }

        if (TrySetModProperty(
                "Chroma",
                "Chroma.Settings.Config",
                value,
                configPropertyName))
        {
            return;
        }

        try
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Chroma");
            if (assembly == null) return;

            var settableType = assembly.GetType("Chroma.Settings.ChromaSettableSettings");
            if (settableType == null) return;

            var settingProp = settableType.GetProperty(
                settablePropertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (settingProp == null) return;

            var settingObj = settingProp.GetValue(null);
            if (settingObj == null) return;

            var valueProp = settingObj.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
            valueProp?.SetValue(settingObj, value);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warn("[Chroma] Failed to set '" + configPropertyName + "': " + ex.Message);
        }
    }

    private static void RefreshModToggleUi()
    {
        var refreshed = 0;
        foreach (var toggleSetting in Resources.FindObjectsOfTypeAll<ToggleSetting>())
        {
            if (toggleSetting == null ||
                toggleSetting.AssociatedValue == null ||
                !toggleSetting.gameObject.scene.IsValid())
            {
                continue;
            }

            try
            {
                toggleSetting.ReceiveValue();
                refreshed++;
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn("[UI] Failed to refresh toggle '" +
                                toggleSetting.name + "': " + ex.Message);
            }
        }

        Plugin.Log.Debug("[UI] Refreshed " + refreshed + " mod setting toggles.");
    }
}
