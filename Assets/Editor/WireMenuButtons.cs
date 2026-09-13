using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEditor;
using UnityEditor.Events;

[ExecuteInEditMode]
public class WireMenuButtons : MonoBehaviour
{
    [Header("MainMenuController")]
    public MonoBehaviour target;

    [Header("Buttons")]
    public Button playButton;
    public Button settingsButton;
    public Button quitButton;
    public Button quitYesButton;
    public Button quitNoButton;
    public Button settingsBackButton;

    public void WireAll()
    {
        if (target == null) { Debug.LogError("Target is null"); return; }

        // OnPlayButton
        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            AddPersistentListener(playButton, "OnPlayButton");
        }

        // OnSettingsButton
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            AddPersistentListener(settingsButton, "OnSettingsButton");
        }

        // OnQuitButton
        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            AddPersistentListener(quitButton, "OnQuitButton");
        }

        // OnQuitConfirmYes
        if (quitYesButton != null)
        {
            quitYesButton.onClick.RemoveAllListeners();
            AddPersistentListener(quitYesButton, "OnQuitConfirmYes");
        }

        // OnQuitConfirmNo
        if (quitNoButton != null)
        {
            quitNoButton.onClick.RemoveAllListeners();
            AddPersistentListener(quitNoButton, "OnQuitConfirmNo");
        }

        // OnSettingsBackButton
        if (settingsBackButton != null)
        {
            settingsBackButton.onClick.RemoveAllListeners();
            AddPersistentListener(settingsBackButton, "OnSettingsBackButton");
        }

        Debug.Log("WireMenuButtons: All listeners wired!");
    }

    private void AddPersistentListener(Button button, string methodName)
    {
        // Build a UnityAction delegate matching the method signature, then attach it as a persistent listener
        var t = target.GetType();
        var method = t.GetMethod(methodName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        if (method == null)
        {
            Debug.LogError("Method not found: " + methodName);
            return;
        }

        var del = System.Delegate.CreateDelegate(typeof(UnityAction), target, method) as UnityAction;
        if (del == null)
        {
            Debug.LogError("Failed to create delegate for: " + methodName);
            return;
        }

        UnityEventTools.AddPersistentListener(button.onClick, del);
    }
}
