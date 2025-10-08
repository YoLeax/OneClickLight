using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using UnityEngine;
using HMUI;
using BeatSaberMarkupLanguage.GameplaySetup;
using BeatSaberMarkupLanguage.Attributes;
using IPA.Logging;
using ModestTree;
using Zenject;

namespace OneClickLight.Menu;

internal class GameplayMenu : IInitializable, IDisposable, INotifyPropertyChanged
{
    private const string MenuName = "One Click Light";
    private const string ResourcePath = "OneClickLight.Menu.gameplayMenu.bsml";
    private const float DefaultAlpha = 0.4f;
    private const float SelectedAlpha = 1.0f;

    private enum EPage
    {
        Main,
        EditConfig,
    }

    private enum ELightConfig
    {
        None,
        On,
        Off,
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    private readonly PluginConfig _cfg;
    
    private EPage _curPage = EPage.Main;
    private EPage CurPage
    {
        get => _curPage;
        set
        {
            _curPage = value;
            NotifyPropertyChanged(nameof(IsMainPageActive));
            NotifyPropertyChanged(nameof(IsEditConfigPageActive));
        }
    }

    private ELightConfig _curELightConfig = ELightConfig.None;
    private ELightConfig CurELightConfig
    {
        get => _curELightConfig;
        set
        {
            _curELightConfig = value;
            
            NotifyPropertyChanged(nameof(OEnvironmentEffects));
            NotifyPropertyChanged(nameof(EnvironmentEffects));
            NotifyPropertyChanged(nameof(OEpEnvironmentEffects));
            NotifyPropertyChanged(nameof(EpEnvironmentEffects));
            NotifyPropertyChanged(nameof(ONoTextsOrHUDs));
            NotifyPropertyChanged(nameof(NoTextsOrHUDs));
            NotifyPropertyChanged(nameof(OAdvancedHUD));
            NotifyPropertyChanged(nameof(AdvancedHUD));
            NotifyPropertyChanged(nameof(OArcVisibility));
            NotifyPropertyChanged(nameof(ArcVisibility));
            NotifyPropertyChanged(nameof(OOverrideDefaultEnvironments));
            NotifyPropertyChanged(nameof(OverrideDefaultEnvironments));
            NotifyPropertyChanged(nameof(OOverrideDefaultColors));
            NotifyPropertyChanged(nameof(OverrideDefaultColors));
            
            NotifyPropertyChanged(nameof(OAllowCustomSongNoteColors));
            NotifyPropertyChanged(nameof(AllowCustomSongNoteColors));
            NotifyPropertyChanged(nameof(OAllowCustomSongObstacleColors));
            NotifyPropertyChanged(nameof(AllowCustomSongObstacleColors));
            NotifyPropertyChanged(nameof(OAllowCustomSongEnvironmentColors));
            NotifyPropertyChanged(nameof(AllowCustomSongEnvironmentColors));
            
            NotifyPropertyChanged(nameof(OChromaUseCustomEnvironment));
            NotifyPropertyChanged(nameof(ChromaUseCustomEnvironment));
            NotifyPropertyChanged(nameof(OChromaDisableEnvironmentEnhancements));
            NotifyPropertyChanged(nameof(ChromaDisableEnvironmentEnhancements));
            NotifyPropertyChanged(nameof(OChromaDisableNoteColoring));
            NotifyPropertyChanged(nameof(ChromaDisableNoteColoring));
            NotifyPropertyChanged(nameof(OChromaDisableChromaEvents));
            NotifyPropertyChanged(nameof(ChromaDisableChromaEvents));
            NotifyPropertyChanged(nameof(OChromaForceZenModeWalls));
            NotifyPropertyChanged(nameof(ChromaForceZenModeWalls));

            NotifyPropertyChanged(nameof(OJDFixerEnabled));
            NotifyPropertyChanged(nameof(JDFixerEnabled));

        }
    }
    private PluginConfig.LightConfig CurLightConfig => CurELightConfig == ELightConfig.On ? _cfg.CfgOn : _cfg.CfgOff;
    
