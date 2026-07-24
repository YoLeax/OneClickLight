using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.UI;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.GameplaySetup;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Parser;
using IPA.Logging;
using IPA.Loader;
using ModestTree;
using Zenject;

namespace OneClickLight.Menu;

internal class GameplayMenu : IInitializable, ITickable, IDisposable, INotifyPropertyChanged
{
    private const string MenuName = "One Click Light";
    private const string ResourcePath = "OneClickLight.Menu.gameplayMenu.bsml";
    private const int MaxButtonsPerRow = 3;
    private const float ButtonHeight = 10f;
    private const float ButtonSpacing = 3f;
    private const float RowSpacing = 1f;
    private const float ContainerPaddingH = 4f;
    private const float ContainerPaddingV = 6f;
    private const int ButtonFontSize = 7;

    private const float DeleteConfirmWaitSeconds = 2.0f;

    private enum ConfirmState { Default, StartConfirming, Confirming }

    private enum EPage
    {
        Main,
        EditConfig,
    }

    // Simple host for BSMLParser action resolution
    private class ButtonActionHost
    {
        private readonly Action _action;
        public ButtonActionHost(Action action) => _action = action;

        [UIAction("on_slot_click")]
        private void OnSlotClick() => _action();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly PluginConfig _cfg;
    private readonly SettingsApplier _settingsApplier;
    private readonly bool _isVersion40Plus;
    private readonly bool _isJDFixerAvailable;
    private readonly bool _isNoAutoExposureAvailable;

    private ConfirmState _deleteState = ConfirmState.Default;
    private float _deleteWaitUntil = 0f;

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

    private int _curConfigIndex;
    private int CurConfigIndex
    {
        get => _curConfigIndex;
        set
        {
            _curConfigIndex = value;
            NotifyPropertyChanged(nameof(CurConfigName));
            NotifyAllConfigValues();
            UpdateActionButtons();
            UpdateColorButton();
        }
    }

    private PluginConfig.LightConfig CurLightConfig => _cfg.GetSlot(_curConfigIndex);

    // BSML references
    [UIComponent("config_dropdown")] private readonly DropDownListSetting _configDropdown = null!;

    private Transform? _contentTransform;
    private ModalKeyboard? _nameKeyboard;
    private ModalColorPicker? _colorPicker;

    public GameplayMenu(PluginConfig pluginConfig, SettingsApplier settingsApplier)
    {
        _cfg = pluginConfig;
        _settingsApplier = settingsApplier;
        _isVersion40Plus = typeof(ColorSchemesSettings).GetProperty("colorOverrideType") != null;
        _isJDFixerAvailable = PluginManager.GetPluginFromId("JDFixer") != null;
        _isNoAutoExposureAvailable = PluginManager.GetPluginFromId("NoAutoExposure") != null;
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

    public void Tick()
    {
        if (_deleteState == ConfirmState.StartConfirming)
        {
            _deleteWaitUntil = Time.time + DeleteConfirmWaitSeconds;
            _deleteState = ConfirmState.Confirming;
            NotifyPropertyChanged(nameof(DeleteButtonText));
        }
        else if (_deleteState == ConfirmState.Confirming)
        {
            if (Time.time > _deleteWaitUntil)
            {
                _deleteState = ConfirmState.Default;
                NotifyPropertyChanged(nameof(DeleteButtonText));
            }
        }
    }

    [UIValue("delete_button_text")]
    private string DeleteButtonText => _deleteState == ConfirmState.Default ? "-" : "?";


    #region Post-Parse

    [UIAction("#post-parse")]
    private void PostParse()
    {
        // Find the "Edit Config" button by searching for its text
        Button? editConfigBtn = null;
        foreach (var btn in Resources.FindObjectsOfTypeAll<Button>())
        {
            var tmp = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null && tmp.text == "Edit Config")
            {
                editConfigBtn = btn;
                break;
            }
        }
        if (editConfigBtn == null) return;

        // The main page vertical is the direct parent of the Edit Config button
        var mainPageVertical = editConfigBtn.transform.parent;
        if (mainPageVertical == null) return;

        // Create content container and insert before the Edit Config button
        var containerGO = new GameObject("ConfigSlotsContainer");
        containerGO.AddComponent<RectTransform>();
        containerGO.transform.SetParent(mainPageVertical, false);
        containerGO.transform.SetSiblingIndex(1); // After top spacer, before Edit Config

        _contentTransform = containerGO.transform;

        // VerticalLayoutGroup to size rows
        var vlg = containerGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = RowSpacing;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset((int)ContainerPaddingH, (int)ContainerPaddingH, (int)ContainerPaddingV, (int)ContainerPaddingV);

        // Size container to fit content
        var slotCount = _cfg.SlotCount;
        var rowCount = (slotCount + MaxButtonsPerRow - 1) / MaxButtonsPerRow;
        var containerHeight = rowCount * ButtonHeight + (rowCount - 1) * RowSpacing;

        var containerRT = containerGO.GetComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 1f);
        containerRT.anchorMax = new Vector2(0.5f, 1f);
        containerRT.pivot = new Vector2(0.5f, 1f);
        containerRT.sizeDelta = new Vector2(96f, containerHeight);

