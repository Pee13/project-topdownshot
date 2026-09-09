using UnityEngine;

namespace TopDownTacticalAI.UI
{
    /// <summary>Displays FPS in the top-left corner of the screen (created automatically by SettingsManager when "Show FPS" option is enabled)</summary>
    public class FpsCounterDisplay : MonoBehaviour
    {
        private float _deltaTime;
        private GUIStyle _style;

        private void Update()
        {
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
        }

        private void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    normal = { textColor = Color.yellow }
                };
            }

            float fps = 1f / Mathf.Max(_deltaTime, 0.0001f);
            GUI.Label(new Rect(10, Screen.height - 30, 200, 30), $"FPS: {fps:F0}", _style);
        }
    }
}
