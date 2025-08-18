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

internal class GameplayMenu : IInitializable, IDisposable
{
    private const string MenuName = "One Click Light";
    private const string ResourcePath = "OneClickLight.Menu.gameplayMenu.bsml";
    
    private readonly PluginConfig _cfg;
    
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

    [UIValue("enabled")] private bool Enabled => _cfg.Enabled;
    [UIAction("on_enabled_change")]
    private void OnEnabledChange(bool value)
    {
        _cfg.Enabled = value;
    }
}