        RebuildMainPageButtons();

        // Find modal keyboard
        foreach (var mk in Resources.FindObjectsOfTypeAll<ModalKeyboard>())
        {
            _nameKeyboard = mk;
            break;
        }

        // Find color picker
        foreach (var cp in Resources.FindObjectsOfTypeAll<ModalColorPicker>())
        {
            _colorPicker = cp;
            break;
        }

        // Apply current config color to ◉ button
        UpdateColorButton();
    }

    #endregion


    #region Main Page — Dynamic Buttons

    private void RebuildMainPageButtons()
    {
        if (_contentTransform == null) return;

        // Update container height
        var slotCount = _cfg.SlotCount;
        var rowCount = (slotCount + MaxButtonsPerRow - 1) / MaxButtonsPerRow;
        var containerHeight = rowCount * ButtonHeight + (rowCount - 1) * RowSpacing;
        var rt = _contentTransform.GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = new Vector2(rt.sizeDelta.x, containerHeight);

        // Destroy existing children
        foreach (Transform child in _contentTransform)
            UnityEngine.Object.Destroy(child.gameObject);

        // Create rows with HorizontalLayoutGroup
        HorizontalLayoutGroup? currentRow = null;

        for (int i = 0; i < _cfg.SlotCount; i++)
        {
            if (i % MaxButtonsPerRow == 0)
            {
                var rowGO = new GameObject("Row");
                rowGO.transform.SetParent(_contentTransform, false);
                rowGO.AddComponent<RectTransform>();

                currentRow = rowGO.AddComponent<HorizontalLayoutGroup>();
                currentRow.spacing = ButtonSpacing;
                currentRow.childAlignment = TextAnchor.MiddleCenter;
                currentRow.childControlWidth = false;
                currentRow.childControlHeight = false;
                currentRow.childForceExpandWidth = false;
                currentRow.childForceExpandHeight = false;
            }

            CreateConfigButton(_cfg.GetSlot(i).Name, i, currentRow!.transform);
        }
    }

    private void CreateConfigButton(string name, int index, Transform parent)
    {
        var colorStr = _cfg.GetSlot(index).Color;
        var text = $"<color={colorStr}>{name}</color>";

        // BSML markup for a styled button (use escaped color tags for XML safety)
        var markup = $"<button text=\"&lt;color={colorStr}&gt;{name}&lt;/color&gt;\" font-size=\"{ButtonFontSize}\" pref-height=\"{ButtonHeight}\" on-click=\"on_slot_click\"/>";

        var host = new ButtonActionHost(() => OnConfigSlotClick(index));
        BSMLParser.Instance.Parse(markup, parent.gameObject, host);
    }

    private void OnConfigSlotClick(int index)
    {
        if (index < 0 || index >= _cfg.SlotCount) return;
        _settingsApplier.Apply(_cfg.GetSlot(index));
    }

    #endregion


    #region Pages

    [UIValue("main_page_active")] private bool IsMainPageActive => _curPage == EPage.Main;
    [UIValue("edit_config_page_active")] private bool IsEditConfigPageActive => _curPage == EPage.EditConfig;

    [UIAction("edit_config_click")]
    private void EditConfig_Click()
    {
        CurPage = EPage.EditConfig;
        RefreshDropdown();
        UpdateActionButtons();
    }

    [UIAction("back_click")]
    private void Back_Click()
    {
        CurPage = EPage.Main;
        RebuildMainPageButtons();
    }

    #endregion


    #region Config Management

    [UIValue("config_options")]
    private List<object> ConfigOptions =>
        _cfg.Slots.Select(s => (object)s.Name).ToList();

