using ECommons.DalamudServices;
using ECommons.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using WrathCombo.Services;
using Debug = WrathCombo.Window.Tabs.Debug;

namespace WrathCombo.Core;

public partial class Configuration
{
    internal void SetActionChanging(bool? newValue = null)
    {
        if (newValue is not null && newValue != ActionChanging)
        {
            ActionChanging = newValue.Value;
            Save();
        }

        // Checks if action replacing is not in line with the setting
        if (ActionChanging && !Service.ActionReplacer.getActionHook.IsEnabled)
            Service.ActionReplacer.getActionHook.Enable();
        if (!ActionChanging && Service.ActionReplacer.getActionHook.IsEnabled)
            Service.ActionReplacer.getActionHook.Disable();
    }

    #region Saving

    /// <summary>
    ///     The queue of items to be saved.
    /// </summary>
    internal static readonly Queue<(Configuration, StackTrace)> SaveQueue = [];

    /// <summary>
    ///     Whether an item is currently being saved.
    /// </summary>
    private static bool _isSaving;

    /// <summary>
    ///     Process the <see cref="SaveQueue"/>, trying to save each item.
    /// </summary>
    /// <seealso cref="Save"/>
    internal static void ProcessSaveQueue()
    {
        if (_isSaving || SaveQueue.Count == 0) return;

        _isSaving = true;
        var (config, trace) = SaveQueue.Dequeue();

        if (Debug.DebugConfig)
        {
            PluginLog.Warning(
                $"[Saving] Saving attempted when we shouldn't!\n{trace}");
            _isSaving = false;
            return;
        }

        try
        {
            PluginLog.Verbose(
                "[Saving] Attempting to save ...");
            Svc.PluginInterface.SavePluginConfig(config);
            _isSaving = false;
            PluginLog.Verbose(
                $"[Saving] Saved (queue size now: {SaveQueue.Count})");
        }
        catch (Exception)
        {
            Svc.Framework.Run(() => RetrySave(config, trace));
        }
    }

    internal static void RetrySave
        (Configuration config, StackTrace trace)
    {
        var success = false;
        var retryCount = 0;

        if (Debug.DebugConfig)
        {
            PluginLog.Warning(
                $"[Saving] Saving attempted when we shouldn't!\n{trace}");
            _isSaving = false;
            return;
        }

        while (!success)
        {
            try
            {
                PluginLog.Verbose(
                    $"[Saving] Retrying save ... (attempt {retryCount})");
                Svc.PluginInterface.SavePluginConfig(config);
                success = true;
                PluginLog.Verbose(
                    $"[Saving] Saved (queue size now: {SaveQueue.Count})");
            }
            catch (Exception e)
            {
                retryCount++;
                if (retryCount < 3)
                {
                    Task.Delay(20).Wait();
                    continue;
                }

                PluginLog.Error(
                    "[Saving] Failed to save configuration after 3 retries.\n" +
                    e.Message + "\n" + trace);
                _isSaving = false;
                return;
            }
        }

        _isSaving = false;
    }

    /// <summary> Set the configuration to be saved to disk. </summary>
    /// <remarks>
    ///     Configurations set to be saved will be processed in the order they
    ///     were added, each frame.
    /// </remarks>
    /// <seealso cref="SaveQueue"/>
    public void Save()
    {
        if (Debug.DebugConfig)
            return;

        SaveQueue.Enqueue((this, new StackTrace()));
        PluginLog.Verbose(
            $"[Saving] Save queued (queue size: {SaveQueue.Count})");
    }

    #endregion

    #region Preset Resetting

    [JsonProperty]
    private static Dictionary<string, bool> ResetFeatureCatalog { get; set; } = [];

    private static bool GetResetValues(string config)
    {
        if (ResetFeatureCatalog.TryGetValue(config, out var value)) return value;

        return false;
    }

    private static void SetResetValues(string config, bool value)
    {
        ResetFeatureCatalog[config] = value;
    }

    public void ResetFeatures(string config, int[] values)
    {
        Svc.Log.Debug($"{config} {GetResetValues(config)}");
        if (!GetResetValues(config))
        {
            bool needToResetMessagePrinted = false;

            foreach (int value in values)
            {
                Svc.Log.Debug(value.ToString());

                var preset = (Preset)value;

                if (!PresetStorage.AllPresets.TryGetValue(preset, out var presetData))
                    continue;

                // If not found, skip
                if (!PresetStorage.AllPresets.ContainsKey(preset))
                    continue;

                if (!PresetStorage.IsEnabled(preset))
                    continue;

                if (!needToResetMessagePrinted)
                {
                    DuoLog.Error($"Some features have been disabled due to an internal configuration update:");
                    needToResetMessagePrinted = !needToResetMessagePrinted;
                }

                DuoLog.Error($"- {presetData.JobInfo.JobName}: {presetData.Name}");
                EnabledActions.Remove(preset);
            }

            if (needToResetMessagePrinted)
                DuoLog.Error($"Please re-enable these features to use them again. We apologise for the inconvenience");
        }
        SetResetValues(config, true);
        Save();
    }

