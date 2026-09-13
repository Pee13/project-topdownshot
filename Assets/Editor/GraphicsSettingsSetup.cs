using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using TopDownTacticalAI.UI;

namespace TopDownTacticalAI.EditorTools
{
    /// <summary>
    /// Editor-only tool that adds the new Graphics &amp; Effects rows to any
    /// SettingsPanel that has a <see cref="SettingsPanelBinder"/> attached.
    ///
    /// Adds:
    ///   - EFFECTSQUALITYRow   (Slider, 0..1)
    ///   - MENUVFXRow          (Toggle)
    ///   - BACKGROUNDVFXRow    (Toggle)
    ///   - SCREENEFFECTSRow    (Toggle)
    ///
    /// Run once from: Tools → TopDownTacticalAI → Setup Graphics &amp; Effects
    /// </summary>
    public static class GraphicsSettingsSetup
    {
        // ── Menu entry ──────────────────────────────────────────────────────
        [MenuItem("Tools/TopDownTacticalAI/Setup Graphics and Effects", false, 200)]
        private static void RunSetup()
        {
            int done = 0;

            // Process every scene (not just active) so prefabs used across scenes get updated too
            foreach (var sceneGUID in AssetDatabase.FindAssets("t:Scene"))
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGUID);
                if (!System.IO.File.Exists(scenePath)) continue;

                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);

