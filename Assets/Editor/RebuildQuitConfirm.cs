using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

namespace TopDownTacticalAI.Editor
{
    /// <summary>
    /// Replaces the two TMP_Dropdown children in QuitConfirmPanel (OnQuitConfirmYes / OnQuitConfirmnO)
    /// with proper Buttons ("ใช่" / "ไม่"), centered, wired to MainMenuController.OnQuitConfirmYes / OnQuitConfirmNo.
    /// </summary>
    public static class RebuildQuitConfirm
    {
        private static System.Reflection.Assembly UiAsm()
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a.GetName().Name == "UnityEngine.UI") return a;
            }
            return null;
        }

        private static System.Reflection.Assembly TmpAsm()
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a.GetName().Name == "Unity.TextMeshPro") return a;
            }
            return null;
        }

        [MenuItem("Tools/Rebuild QuitConfirm Panel")]
        private static void Run()
        {
            GameObject panel = null;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name == "QuitConfirmPanel" && go.scene.IsValid())
                {
                    panel = go;
                    break;
                }
            }
            if (panel == null)
            {
                Debug.LogWarning("QuitConfirmPanel not found");
                return;
            }

            // Find ALL MainMenuController instances (there may be duplicates in scene)
            List<MonoBehaviour> controllers = new List<MonoBehaviour>();
            foreach (var mb in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "MainMenuController" && mb.gameObject.scene.IsValid())
                {
                    controllers.Add(mb);
                }
            }
            if (controllers.Count == 0) { Debug.LogWarning("MainMenuController not found"); return; }
            Debug.Log("Found " + controllers.Count + " MainMenuController instance(s)");

            // 1. Remove ALL previous children that this tool may have created previously.
            //    This includes:
            //    - Old TMP_Dropdown children named "OnQuitConfirmYes" / "OnQuitConfirmnO"
            //    - Any prior "ButtonContainer" (the tool creates one on every run, so leftover
            //      containers stack and the new buttons sit behind them, blocking clicks)
            //    - Any prior "YesButton" / "NoButton" siblings of the container
            List<GameObject> toDestroy = new List<GameObject>();
            foreach (Transform child in panel.transform)
            {
                if (child.name == "OnQuitConfirmYes" || child.name == "OnQuitConfirmnO"
                    || child.name == "ButtonContainer" || child.name == "YesButton" || child.name == "NoButton")
                    toDestroy.Add(child.gameObject);
            }
            foreach (var go in toDestroy)
                UnityEngine.Object.DestroyImmediate(go);

            // 2. Create button container
            var container = new GameObject("ButtonContainer");
            container.transform.SetParent(panel.transform);

            var uiAsm = UiAsm();
            if (uiAsm == null) { Debug.LogError("UnityEngine.UI assembly not found"); return; }
            var hlgType = uiAsm.GetType("UnityEngine.UI.HorizontalLayoutGroup");
            if (hlgType == null) { Debug.LogError("HorizontalLayoutGroup type not found"); return; }
            var addComponentMethod = typeof(GameObject).GetMethod("AddComponent", new Type[] { typeof(Type) });
            var hlg = addComponentMethod.Invoke(container, new object[] { hlgType });
            var hlgSpacingProp = hlgType.GetProperty("spacing");
            var hlgAlignProp = hlgType.GetProperty("childAlignment");
            var hlgCwProp = hlgType.GetProperty("childControlWidth");
            var hlgChProp = hlgType.GetProperty("childControlHeight");
            hlgSpacingProp.SetValue(hlg, 60f);
            // childAlignment = MiddleCenter (4) so the HLG centers the buttons within the container
            hlgAlignProp.SetValue(hlg, 4);
            // childControlWidth=false so the HorizontalLayoutGroup respects each button's
            // own sizeDelta instead of stretching them to fill the container.
            hlgCwProp.SetValue(hlg, false);
            hlgChProp.SetValue(hlg, false);

            var containerRt = container.GetComponent<RectTransform>();
            // Fixed-width container centered horizontally and vertically in the panel.
            // Width = 2 buttons (180 each) + spacing (60) = 420px.
            containerRt.anchorMin = new Vector2(0.5f, 0.5f);
            containerRt.anchorMax = new Vector2(0.5f, 0.5f);
            containerRt.pivot = new Vector2(0.5f, 0.5f);
            containerRt.sizeDelta = new Vector2(420f, 80f);
            containerRt.anchoredPosition = new Vector2(0f, 0f);

            // 3. Create Yes/No buttons (English labels — LiberationSans SDF has no Thai glyphs,
            //    so Thai text renders as blank boxes; English keeps the panel clean and readable)
            var yesBtn = CreateButton(container.transform, "YesButton", "YES", new Vector2(180f, 70f), new Color(0.15f, 0.45f, 0.25f, 1f));
            var noBtn = CreateButton(container.transform, "NoButton", "NO", new Vector2(180f, 70f), new Color(0.55f, 0.15f, 0.15f, 1f));

            // 4. Wire listeners via reflection
            var buttonType = uiAsm.GetType("UnityEngine.UI.Button");
            var buttonClickEventType = uiAsm.GetType("UnityEngine.UI.Button+ButtonClickedEvent");
            var addListenerMethod = buttonClickEventType.GetMethod("AddListener");
            var removeAllMethod = buttonClickEventType.GetMethod("RemoveAllListeners");
            var unityActionType = Type.GetType("UnityEngine.Events.UnityAction, UnityEngine.CoreModule");
            if (unityActionType == null) unityActionType = typeof(UnityEngine.Events.UnityAction);

            var yesButton = yesBtn.GetComponent(buttonType);
            var noButton = noBtn.GetComponent(buttonType);
            var yesOnClick = buttonType.GetProperty("onClick").GetValue(yesButton);
            var noOnClick = buttonType.GetProperty("onClick").GetValue(noButton);

            removeAllMethod.Invoke(yesOnClick, null);
            removeAllMethod.Invoke(noOnClick, null);

            // Wire listeners AND assign fields on EVERY MainMenuController instance so the
            // new buttons work regardless of which controller Awake() runs on.
            foreach (var mmc in controllers)
            {
                var yesMethod = mmc.GetType().GetMethod("OnQuitConfirmYes");
                var noMethod = mmc.GetType().GetMethod("OnQuitConfirmNo");
                if (yesMethod == null || noMethod == null) continue;

                var yesDel = Delegate.CreateDelegate(unityActionType, mmc, yesMethod);
                var noDel = Delegate.CreateDelegate(unityActionType, mmc, noMethod);
                addListenerMethod.Invoke(yesOnClick, new object[] { yesDel });
                addListenerMethod.Invoke(noOnClick, new object[] { noDel });

                // 5. Assign to MainMenuController fields
                var quitConfirmYesField = mmc.GetType().GetField("quitConfirmYesButton");
                var quitConfirmNoField = mmc.GetType().GetField("quitConfirmNoButton");
                if (quitConfirmYesField != null) quitConfirmYesField.SetValue(mmc, yesButton);
                if (quitConfirmNoField != null) quitConfirmNoField.SetValue(mmc, noButton);

                Debug.Log("Wired listeners + assigned fields on " + mmc.gameObject.name);
            }

            // 6. Force layout rebuild (toggle HLG to trigger a full rebuild,
            //    then call ForceRebuildLayoutImmediate so buttons are centered)
            var layoutRebuilderType = uiAsm.GetType("UnityEngine.UI.LayoutRebuilder");
            var forceRebuildMethod = layoutRebuilderType.GetMethod("ForceRebuildLayoutImmediate");

            // Toggle HLG to force Unity to recalculate child positions
            var hlgEnabledProp = hlgType.GetProperty("enabled");
            hlgEnabledProp.SetValue(hlg, false);
            hlgEnabledProp.SetValue(hlg, true);

            forceRebuildMethod.Invoke(null, new object[] { containerRt });

            // Mark scene dirty so it gets saved
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(panel.scene);

            Debug.Log("QuitConfirmPanel rebuilt: Yes=" + yesBtn.name + " No=" + noBtn.name);
        }

        private static GameObject CreateButton(Transform parent, string name, string text, Vector2 size, Color bgColor)
        {
            var btnGO = new GameObject(name);
            btnGO.transform.SetParent(parent);

            var uiAsm = UiAsm();
            var addComponentMethod = typeof(GameObject).GetMethod("AddComponent", new Type[] { typeof(Type) });

            var imageType = uiAsm.GetType("UnityEngine.UI.Image");
            var img = addComponentMethod.Invoke(btnGO, new object[] { imageType });
            var imgColorProp = imageType.GetProperty("color");
            var imgTypeProp = imageType.GetProperty("type");
            imgColorProp.SetValue(img, bgColor);
            // Image.Type: 0=Simple, 1=Sliced, 2=Tiled, 3=Filled
            // Use Simple (0) so the button doesn't get stretched/9-sliced when the
            // theme is applied later (without a sprite, Sliced falls back weirdly).
            imgTypeProp.SetValue(img, 0);

            var buttonType = uiAsm.GetType("UnityEngine.UI.Button");
            var btn = addComponentMethod.Invoke(btnGO, new object[] { buttonType });
            var btnTargetGraphicProp = buttonType.GetProperty("targetGraphic");
            btnTargetGraphicProp.SetValue(btn, img);

            var rt = btnGO.GetComponent<RectTransform>();
            // Use stretch-relative anchor (0,0)–(0,0) so the HorizontalLayoutGroup can size and center the button
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(btnGO.transform);
            var tmpAsm = TmpAsm();
            var tmpType = tmpAsm.GetType("TMPro.TextMeshProUGUI");
            var tmp = addComponentMethod.Invoke(textGO, new object[] { tmpType });
            var alignType = tmpAsm.GetType("TMPro.TextAlignmentOptions");

            tmpType.GetProperty("text").SetValue(tmp, text);
            tmpType.GetProperty("fontSize").SetValue(tmp, 36f);
            tmpType.GetProperty("alignment").SetValue(tmp, Enum.ToObject(alignType, 514));
            tmpType.GetProperty("color").SetValue(tmp, Color.white);
            tmpType.GetProperty("enableAutoSizing").SetValue(tmp, true);
            tmpType.GetProperty("fontSizeMin").SetValue(tmp, 24f);
            tmpType.GetProperty("fontSizeMax").SetValue(tmp, 40f);

            var textRt = textGO.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            return btnGO;
        }
    }
}