    #endregion

    #region UserConfig Method Access

    #region Custom Floats

    /// <summary> Gets a custom float value. </summary>
    public static float GetCustomFloatValue(string config, float value = 0)
    {
        if (!CustomFloatValues.TryGetValue(config, out float configValue))
        {
            SetCustomFloatValue(config, value, true);
            return value;
        }

        return configValue;
    }

    /// <summary> Sets a custom float value. </summary>
    /// <returns> The Set value.</returns>
    public static float SetCustomFloatValue
        (string config, float value, bool shouldBatch = false)
    {
        CustomFloatValues[config] = value;

        Service.Configuration.TriggerUserConfigChanged(
            ConfigChangeType.UserData, ConfigChangeSource.UI,
            config, value);

        // todo: add batching logic, for initial plugin loading

        Service.Configuration.Save();
        return value;
    }

    #endregion

    #region Custom Ints

    /// <summary> Gets a custom integer value. </summary>
    public static int GetCustomIntValue(string config, int value = 0)
    {
        if (!CustomIntValues.TryGetValue(config, out int configValue))
        {
            SetCustomIntValue(config, value, true);
            return value;
        }

        return configValue;
    }

    /// <summary> Sets a custom integer value. </summary>
    /// <returns> The Set value.</returns>
    public static int SetCustomIntValue
        (string config, int value, bool shouldBatch = false)
    {
        CustomIntValues[config] = value;

        Service.Configuration.TriggerUserConfigChanged(
            ConfigChangeType.UserData, ConfigChangeSource.UI,
            config, value);

        // todo: add batching logic, for initial plugin loading

        Service.Configuration.Save();
        return value;
    }

    #endregion

    #region Custom Bools

    /// <summary> Gets a custom boolean value. </summary>
    public static bool GetCustomBoolValue(string config)
    {
        if (!CustomBoolValues.TryGetValue(config, out bool configValue))
        {
            SetCustomBoolValue(config, false, true);
            return false;
        }

        return configValue;
    }

    /// <summary> Sets a custom boolean value. </summary>
    /// <returns> The Set value.</returns>
    public static bool SetCustomBoolValue
        (string config, bool value, bool shouldBatch = false)
    {
        CustomBoolValues[config] = value;

        Service.Configuration.TriggerUserConfigChanged(
            ConfigChangeType.UserData, ConfigChangeSource.UI,
            config, value);

        // todo: add batching logic, for initial plugin loading

        Service.Configuration.Save();
        return value;
    }

    #endregion

    #region Custom Int Arrays

    /// <summary> Gets a custom integer array value. </summary>
    public static int[] GetCustomIntArrayValue(string config)
    {
        if (!CustomIntArrayValues.TryGetValue(config, out int[]? configValue))
        {
            SetCustomIntArrayValue(config, [], true);
            return [];
        }

        return configValue;
    }

    /// <summary> Sets a custom integer array value. </summary>
    /// <returns> The Set value.</returns>
    public static int[] SetCustomIntArrayValue
        (string config, int[] value, bool shouldBatch = false)
    {
        CustomIntArrayValues[config] = value;

        Service.Configuration.TriggerUserConfigChanged(
            ConfigChangeType.UserData, ConfigChangeSource.UI,
            config, value);

        // todo: add batching logic, for initial plugin loading

        Service.Configuration.Save();
        return value;
    }

    #endregion

    #region Custom Bool Arrays

    /// <summary> Gets a custom boolean array value. </summary>
    public static bool[] GetCustomBoolArrayValue(string config)
    {
        if (!CustomBoolArrayValues.TryGetValue(config, out bool[]? configValue))
        {
            SetCustomBoolArrayValue(config, [], true);
            return [];
        }

        return configValue;
    }

    /// <summary> Sets a custom boolean array value. </summary>
    /// <returns> The Set value.</returns>
    public static bool[] SetCustomBoolArrayValue
        (string config, bool[] value, bool shouldBatch = false)
    {
        CustomBoolArrayValues[config] = value;

        Service.Configuration.TriggerUserConfigChanged(
            ConfigChangeType.UserData, ConfigChangeSource.UI,
            config, value);

        // todo: add batching logic, for initial plugin loading

        Service.Configuration.Save();
        return value;
    }

    #endregion

    #endregion

    #region Config Import

    /// <summary>
    ///     Whether a WrathCombo config import has already been performed.
    /// </summary>
    public static bool HasImportedFromWrathCombo { get; set; }