                done += ProcessScene();
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }

            // Also process SettingsPanel.prefab directly
            var prefabPath = "Assets/Scenes/Panel/SettingsPanel.prefab";
            done += ProcessPrefab(prefabPath);

            Debug.Log($"[GraphicsSettingsSetup] Done — updated {done} SettingsPanel(s).");
        }

        // ── Scene logic ─────────────────────────────────────────────────────
        private static int ProcessScene()
        {
            int done = 0;
            var binders = UnityEngine.Object.FindObjectsByType<SettingsPanelBinder>(
                UnityEngine.FindObjectsSortMode.None);

            foreach (var binder in binders)
            {
                var panel = binder.gameObject;
                if (panel == null) continue;

                // Check if already upgraded (has our new fields wired)
                if (binder.effectsQualitySlider != null) continue;

                var root = panel.transform;
                bool changed = false;

                // Find QUALITYRow to anchor new rows after it (or FULLSCREENRow as fallback)
                Transform anchor = root.Find("QUALITYRow");
                if (anchor == null) anchor = root.Find("FULLSCREENRow");
                if (anchor == null) anchor = root.Find("MASTERVOLUMERow");

                if (anchor == null)
                {
                    Debug.LogWarning("[GraphicsSettingsSetup] Can't find QUALITYRow/FULLSCREENRow in " +
                        $"SettingsPanel '{panel.scene.name}/{panel.name}' — skipping.");
                    continue;
                }

                var theme = FindTheme(panel);
                changed = AddEffectsQualityRow(root, anchor, theme) || changed;
                changed = AddToggleRow(root, anchor, "MENUVFXRow", "Menu VFX", "menuVFXToggle", theme) || changed;
                changed = AddToggleRow(root, anchor, "BACKGROUNDVFXRow", "Background VFX", "backgroundVFXToggle", theme) || changed;
                changed = AddToggleRow(root, anchor, "SCREENEFFECTSRow", "Screen Effects", "screenEffectsToggle", theme) || changed;

                UnityEditor.EditorUtility.SetDirty(binder);
                UnityEditor.EditorUtility.SetDirty(panel);
                done++;
            }
            return done;
        }

        // ── Prefab logic ────────────────────────────────────────────────────
        private static int ProcessPrefab(string path)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return 0;

            var binder = prefab.GetComponent<SettingsPanelBinder>();
            if (binder == null) return 0;
            if (binder.effectsQualitySlider != null) return 0; // already upgraded

            var root = prefab.transform;
            Transform anchor = root.Find("QUALITYRow");
            if (anchor == null) anchor = root.Find("FULLSCREENRow");
            if (anchor == null) anchor = root.Find("MASTERVOLUMERow");
            if (anchor == null) return 0;

            var theme = FindTheme(prefab);
            AddEffectsQualityRow(root, anchor, theme);
            AddToggleRow(root, anchor, "MENUVFXRow", "Menu VFX", "menuVFXToggle", theme);
            AddToggleRow(root, anchor, "BACKGROUNDVFXRow", "Background VFX", "backgroundVFXToggle", theme);
            AddToggleRow(root, anchor, "SCREENEFFECTSRow", "Screen Effects", "screenEffectsToggle", theme);

            UnityEditor.EditorUtility.SetDirty(binder);
            UnityEditor.EditorUtility.SetDirty(prefab);
            UnityEditor.AssetDatabase.SaveAssets();
            return 1;
        }

        // ── UI builders ─────────────────────────────────────────────────────
        private static bool AddEffectsQualityRow(Transform root, Transform anchor, Object themeOrNull)
        {
            if (root.Find("EFFECTSQUALITYRow") != null) return false;

            float yOffset = -40f;
            var row = MakeRow(root, "EFFECTSQUALITYRow", anchor, yOffset);
            MakeLabel(row, "Effects Quality", themeOrNull);

            // Slider
            var sliderGO = new GameObject("Slider", typeof(RectTransform));
            sliderGO.transform.SetParent(row.transform, false);
            var rt = sliderGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(500f, 40f);

            var slider = sliderGO.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };

            // Background
            var bg = new GameObject("Background", typeof(RectTransform));
            bg.transform.SetParent(sliderGO.transform, false);
            var bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;
            bgRT.anchoredPosition = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            // Fill Area + Handle
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGO.transform, false);
            var faRT = fillArea.GetComponent<RectTransform>();
            faRT.anchorMin = Vector2.zero;
            faRT.anchorMax = Vector2.one;
            faRT.sizeDelta = new Vector2(-10f, 0f);
            faRT.anchoredPosition = Vector2.zero;

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGO.transform, false);
            var haRT = handleArea.GetComponent<RectTransform>();
            haRT.anchorMin = Vector2.zero;
            haRT.anchorMax = Vector2.one;
            haRT.sizeDelta = Vector2.zero;
            haRT.anchoredPosition = Vector2.zero;

            var fillImg = new GameObject("Fill", typeof(RectTransform));
            fillImg.transform.SetParent(fillArea.transform, false);
            var fiRT = fillImg.GetComponent<RectTransform>();
            fiRT.anchorMin = Vector2.zero;
            fiRT.anchorMax = new Vector2(0f, 1f);
            fiRT.sizeDelta = Vector2.zero;
            fiRT.anchoredPosition = Vector2.zero;
            var fiImg = fillImg.AddComponent<Image>();
            fiImg.color = new Color(0.3f, 0.7f, 1f, 1f);
            slider.fillRect = fiRT;

            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(handleArea.transform, false);
            var hRT = handle.GetComponent<RectTransform>();
            hRT.anchorMin = new Vector2(0.5f, 0.5f);
            hRT.anchorMax = new Vector2(0.5f, 0.5f);
            hRT.pivot = new Vector2(0.5f, 0.5f);
            hRT.sizeDelta = new Vector2(24f, 24f);
            hRT.anchoredPosition = Vector2.zero;
            var hImg = handle.AddComponent<Image>();
            hImg.color = Color.white;
            slider.handleRect = hRT;
            slider.targetGraphic = hImg;

            return true;
        }

        private static bool AddToggleRow(Transform root, Transform anchor, string rowName,
            string labelText, string fieldName, Object themeOrNull)
        {
            if (root.Find(rowName) != null) return false;

            float yOffset = -40f;
            var row = MakeRow(root, rowName, anchor, yOffset);
            MakeLabel(row, labelText, themeOrNull);

            // Toggle
            var toggleGO = new GameObject("Toggle", typeof(RectTransform));
            toggleGO.transform.SetParent(row.transform, false);
            var rt = toggleGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(60f, 30f);

            var toggle = toggleGO.AddComponent<Toggle>();
            toggle.isOn = true;
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };

            // Background
            var bg = new GameObject("Background", typeof(RectTransform));
            bg.transform.SetParent(toggleGO.transform, false);
            var bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0f, 0.25f);
            bgRT.anchorMax = new Vector2(1f, 0.75f);
            bgRT.sizeDelta = Vector2.zero;
            bgRT.anchoredPosition = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.25f, 0.25f, 0.25f, 1f);

            // Checkmark
            var checkmark = new GameObject("Checkmark", typeof(RectTransform));
            checkmark.transform.SetParent(toggleGO.transform, false);
            var cmRT = checkmark.GetComponent<RectTransform>();
            cmRT.anchorMin = new Vector2(0.25f, 0.25f);
            cmRT.anchorMax = new Vector2(0.75f, 0.75f);
            cmRT.sizeDelta = Vector2.zero;
            cmRT.anchoredPosition = Vector2.zero;
            var cmImg = checkmark.AddComponent<Image>();
            cmImg.color = new Color(0.3f, 0.9f, 1f, 1f);
            var cmSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
            if (cmSprite != null) cmImg.sprite = cmSprite;
            toggle.graphic = cmImg;
            toggle.targetGraphic = bgImg;

            return true;
        }

        // ── Shared helpers ──────────────────────────────────────────────────
        private static GameObject MakeRow(Transform root, string name, Transform anchor, float yOffset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(root, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(850f, 50f);

            // Place right after the anchor
            int anchorIndex = anchor.GetSiblingIndex();
            go.transform.SetSiblingIndex(anchorIndex + 1);

            // Shift Y position down by yOffset per row (each new row is offset by yOffset from previous)
            // We'll position based on sibling order - the anchor's position + yOffset
            var anchorRT = anchor as RectTransform;
            if (anchorRT != null)
            {
                rt.anchoredPosition = anchorRT.anchoredPosition + new Vector2(0, yOffset);
            }

            return go;
        }

        private static void MakeLabel(GameObject row, string text, Object themeOrNull)
        {
            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(row.transform, false);

            var rt = label.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(220f, 0f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            var tmp = label.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Left | TextAlignmentOptions.Midline;
            tmp.color = Color.white;
        }

        private static Object FindTheme(GameObject go)
        {
            // Try to find a UITheme on the same GameObject or nearby
            var binder = go.GetComponent<SettingsPanelBinder>();
            // Try MainMenuController on the scene root
            var mainMenu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
            if (mainMenu != null && mainMenu.theme != null) return mainMenu.theme;
            // Try the canvas
            var canvas = go.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var mainCtrl = canvas.GetComponentInParent<MainMenuController>();
                if (mainCtrl != null && mainCtrl.theme != null) return mainCtrl.theme;
            }
            return null;
        }
    }
}
