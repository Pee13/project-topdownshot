using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace TopDownTacticalAI.EditorTools
{
    /// <summary>
    /// One-shot editor script that creates the AddressableAssetSettings,
    /// two groups (Preload_MainMenu, Preload_Level1), and adds the heavy
    /// assets used by the splash + main menu + level 1 flow.
    ///
    /// Run via: Tools > TopDownTacticalAI > Setup Addressables Groups
    /// Or programmatically via: AddressablesBootstrapper.Run().
    /// </summary>
    public static class AddressablesBootstrapper
    {
        // Assets to pre-warm. Paths are Assets/-relative.
        private static readonly (string group, string label, string path)[] Entries =
        {
            // Group 1: Preload_MainMenu (label = "mainmenu")
            ("Preload_MainMenu", "mainmenu", "Assets/UI/Splash/newLogo_mahidol.png"),
            ("Preload_MainMenu", "mainmenu", "Assets/UI/Splash/logoมหาลัย.png"),
            ("Preload_MainMenu", "mainmenu", "Assets/UI/Splash/logoCss.png"),
            ("Preload_MainMenu", "mainmenu", "Assets/UI/Splash/Official_unity_logo.png"),
            ("Preload_MainMenu", "mainmenu", "Assets/หน้าเกมui/หน้าหลัก/พื้นหลัง.png"),
            ("Preload_MainMenu", "mainmenu", "Assets/Prefabs/UI/SettingsPanel.prefab"),
            ("Preload_MainMenu", "mainmenu", "Assets/Scenes/MainMenu.unity"),

            // Group 2: Preload_Level1 (label = "level1")
            ("Preload_Level1", "level1", "Assets/Sprites/fire_dash_spritesheet.png"),
            ("Preload_Level1", "level1", "Assets/Sprites/fire_dash_spritesheet_v3.png"),
            ("Preload_Level1", "level1", "Assets/Sprites/player_modular_parts_sheet_1.png"),
            ("Preload_Level1", "level1", "Assets/Sprites/player_modular_parts_sheet_2.png"),
            ("Preload_Level1", "level1", "Assets/Sprites/player_back_pipe.png"),
            ("Preload_Level1", "level1", "Assets/Sprites/player_back_pipe_blue.png"),
            ("Preload_Level1", "level1", "Assets/Sprites/PlayerModularParts_Extracted/player_core_body.png"),
            ("Preload_Level1", "level1", "Assets/Scenes/Level1.unity"),
        };

        [MenuItem("Tools/TopDownTacticalAI/Setup Addressables Groups")]
        public static void Run()
        {
            // 1. Get or create the settings asset.
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                Debug.Log("[AddressablesBootstrapper] Created AddressableAssetSettings.");
            }
            else
            {
                Debug.Log("[AddressablesBootstrapper] Using existing AddressableAssetSettings.");
            }

            // 2. Track groups and which labels to add to them.
            var groupCache = new Dictionary<string, AddressableAssetGroup>();
            int addedCount = 0;
            int skippedCount = 0;

            foreach (var entry in Entries)
            {
                // Find or create the group.
                if (!groupCache.TryGetValue(entry.group, out var group))
                {
                    group = settings.FindGroup(entry.group);
                    if (group == null)
                    {
                        group = settings.CreateGroup(
                            entry.group,
                            setAsDefaultGroup: false,
                            readOnly: false,
                            postEvent: true,
                            schemasToCopy: null,
                            types: new[] { typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema) });
                        Debug.Log("[AddressablesBootstrapper] Created group: " + entry.group);
                    }
                    groupCache[entry.group] = group;
                }

                // Add the asset as an entry.
                string guid = AssetDatabase.AssetPathToGUID(entry.path);
                if (string.IsNullOrEmpty(guid))
                {
                    Debug.LogWarning("[AddressablesBootstrapper] Asset not found, skipped: " + entry.path);
                    skippedCount++;
                    continue;
                }

                var e = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: true);
                if (e == null)
                {
                    Debug.LogWarning("[AddressablesBootstrapper] Failed to add entry: " + entry.path);
                    skippedCount++;
                    continue;
                }

                // Apply label.
                e.SetLabel(entry.label, true, true, false);
                addedCount++;
            }

            // 3. Set labels enum (optional but recommended for code-side usage).
            settings.AddLabel("mainmenu", false);
            settings.AddLabel("level1", false);

            // 4. Save.
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AddressablesBootstrapper] Done. Added: {addedCount}, Skipped: {skippedCount}. " +
                      "Open Window > Asset Management > Addressables > Groups to verify.");
        }
    }
}
