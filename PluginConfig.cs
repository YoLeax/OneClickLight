using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace OneClickLight;

internal class PluginConfig
{
    internal static PluginConfig? Instance { get; set; }
    internal const int MaxSlotCount = 12;

    public virtual bool NotInitialized { get; set; } = true;

    [UseConverter(typeof(ListConverter<LightConfig>))]
    [NonNullable]
    public virtual List<LightConfig> Slots { get; set; } = new();

    public virtual void Changed() { }

    internal int SlotCount => Slots.Count;

    internal LightConfig GetSlot(int index) =>
        index >= 0 && index < Slots.Count ? Slots[index] : new LightConfig();

    internal void SetSlotName(int index, string name)
    {
        if (index >= 0 && index < Slots.Count) Slots[index].Name = name;
    }

    /// <summary>Find a unique name by appending (n) if needed. excludeIndex skips a slot (for renaming).</summary>
    internal string GetUniqueName(string baseName, int excludeIndex = -1)
    {
        bool Exists(string name) => Slots.Where((s, i) => i != excludeIndex).Any(s => s.Name == name);
        if (!Exists(baseName)) return baseName;
        int n = 2;
        while (Exists($"{baseName} {n}")) n++;
        return $"{baseName} {n}";
    }

    internal void AddSlot(string name, LightConfig config)
    {
        if (Slots.Count >= MaxSlotCount) return;
        Slots.Add(config);
        Slots[Slots.Count - 1].Name = name;
    }

    internal void RemoveSlot(int index)
    {
        if (index >= 0 && index < Slots.Count) Slots.RemoveAt(index);
    }

    internal void SwapSlots(int indexA, int indexB)
    {
        if (indexA < 0 || indexA >= Slots.Count || indexB < 0 || indexB >= Slots.Count) return;
        (Slots[indexA], Slots[indexB]) = (Slots[indexB], Slots[indexA]);
    }

    internal void Init()
    {
        if (Slots.Count == 0)
        {
            Slots = new List<LightConfig>
            {
                LightConfig.CreateDefaultOn(),
                LightConfig.CreateDefaultHalfOn(),
                LightConfig.CreateDefaultOff(),
            };
        }

        NotInitialized = false;

        // Enforce max slot count (handles manually edited JSON)
        if (Slots.Count > MaxSlotCount)
            Slots.RemoveRange(MaxSlotCount, Slots.Count - MaxSlotCount);
    }

    public class LightConfig
    {
        // ── Factory ──

        public static LightConfig CreateDefault(string name) => new LightConfig { Name = name };

        /// <summary>Default ON: all effects enabled, permissive settings.</summary>
        public static LightConfig CreateDefaultOn() => new LightConfig {
            Name = "ON",
            Color = "#00b616"
        };

        public static LightConfig CreateDefaultOff()
        {
            return new LightConfig
            {
                Name = "OFF",
                Color = "#c81717",
                EnvironmentEffects = EnvironmentEffectsFilterPreset.NoEffects,
                EpEnvironmentEffects = EnvironmentEffectsFilterPreset.NoEffects,
                NoTextsOrHUDs = false,
                AdvancedHUD = true,
                OverrideDefaultEnvironments = true,
                OverrideDefaultColors = true,
                AllowCustomSongNoteColors = false,
                AllowCustomSongObstacleColors = false,
                AllowCustomSongEnvironmentColors = false,
                ChromaDisableEnvironmentEnhancements = true,
                ChromaDisableNoteColoring = true,
                ChromaDisableChromaEvents = true,
            };
        }

        public static LightConfig CreateDefaultHalfOn()
        {
            return new LightConfig
            {
                Name = "Half-ON",
                Color = "#268ED2",
                NoTextsOrHUDs = false,
                AdvancedHUD = true,
                OverrideDefaultColors = typeof(ColorSchemesSettings).GetProperty("colorOverrideType") != null,  // 1.40+ override notes color only
                ColorTypeOverride = 1,
                AllowCustomSongNoteColors = false,
            };
        }

        // Identity

        public virtual string Name { get; set; } = "";
        public virtual string Color { get; set; } = "#ffffff";

        // BaseGame

        public virtual bool OEnvironmentEffects { get; set; } = true;
        public virtual EnvironmentEffectsFilterPreset EnvironmentEffects { get; set; } =
            EnvironmentEffectsFilterPreset.AllEffects;

        public virtual bool OEpEnvironmentEffects { get; set; } = true;
        public virtual EnvironmentEffectsFilterPreset EpEnvironmentEffects { get; set; } =
            EnvironmentEffectsFilterPreset.AllEffects;

        public virtual bool ONoTextsOrHUDs { get; set; } = true;
        public virtual bool NoTextsOrHUDs { get; set; } = true;

        public virtual bool OAdvancedHUD { get; set; } = true;
        public virtual bool AdvancedHUD { get; set; } = false;

        public virtual bool OArcVisibility { get; set; } = false;
        public virtual ArcVisibilityType ArcVisibility { get; set; } = ArcVisibilityType.Standard;

        public virtual bool OOverrideDefaultEnvironments { get; set; } = true;
        public virtual bool OverrideDefaultEnvironments { get; set; } = false;

        public virtual bool OOverrideDefaultColors { get; set; } = true;
        public virtual bool OverrideDefaultColors { get; set; } = false;

        /// <summary>Only available in Beat Saber 1.40.0+. 0=All, 1=NotesOnly.</summary>
        public virtual bool OColorTypeOverride { get; set; } = true;
        public virtual int ColorTypeOverride { get; set; } = 0; // 0=All, 1=NotesOnly

        // SongCore

        public virtual bool OAllowCustomSongNoteColors { get; set; } = true;
        public virtual bool AllowCustomSongNoteColors { get; set; } = true;

        public virtual bool OAllowCustomSongObstacleColors { get; set; } = true;
        public virtual bool AllowCustomSongObstacleColors { get; set; } = true;

        public virtual bool OAllowCustomSongEnvironmentColors { get; set; } = true;
        public virtual bool AllowCustomSongEnvironmentColors { get; set; } = true;

        // Chroma

        public virtual bool OChromaUseCustomEnvironment { get; set; } = false;
        public virtual bool ChromaUseCustomEnvironment { get; set; } = false;

        public virtual bool OChromaDisableEnvironmentEnhancements { get; set; } = true;
        public virtual bool ChromaDisableEnvironmentEnhancements { get; set; } = false;

        public virtual bool OChromaDisableNoteColoring { get; set; } = true;
        public virtual bool ChromaDisableNoteColoring { get; set; } = false;

        public virtual bool OChromaDisableChromaEvents { get; set; } = true;
        public virtual bool ChromaDisableChromaEvents { get; set; } = false;

        public virtual bool OChromaForceZenModeWalls { get; set; } = false;
        public virtual bool ChromaForceZenModeWalls { get; set; } = false;

        // Extra

        public virtual bool OJDFixerEnabled { get; set; } = false;
        public virtual bool JDFixerEnabled { get; set; } = false;

        public virtual bool ONoAutoExposureEnabled { get; set; } = false;
        public virtual bool NoAutoExposureEnabled { get; set; } = false;
    }
}