    [UIValue("cur_config_index")]
    private int CurConfigDropdownIndex
    {
        get => _curConfigIndex;
        set
        {
            if (value >= 0 && value < _cfg.SlotCount)
                CurConfigIndex = value;
        }
    }

    [UIAction("on_config_dropdown_change")]
    private void OnConfigDropdownChange(string value)
    {
        var index = _cfg.Slots.FindIndex(s => s.Name == value);
        if (index >= 0) CurConfigDropdownIndex = index;
    }

    [UIValue("cur_config_name")]
    private string CurConfigName
    {
        get => _cfg.GetSlot(_curConfigIndex).Name;
        set
        {
            _cfg.SetSlotName(_curConfigIndex, _cfg.GetUniqueName(value, _curConfigIndex));
            RefreshDropdown();
        }
    }

    [UIValue("cur_config_color")]
    private Color CurConfigColor
    {
        get => ColorUtility.TryParseHtmlString(_cfg.GetSlot(_curConfigIndex).Color, out var c) ? c : Color.white;
        set
        {
            _cfg.GetSlot(_curConfigIndex).Color = $"#{ColorUtility.ToHtmlStringRGB(value)}";
            _cfg.Changed();
            UpdateColorButton();
            RebuildMainPageButtons();
        }
    }

    [UIAction("show_color_picker")]
    private void ShowColorPicker()
    {
        _colorPicker?.ModalView?.Show(true);
    }

    [UIAction("on_color_done")]
    private void OnColorDone(Color value)
    {
        CurConfigColor = value;
    }