    public GameplayMenu(PluginConfig pluginConfig)
    {
        _cfg = pluginConfig;
    }
    
    public void Initialize()
    {
        GameplaySetup.Instance.AddTab(MenuName, ResourcePath, this);
    }

    public void Dispose()
    {
        if (GameplaySetup.Instance != null)
        {
            _cfg.Changed();
            GameplaySetup.Instance.RemoveTab(MenuName);
        }
    }
    
    
    
    #region Pages
    
    [UIValue("main_page_active")] private bool IsMainPageActive => _curPage == EPage.Main;
    [UIValue("edit_config_page_active")] private bool IsEditConfigPageActive => _curPage == EPage.EditConfig;

    [UIAction("edit_config_click")]
    private void EditConfig_Click()
    {
        CurPage = EPage.EditConfig;
        if (CurELightConfig == ELightConfig.None) OnSelectionClick(ELightConfig.On);
    }
    [UIAction("back_click")]
    private void Back_Click()
    {
        CurPage = EPage.Main;
    }
    
    #endregion

    
    
	#region Config Selection
    
    [UIComponent("edit_on_bg")] private readonly ImageView EditOn_BG = null!;
    [UIComponent("edit_off_bg")] private readonly ImageView EditOff_BG = null!;
    
    [UIAction("edit_on_click")] private void EditOn_Click() => OnSelectionClick(ELightConfig.On);
    [UIAction("edit_off_click")] private void EditOff_Click() => OnSelectionClick(ELightConfig.Off);
    private void OnSelectionClick(ELightConfig lightConfig)
    {
        if (lightConfig == CurELightConfig) return;
        else
        {
            Select(CurELightConfig, false);
            Select(lightConfig, true);
            CurELightConfig = lightConfig;
        }
    }
    private void Select(ELightConfig lightConfig, bool isSelect)
    {
        if (lightConfig == ELightConfig.None) return;
        ImageView? iv = lightConfig == ELightConfig.On ? EditOn_BG : EditOff_BG;
        if (iv == null) return;

        Color c = iv.color;
        c.a = isSelect ? SelectedAlpha : DefaultAlpha;
        iv.color = c;
    }

	#endregion



    #region Options


    #region Base Game

    [UIValue("environment_effects_options")] private List<object> EnvironmentEffectsOptions =
        [ "All Effects", "No Flickering", "No Effects"];
    private EnvironmentEffectsFilterPreset EnvironmentEffectsConverter(string value)
    {
        if (value == EnvironmentEffectsOptions[0].ToString()) return EnvironmentEffectsFilterPreset.AllEffects;
        if (value == EnvironmentEffectsOptions[1].ToString()) return EnvironmentEffectsFilterPreset.StrobeFilter;
        if (value == EnvironmentEffectsOptions[2].ToString()) return EnvironmentEffectsFilterPreset.NoEffects;
        return EnvironmentEffectsFilterPreset.NoEffects;
    }
    private string EnvironmentEffectsConverter(EnvironmentEffectsFilterPreset value)
    {
        switch (value)
        {
            case EnvironmentEffectsFilterPreset.AllEffects:
                return EnvironmentEffectsOptions[0].ToString();
            case EnvironmentEffectsFilterPreset.StrobeFilter:
                return EnvironmentEffectsOptions[1].ToString();
            case EnvironmentEffectsFilterPreset.NoEffects:
            default:
                return EnvironmentEffectsOptions[2].ToString();
        }
    }
    
    // Environment Effects
    [UIValue("o_environment_effects")] private bool OEnvironmentEffects => CurLightConfig.OEnvironmentEffects;
    [UIAction("on_o_environment_effects_change")] private void OnOEnvironmentEffectsChange(bool value) => CurLightConfig.OEnvironmentEffects = value;
    [UIValue("environment_effects")] private string EnvironmentEffects => EnvironmentEffectsConverter(CurLightConfig.EnvironmentEffects);
    [UIAction("on_environment_effects_change")] private void OnEnvironmentEffectsChange(string value) => CurLightConfig.EnvironmentEffects = EnvironmentEffectsConverter(value);

