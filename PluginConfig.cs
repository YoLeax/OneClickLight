using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace OneClickLight;

internal class PluginConfig
{
    internal static PluginConfig? Instance { get; set; }

    public virtual bool Enabled { get; set; } = false;
    
    // Members must be 'virtual' if you want BSIPA to detect a value change and save the config automatically
    // You can assign a default value to be used when the config is first created by assigning one after '=' 
    // examples:
    // public virtual bool FeatureEnabled { get; set; } = true;
    // public virtual int NumValue { get; set; } = 42;
    // public virtual Color TheColor { get; set; } = new Color(0.12f, 0.34f, 0.56f);

    /*
    /// <summary>
    /// This is called whenever BSIPA reads the config from disk (including when file changes are detected).
    /// </summary>
    public virtual void OnReload() { }
    */

    /// <summary>
    /// Call this to force BSIPA to update the config file. This is also called by BSIPA if it detects the file was modified.
    /// </summary>
    public virtual void Changed() { }

    /*
    /// <summary>
    /// Call this to have BSIPA copy the values from <paramref name="other"/> into this config.
    /// </summary>
    public virtual void CopyFrom(PluginConfig other) { }
    */

    public class LightCfg
    {
        public virtual string Name { get; set; } = "New Config";
        
        // BaseGame

        public virtual EnvironmentEffectsFilterPreset EnvironmentEffects { get; set; } =
            EnvironmentEffectsFilterPreset.AllEffects;
        
        public virtual EnvironmentEffectsFilterPreset EpEnvironmentEffects { get; set; } =
            EnvironmentEffectsFilterPreset.AllEffects;
        
        public virtual bool NoTextsOrHUDs { get; set; } = false;
        
        public virtual bool AdvancedHUD { get; set; } = false;
        
        public virtual ArcVisibilityType ArcVisibility { get; set; } = ArcVisibilityType.Standard;

        public virtual bool OverrideDefaultEnvironments { get; set; } = false;

        public virtual bool OverrideDefaultColors { get; set; } = false;
        
        // SongCore
        
        public virtual bool AllowCustomSongNoteColors { get; set; } = false;
        
        public virtual bool AllowCustomSongObstacleColors { get; set; } = false;
        
        public virtual bool AllowCustomSongEnvironmentColors { get; set; } = false;
        
        // Chroma

        public virtual bool ChromaUseCustomEnvironment { get; set; } = false;

        public virtual bool ChromaDisableEnvironmentEnhancements { get; set; } = false;
        
        public virtual bool ChromaDisableNoteColoring { get; set; } = false;
        
        public virtual bool ChromaDisableChromaEvents { get; set; } = false;
        
        public virtual bool ChromaForceZenModeWalls { get; set; } = false;
        
        // JDFixer
        
        public virtual bool JDFixerEnabled { get; set; } = false;
    }
}