    private void UpdateColorButton()
    {
        var colorHex = _cfg.GetSlot(_curConfigIndex).Color;
        foreach (var btn in Resources.FindObjectsOfTypeAll<Button>())
        {
            var tmp = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null && (tmp.text == "◉" || tmp.text.Contains("◉")))
            {
                tmp.text = $"<color={colorHex}>◉</color>";
                break;
            }
        }
    }

    [UIAction("show_name_keyboard")]
    private void ShowNameKeyboard()
    {
        _nameKeyboard?.ModalView?.Show(true);
    }

    [UIAction("on_name_entered")]
    private void OnNameEntered(string value)
    {
        _cfg.SetSlotName(_curConfigIndex, _cfg.GetUniqueName(value, _curConfigIndex));
        RefreshDropdown();
        NotifyPropertyChanged(nameof(CurConfigName));
    }

    [UIAction("new_config_click")]
    private void NewConfig_Click()
    {
        var newName = _cfg.GetUniqueName("Config");
        _cfg.AddSlot(newName, PluginConfig.LightConfig.CreateDefault(newName));
        _cfg.Changed();

        CurConfigIndex = _cfg.SlotCount - 1;
        RebuildMainPageButtons();
        RefreshDropdown();
        UpdateActionButtons();
    }

    [UIAction("delete_config_click")]
    private void DeleteConfigClick()
    {
        if (_cfg.SlotCount <= 1) return;

        if (_deleteState == ConfirmState.Default)
        {
            _deleteState = ConfirmState.StartConfirming;
        }
        else
        {
            _deleteState = ConfirmState.Default;
            NotifyPropertyChanged(nameof(DeleteButtonText));
            _cfg.RemoveSlot(_curConfigIndex);
            _cfg.Changed();

            CurConfigIndex = Math.Min(_curConfigIndex, _cfg.SlotCount - 1);
            RebuildMainPageButtons();
            RefreshDropdown();
            UpdateActionButtons();
        }
    }

    [UIAction("move_config_up")]
    private void MoveConfigUp()
    {
        if (_curConfigIndex <= 0) return;
        _cfg.SwapSlots(_curConfigIndex, _curConfigIndex - 1);
        _cfg.Changed();

        CurConfigIndex = _curConfigIndex - 1;
        RebuildMainPageButtons();
        RefreshDropdown();
        UpdateActionButtons();
    }

    [UIAction("move_config_down")]
    private void MoveConfigDown()
    {
        if (_curConfigIndex >= _cfg.SlotCount - 1) return;
        _cfg.SwapSlots(_curConfigIndex, _curConfigIndex + 1);
        _cfg.Changed();

        CurConfigIndex = _curConfigIndex + 1;
        RebuildMainPageButtons();
        RefreshDropdown();
        UpdateActionButtons();
    }

    private void UpdateActionButtons()
    {
        bool canMoveUp = _curConfigIndex > 0;
        bool canMoveDown = _curConfigIndex < _cfg.SlotCount - 1;
        bool canDelete = _cfg.SlotCount > 1;
        bool canAdd = _cfg.SlotCount < PluginConfig.MaxSlotCount;

        foreach (var btn in Resources.FindObjectsOfTypeAll<Button>())
        {
            var tmp = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp == null) continue;

            switch (tmp.text)
            {
                case "▲": btn.interactable = canMoveUp; break;
                case "▼": btn.interactable = canMoveDown; break;
                case "-": case "?": btn.interactable = canDelete; break;
                case "+": btn.interactable = canAdd; break;
            }
        }
    }

    private void RefreshDropdown()
    {
        if (_configDropdown == null) return;

        _configDropdown.Values = ConfigOptions;
        _configDropdown.UpdateChoices();
        if (_curConfigIndex >= 0 && _curConfigIndex < _cfg.SlotCount)
            _configDropdown.Value = _cfg.GetSlot(_curConfigIndex).Name;
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

    // Color Type Override (1.40.0+ only)
    [UIValue("is_version_40_plus")] private bool IsVersion40Plus => _isVersion40Plus;

    [UIValue("color_type_override_options")] private List<object> ColorTypeOverrideOptions =
        [ "All", "Notes Only" ];
    private int ColorTypeOverrideConverter(string value)
    {
        if (value == ColorTypeOverrideOptions[1].ToString()) return 1;
        return 0;
    }
    private string ColorTypeOverrideConverter(int value)
    {
        return value >= ColorTypeOverrideOptions.Count ? ColorTypeOverrideOptions[0].ToString() : ColorTypeOverrideOptions[value].ToString();
    }

    [UIValue("o_color_type_override")] private bool OColorTypeOverride => CurLightConfig.OColorTypeOverride;
    [UIAction("on_o_color_type_override_change")] private void OnOColorTypeOverrideChange(bool value) => CurLightConfig.OColorTypeOverride = value;
    [UIValue("color_type_override")] private string ColorTypeOverride => ColorTypeOverrideConverter(CurLightConfig.ColorTypeOverride);
    [UIAction("on_color_type_override_change")] private void OnColorTypeOverrideChange(string value) => CurLightConfig.ColorTypeOverride = ColorTypeOverrideConverter(value);

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


    #region Extra

    [UIValue("is_extra_available")] private bool IsExtraAvailable =>
        _isJDFixerAvailable || _isNoAutoExposureAvailable;

    // JDFixer Enabled
    [UIValue("is_jd_fixer_available")] private bool IsJDFixerAvailable => _isJDFixerAvailable;
    [UIValue("o_jd_fixer_enabled")] private bool OJDFixerEnabled => CurLightConfig.OJDFixerEnabled;
    [UIAction("on_o_jd_fixer_enabled_change")] private void OnJDFixerEnabledOChange(bool value) => CurLightConfig.OJDFixerEnabled = value;
    [UIValue("jd_fixer_enabled")] private bool JDFixerEnabled => CurLightConfig.JDFixerEnabled;
    [UIAction("on_jd_fixer_enabled_change")] private void OnJDFixerEnabledChange(bool value) => CurLightConfig.JDFixerEnabled = value;

    // NoAutoExposure Enabled
    [UIValue("is_no_auto_exposure_available")] private bool IsNoAutoExposureAvailable => _isNoAutoExposureAvailable;
    [UIValue("o_no_auto_exposure_enabled")] private bool ONoAutoExposureEnabled => CurLightConfig.ONoAutoExposureEnabled;
    [UIAction("on_o_no_auto_exposure_enabled_change")] private void OnNoAutoExposureEnabledOChange(bool value) => CurLightConfig.ONoAutoExposureEnabled = value;
    [UIValue("no_auto_exposure_enabled")] private bool NoAutoExposureEnabled => CurLightConfig.NoAutoExposureEnabled;
    [UIAction("on_no_auto_exposure_enabled_change")] private void OnNoAutoExposureEnabledChange(bool value) => CurLightConfig.NoAutoExposureEnabled = value;

    #endregion


    #endregion


    private void NotifyAllConfigValues()
    {
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
        NotifyPropertyChanged(nameof(OColorTypeOverride));
        NotifyPropertyChanged(nameof(ColorTypeOverride));

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
        NotifyPropertyChanged(nameof(ONoAutoExposureEnabled));
        NotifyPropertyChanged(nameof(NoAutoExposureEnabled));
    }

    private void NotifyPropertyChanged(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