    // Expert+ Environment Effects
    [UIValue("o_ep_environment_effects")] private bool OEpEnvironmentEffects => CurLightConfig.OEpEnvironmentEffects;
    [UIAction("on_o_ep_environment_effects_change")] private void OnOEpEnvironmentEffectsChange(bool value) => CurLightConfig.OEpEnvironmentEffects = value;
    [UIValue("ep_environment_effects")] private string EpEnvironmentEffects => EnvironmentEffectsConverter(CurLightConfig.EpEnvironmentEffects);
    [UIAction("on_ep_environment_effects_change")] private void OnEpEnvironmentEffectsChange(string value) => CurLightConfig.EpEnvironmentEffects = EnvironmentEffectsConverter(value);

    // No Texts&HUDs
    [UIValue("o_no_texts_or_huds")] private bool ONoTextsOrHUDs => CurLightConfig.ONoTextsOrHUDs;
    [UIAction("on_o_no_texts_or_huds_change")] private void OnONoTextsOrHUDsChange(bool value) => CurLightConfig.ONoTextsOrHUDs = value;
    [UIValue("no_texts_or_huds")] private bool NoTextsOrHUDs => CurLightConfig.NoTextsOrHUDs;
    [UIAction("on_no_texts_or_huds_change")] private void OnNoTextsOrHUDsChange(bool value) => CurLightConfig.NoTextsOrHUDs = value;

    // Advanced HUD
    [UIValue("o_advanced_hud")] private bool OAdvancedHUD => CurLightConfig.OAdvancedHUD;
    [UIAction("on_o_advanced_hud_change")] private void OnOAdvancedHUDChange(bool value) => CurLightConfig.OAdvancedHUD = value;
    [UIValue("advanced_hud")] private bool AdvancedHUD => CurLightConfig.AdvancedHUD;
    [UIAction("on_advanced_hud_change")] private void OnAdvancedHUDChange(bool value) => CurLightConfig.AdvancedHUD = value;

    [UIValue("arc_visibility_options")] private List<object> ArcVisibilityOptions =
        [ "High", "Standard", "Low", "None", ];
    private ArcVisibilityType ArcVisibilityConverter(string value)
    {
        if (value == ArcVisibilityOptions[0].ToString()) return ArcVisibilityType.High;
        if (value == ArcVisibilityOptions[1].ToString()) return ArcVisibilityType.Standard;
        if (value == ArcVisibilityOptions[2].ToString()) return ArcVisibilityType.Low;
        if (value == ArcVisibilityOptions[3].ToString()) return ArcVisibilityType.None;
        return ArcVisibilityType.None;
    }
    private string ArcVisibilityConverter(ArcVisibilityType value)
    {
        switch (value)
        {
            case ArcVisibilityType.High:
                return ArcVisibilityOptions[0].ToString();
            case ArcVisibilityType.Standard:
                return ArcVisibilityOptions[1].ToString();
            case ArcVisibilityType.Low:
                return ArcVisibilityOptions[2].ToString();
            case ArcVisibilityType.None:
            default:
                return ArcVisibilityOptions[3].ToString();
        }
    }
    
    // Arc Visibility
    [UIValue("o_arc_visibility")] private bool OArcVisibility => CurLightConfig.OArcVisibility;
    [UIAction("on_o_arc_visibility_change")] private void OnOArcVisibilityChange(bool value) => CurLightConfig.OArcVisibility = value;
    [UIValue("arc_visibility")] private string ArcVisibility => ArcVisibilityConverter(CurLightConfig.ArcVisibility);
    [UIAction("on_arc_visibility_change")] private void OnArcVisibilityChange(string value) => CurLightConfig.ArcVisibility = ArcVisibilityConverter(value);

