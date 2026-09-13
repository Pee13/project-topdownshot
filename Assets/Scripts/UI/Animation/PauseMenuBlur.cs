using UnityEngine;
using UnityEngine.UI;

namespace TopDownTacticalAI.UI.Animation
{
    /// <summary>
    /// Dim/fade background effect for the Pause Menu — a version that does NOT rely on URP (Universal Render Pipeline) at all.
    /// Originally used URP Volume + DepthOfField (real blur), but this project does not have URP installed,
    /// which caused Error CS0246 ('Volume'/'VolumeProfile' not found because the UnityEngine.Rendering.Universal namespace is missing).
    ///
    /// Fix: switched approach to using a fullscreen semi-transparent black Image overlay that fades in/out.
    /// Similar visual result (background looks dimmed/darkened during Pause) but works with any Render Pipeline.
    /// No additional package installation required.
    ///
    /// The effective dim strength scales with <see cref="GraphicsQualityManager.ScreenEffectsIntensity"/>
    /// — so disabling Screen Effects or lowering Effects Quality reduces the dim overlay.
    ///
    /// Public API remains identical (EnableBlur/DisableBlur/ToggleBlur) — PauseMenu.cs does not need to be modified.
    ///
    /// Setup: attach this script to any GameObject under the game-scene Canvas (no special Volume/Camera required).
    /// </summary>
    public class PauseMenuBlur : MonoBehaviour
    {
        [Header("── Dim Overlay Settings ──")]
        [Tooltip("Fullscreen image used for dimming effect (auto-created if not assigned)")]
        [SerializeField] private Image _dimImage;
        [Tooltip("Max dim/brightness during blur (0 = fully transparent, 1 = solid black)")]
        [SerializeField] private float _maxDimAlpha = 0.6f;
        [Tooltip("Color used for the dim overlay")]
        [SerializeField] private Color _dimColor = Color.black;
        [Tooltip("Fade in/out speed (higher = faster)")]
        [SerializeField] private float _transitionSpeed = 8f;

        private bool _isBlurred = false;
        private float _targetAlpha = 0f;

        private void OnEnable()
        {
            GraphicsQualityManager.OnChanged += HandleQualityChanged;
        }

        private void OnDisable()
        {
            GraphicsQualityManager.OnChanged -= HandleQualityChanged;
        }

        private void HandleQualityChanged(float _)
        {
            if (_isBlurred) EnableBlur(); // recompute target with new intensity
        }

        private void Awake()
        {
            if (_dimImage == null) CreateDimImage();
        }

        private void CreateDimImage()
        {
            var canvas = GetComponentInChildren<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();

            GameObject dimGO = new GameObject("PauseDimOverlay");
            if (canvas != null) dimGO.transform.SetParent(canvas.transform, false);
            else dimGO.transform.SetParent(transform, false);

            dimGO.transform.SetAsLastSibling(); // Move to top of sibling order (before the pause menu panels if the order is correct)

            _dimImage = dimGO.AddComponent<Image>();
            _dimImage.color = new Color(_dimColor.r, _dimColor.g, _dimColor.b, 0f);
            _dimImage.raycastTarget = false; // Don't block clicks on the pause menu buttons placed above

            RectTransform rt = _dimImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        private void Update()
        {
            if (_dimImage == null) return;

            Color c = _dimImage.color;
            float newAlpha = Mathf.Lerp(c.a, _targetAlpha, Time.unscaledDeltaTime * _transitionSpeed);
            c.a = newAlpha;
            _dimImage.color = c;
        }

        /// <summary>Enable the dim/fade effect (when paused).</summary>
        public void EnableBlur()
        {
            _isBlurred = true;
            // Apply screen effects intensity (defaults to 1.0 if manager not yet initialized)
            float intensity = GraphicsQualityManager.ScreenEffectsIntensity;
            _targetAlpha = _maxDimAlpha * Mathf.Clamp01(intensity);
        }

        /// <summary>Disable the dim/fade effect (when resumed).</summary>
        public void DisableBlur()
        {
            _isBlurred = false;
            _targetAlpha = 0f;
        }

        /// <summary>Toggle the effect on/off.</summary>
        public void ToggleBlur()
        {
            if (_isBlurred) DisableBlur();
            else EnableBlur();
        }
    }
}