    /// <summary>
    ///     Timestamp of the last WrathCombo config import, or null if never imported.
    /// </summary>
    public static DateTime? LastWrathComboImportTime { get; set; }

    /// <summary>
    ///     Imports compatible settings from a WrathCombo config file into the
    ///     current ParseLord5 configuration. Read-only on the WrathCombo source.
    /// </summary>
    /// <returns>True if import succeeded, false otherwise.</returns>
    public static bool ImportFromWrathCombo()
    {
        try
        {
            // Locate WrathCombo config file
            var configDir = Path.GetDirectoryName(
                Svc.PluginInterface.ConfigFile.FullName);
            var wrathPath = Path.Combine(configDir!, "WrathCombo.json");

            if (!File.Exists(wrathPath))
            {
                PluginLog.Information(
                    "[ParseLord5] WrathCombo config not found at " +
                    $"{wrathPath}. Nothing to import.");
                return false;
            }

            // Read and deserialize WrathCombo config
            var json = File.ReadAllText(wrathPath);
            var wrathConfig = JsonConvert.DeserializeObject<Configuration>(json);
            if (wrathConfig is null)
            {
                PluginLog.Warning(
                    "[ParseLord5] Failed to deserialize WrathCombo config.");
                return false;
            }

            var target = Service.Configuration;

            // Combo selections (replace, don't merge)
            target.EnabledActions.Clear();
            foreach (var preset in wrathConfig.EnabledActions)
                target.EnabledActions.Add(preset);

            // Auto-rotation config
            target.RotationConfig = wrathConfig.RotationConfig;

            // Healing target stack / retargeting settings. These directly affect
            // healer behavior and are safe to mirror from the source config.
            target.RetargetHealingActionsToStack =
                wrathConfig.RetargetHealingActionsToStack;
            target.AddOutOfPartyNPCsToRetargeting =
                wrathConfig.AddOutOfPartyNPCsToRetargeting;
            target.UseUIMouseoverOverridesInDefaultHealStack =
                wrathConfig.UseUIMouseoverOverridesInDefaultHealStack;
            target.UseFieldMouseoverOverridesInDefaultHealStack =
                wrathConfig.UseFieldMouseoverOverridesInDefaultHealStack;
            target.UseFocusTargetOverrideInDefaultHealStack =
                wrathConfig.UseFocusTargetOverrideInDefaultHealStack;
            target.UseLowestHPOverrideInDefaultHealStack =
                wrathConfig.UseLowestHPOverrideInDefaultHealStack;
            target.UseCustomHealStack = wrathConfig.UseCustomHealStack;
            target.CustomHealStack = wrathConfig.CustomHealStack;
            target.RaiseStack = wrathConfig.RaiseStack;

            // Ignored NPCs
            target.IgnoredNPCs.Clear();
            foreach (var (npcId, reason) in wrathConfig.IgnoredNPCs)
                target.IgnoredNPCs[npcId] = reason;

            // Blue Mage spells
            target.ActiveBLUSpells = wrathConfig.ActiveBLUSpells;

            // Dancer dance steps
            target.DancerDanceCompatActionIDs =
                wrathConfig.DancerDanceCompatActionIDs;

            // Status blacklist
            target.StatusBlacklist.Clear();
            foreach (var entry in wrathConfig.StatusBlacklist)
                target.StatusBlacklist.Add(entry);

            ImportCustomValueMaps(json);

            // --- Fields intentionally NOT imported ---
            // Version: uses ParseLord5's own version.
            // AprilFools2026, UI-only settings: plugin-specific, not gameplay.

            // Record import
            HasImportedFromWrathCombo = true;
            LastWrathComboImportTime = DateTime.UtcNow;

            // Persist
            target.Save();

            PluginLog.Information(
                "[ParseLord5] Successfully imported settings from WrathCombo.");
            return true;
        }
        catch (Exception ex)
        {
            PluginLog.Error(
                $"[ParseLord5] Config import failed: {ex.Message}");
            return false;
        }
    }

    private static void ImportCustomValueMaps(string sourceJson)
    {
        var source = JObject.Parse(sourceJson);

        CustomFloatValues =
            ReadDictionary<float>(source, "CustomFloatValuesV6");
        CustomIntValues =
            ReadDictionary<int>(source, "CustomIntValuesV6");
        CustomIntArrayValues =
            ReadDictionary<int[]>(source, "CustomIntArrayValuesV6");
        CustomBoolValues =
            ReadDictionary<bool>(source, "CustomBoolValuesV6");
        CustomBoolArrayValues =
            ReadDictionary<bool[]>(source, "CustomBoolArrayValuesV6");
    }

    private static Dictionary<string, TValue> ReadDictionary<TValue>(
        JObject source, string propertyName)
    {
        return source[propertyName]?.ToObject<Dictionary<string, TValue>>() ??
               [];
    }

    #endregion
}