    // Override Default Environments
    [UIValue("o_override_default_environments")] private bool OOverrideDefaultEnvironments => CurLightConfig.OOverrideDefaultEnvironments;
    [UIAction("on_o_override_default_environments_change")] private void OnOOverrideDefaultEnvironmentsChange(bool value) => CurLightConfig.OOverrideDefaultEnvironments = value;
    [UIValue("override_default_environments")] private bool OverrideDefaultEnvironments => CurLightConfig.OverrideDefaultEnvironments;
    [UIAction("on_override_default_environments_change")] private void OnOverrideDefaultEnvironmentsChange(bool value) => CurLightConfig.OverrideDefaultEnvironments = value;

    // Override Default Colors
    [UIValue("o_override_default_colors")] private bool OOverrideDefaultColors => CurLightConfig.OOverrideDefaultColors;
    [UIAction("on_o_override_default_colors_change")] private void OnOOverrideDefaultColorsChange(bool value) => CurLightConfig.OOverrideDefaultColors = value;
    [UIValue("override_default_colors")] private bool OverrideDefaultColors => CurLightConfig.OverrideDefaultColors;
    [UIAction("on_override_default_colors_change")] private void OnOverrideDefaultColorsChange(bool value) => CurLightConfig.OverrideDefaultColors = value;

    #endregion


    #region Song Core

    // Allow Custom Song Note Colors
    [UIValue("o_allow_custom_song_note_colors")] private bool OAllowCustomSongNoteColors => CurLightConfig.OAllowCustomSongNoteColors;
    [UIAction("on_o_allow_custom_song_note_colors_change")] private void OnOAllowCustomSongNoteColorsChange(bool value) => CurLightConfig.OAllowCustomSongNoteColors = value;
    [UIValue("allow_custom_song_note_colors")] private bool AllowCustomSongNoteColors => CurLightConfig.AllowCustomSongNoteColors;
    [UIAction("on_allow_custom_song_note_colors_change")] private void OnAllowCustomSongNoteColorsChange(bool value) => CurLightConfig.AllowCustomSongNoteColors = value;

    // Allow Custom Song Obstacle Colors
    [UIValue("o_allow_custom_song_obstacle_colors")] private bool OAllowCustomSongObstacleColors => CurLightConfig.OAllowCustomSongObstacleColors;
    [UIAction("on_o_allow_custom_song_obstacle_colors_change")] private void OnAllowCustomSongObstacleColorsOChange(bool value) => CurLightConfig.OAllowCustomSongObstacleColors = value;
    [UIValue("allow_custom_song_obstacle_colors")] private bool AllowCustomSongObstacleColors => CurLightConfig.AllowCustomSongObstacleColors;
    [UIAction("on_allow_custom_song_obstacle_colors_change")] private void OnAllowCustomSongObstacleColorsChange(bool value) => CurLightConfig.AllowCustomSongObstacleColors = value;

    // Allow Custom Song EnvironmentColors
    [UIValue("o_allow_custom_song_environment_colors")] private bool OAllowCustomSongEnvironmentColors => CurLightConfig.OAllowCustomSongEnvironmentColors;
    [UIAction("on_o_allow_custom_song_environment_colors_change")] private void OnAllowCustomSongEnvironmentColorsOChange(bool value) => CurLightConfig.OAllowCustomSongEnvironmentColors = value;
    [UIValue("allow_custom_song_environment_colors")] private bool AllowCustomSongEnvironmentColors => CurLightConfig.AllowCustomSongEnvironmentColors;
    [UIAction("on_allow_custom_song_environment_colors_change")] private void OnAllowCustomSongEnvironmentColorsChange(bool value) => CurLightConfig.AllowCustomSongEnvironmentColors = value;

    #endregion


    #region Chroma

    // Use Custom Environment
    [UIValue("o_chroma_use_custom_environment")] private bool OChromaUseCustomEnvironment => CurLightConfig.OChromaUseCustomEnvironment;
    [UIAction("on_o_chroma_use_custom_environment_change")] private void OnOChromaUseCustomEnvironmentChange(bool value) => CurLightConfig.OChromaUseCustomEnvironment = value;
    [UIValue("chroma_use_custom_environment")] private bool ChromaUseCustomEnvironment => CurLightConfig.ChromaUseCustomEnvironment;
    [UIAction("on_chroma_use_custom_environment_change")] private void OnChromaUseCustomEnvironmentChange(bool value) => CurLightConfig.ChromaUseCustomEnvironment = value;

    // Disable Environment Enhancements
    [UIValue("o_chroma_disable_environment_enhancements")] private bool OChromaDisableEnvironmentEnhancements => CurLightConfig.OChromaDisableEnvironmentEnhancements;
    [UIAction("on_o_chroma_disable_environment_enhancements_change")] private void OnChromaDisableEnvironmentEnhancementsOChange(bool value) => CurLightConfig.OChromaDisableEnvironmentEnhancements = value;
    [UIValue("chroma_disable_environment_enhancements")] private bool ChromaDisableEnvironmentEnhancements => CurLightConfig.ChromaDisableEnvironmentEnhancements;
    [UIAction("on_chroma_disable_environment_enhancements_change")] private void OnChromaDisableEnvironmentEnhancementsChange(bool value) => CurLightConfig.ChromaDisableEnvironmentEnhancements = value;

    // Disable Note Coloring
    [UIValue("o_chroma_disable_note_coloring")] private bool OChromaDisableNoteColoring => CurLightConfig.OChromaDisableNoteColoring;
    [UIAction("on_o_chroma_disable_note_coloring_change")] private void OnOChromaDisableNoteColoringChange(bool value) => CurLightConfig.OChromaDisableNoteColoring = value;
    [UIValue("chroma_disable_note_coloring")] private bool ChromaDisableNoteColoring => CurLightConfig.ChromaDisableNoteColoring;
    [UIAction("on_chroma_disable_note_coloring_change")] private void OnChromaDisableNoteColoringChange(bool value) => CurLightConfig.ChromaDisableNoteColoring = value;

    // Disable Chroma Events
    [UIValue("o_chroma_disable_chroma_events")] private bool OChromaDisableChromaEvents => CurLightConfig.OChromaDisableChromaEvents;
    [UIAction("on_o_chroma_disable_chroma_events_change")] private void OnOChromaDisableChromaEventsChange(bool value) => CurLightConfig.OChromaDisableChromaEvents = value;
    [UIValue("chroma_disable_chroma_events")] private bool ChromaDisableChromaEvents => CurLightConfig.ChromaDisableChromaEvents;
    [UIAction("on_chroma_disable_chroma_events_change")] private void OnChromaDisableChromaEventsChange(bool value) => CurLightConfig.ChromaDisableChromaEvents = value;

    // Force Zen Mode Walls
    [UIValue("o_chroma_force_zen_mode_walls")] private bool OChromaForceZenModeWalls => CurLightConfig.OChromaForceZenModeWalls;
    [UIAction("on_o_chroma_force_zen_mode_walls_change")] private void OnOChromaForceZenModeWallsChange(bool value) => CurLightConfig.OChromaForceZenModeWalls = value;
    [UIValue("chroma_force_zen_mode_walls")] private bool ChromaForceZenModeWalls => CurLightConfig.ChromaForceZenModeWalls;
    [UIAction("on_chroma_force_zen_mode_walls_change")] private void OnChromaForceZenModeWallsChange(bool value) => CurLightConfig.ChromaForceZenModeWalls = value;

    #endregion


    #region JDFixer

    // JDFixer Enabled
    [UIValue("o_jd_fixer_enabled")] private bool OJDFixerEnabled => CurLightConfig.OJDFixerEnabled;
    [UIAction("on_o_jd_fixer_enabled_change")] private void OnJDFixerEnabledOChange(bool value) => CurLightConfig.OJDFixerEnabled = value;
    [UIValue("jd_fixer_enabled")] private bool JDFixerEnabled => CurLightConfig.JDFixerEnabled;
    [UIAction("on_jd_fixer_enabled_change")] private void OnJDFixerEnabledChange(bool value) => CurLightConfig.JDFixerEnabled = value;

    #endregion

    
    #endregion
    

    
    private void NotifyPropertyChanged(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}