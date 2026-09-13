using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TopDownTacticalAI.UI
{
    /// <summary>
    /// Neon-style menu VFX: cyan glow halo behind the panel, soft dust particles
    /// drifting upward, and a pulse animation that breathes in/out. Drop this
    /// component on any menu panel and call Play() on enable / Stop() on disable.
    ///
    /// All effects are pure Unity UI (Image + RectTransform + AnimationCurve) so
    /// no extra packages or Particle System prefabs are required.
    ///
    /// Intensity is driven by <see cref="GraphicsQualityManager.MenuVFXIntensity"/> —
    /// automatically scales down when the player disables Menu VFX or sets
    /// Effects Quality below maximum.
    /// </summary>
    public class NeonMenuVFX : MonoBehaviour
    {
        [Header("── Colors ──")]
        [Tooltip("Primary neon color (e.g. cyan).")]
        public Color neonColor = new Color(0.2f, 0.9f, 1f, 1f);
        [Tooltip("Secondary accent color (e.g. magenta).")]
        public Color accentColor = new Color(1f, 0.3f, 0.8f, 1f);
        [Tooltip("Background panel tint (rgba; usually a dark blue).")]
        public Color panelColor = new Color(0.05f, 0.08f, 0.15f, 0.95f);

        [Header("── Halo Glow ──")]
        [Tooltip("Width of the outer glow (pixels). 0 disables.")]
        [Range(0f, 120f)] public float glowSize = 60f;
        [Tooltip("Halo opacity (0..1).")]
        [Range(0f, 1f)] public float glowAlpha = 0.55f;
        [Tooltip("Front sheen opacity relative to glowAlpha (keep low so buttons stay readable).")]
        [Range(0f, 1f)] public float frontSheenRatio = 0.15f;
        [Tooltip("Slow hue drift between neonColor and accentColor (0 = static color).")]
        [Range(0f, 1f)] public float colorDriftAmount = 0.6f;
        [Tooltip("Speed of the hue drift.")]
        public float colorDriftSpeed = 0.25f;
        [Tooltip("How fast the halo pulses (breathing animation).")]
        public float pulseSpeed = 0.7f;
        [Tooltip("Pulse depth — how much the glow brightens/dims.")]
        [Range(0f, 0.5f)] public float pulseAmount = 0.2f;

        [Header("── Soft Dust Particles ──")]
        [Tooltip("Number of dust particles to spawn.")]
        [Range(0, 60)] public int dustCount = 30;
        [Tooltip("Spawn area half-size in pixels around the panel center.")]
        public float dustSpawnRadius = 250f;
        [Tooltip("Min/max upward drift speed in px/sec.")]
        public float dustMinSpeed = 8f;
        public float dustMaxSpeed = 22f;
        [Tooltip("Dust sprite color (alpha controls visibility).")]
        public Color dustColor = new Color(0.6f, 0.85f, 1f, 0.4f);
        [Tooltip("Dust min/max scale.")]
        public float dustMinScale = 1.5f;
        public float dustMaxScale = 4f;

        [Header("── Panel Shadow ──")]
        [Tooltip("Drop-shadow offset (px).")]
        public Vector2 shadowOffset = new Vector2(0, -8);
        [Tooltip("Drop-shadow blur width (px).")]
        [Range(0f, 40f)] public float shadowBlur = 12f;
        [Tooltip("Drop-shadow alpha (0..1).")]
        [Range(0f, 1f)] public float shadowAlpha = 0.55f;

        [Header("── References (auto-find if empty) ──")]
        [Tooltip("The main panel RectTransform. If null, uses this GameObject's RectTransform.")]
        public RectTransform targetPanel;
        [Tooltip("The main panel Image. Used as the body that the shadow attaches to.")]
        public Image targetImage;

        // ── Internals ──
        private GameObject _haloGO;
        private Image _haloImage;
        private RectTransform _haloRect;

        private GameObject _frontHaloGO;
        private Image _frontHaloImage;
        private RectTransform _frontHaloRect;

        private GameObject _shadowGO;
        private Image _shadowImage;

        private readonly List<DustParticle> _dust = new List<DustParticle>();
        private RectTransform _dustRoot;

        private Coroutine _pulseRoutine;
        private bool _isPlaying = false;

        // Cached intensity from GraphicsQualityManager — read every frame to stay in sync
        private float _intensity = 1f;

        // Simple dust data class (no MonoBehaviour per particle — pure UI)
        private class DustParticle
        {
            public RectTransform rect;
            public Image image;
            public float speed;
            public float startY;
            public float driftX;
            public float phase;
        }

        private void Awake()
        {
            if (targetPanel == null) targetPanel = GetComponent<RectTransform>();
            if (targetImage == null) targetImage = GetComponent<Image>();
        }

        private void OnEnable()
        {
            // Subscribe to intensity changes so Play() / Stop() / pulse auto-update
            GraphicsQualityManager.OnChanged += HandleQualityChanged;
            _intensity = GraphicsQualityManager.MenuVFXIntensity;
        }

        private void OnDisable()
        {
            GraphicsQualityManager.OnChanged -= HandleQualityChanged;
        }

        private void HandleQualityChanged(float _)
        {
            _intensity = GraphicsQualityManager.MenuVFXIntensity;
            // Live update: if intensity drops to 0, stop the VFX; if it rises, restart
            if (_isPlaying && _intensity <= 0f) { Stop(); return; }
            if (!_isPlaying && _intensity > 0f) { Play(); return; }

            // Otherwise: rebuild dust count + halo alpha to match the new intensity
            ApplyIntensityToVisuals();
        }

        private void ApplyIntensityToVisuals()
        {
            // Halo alpha scales with intensity
            if (_haloImage != null)
            {
                var c = _haloImage.color;
                c.a = Mathf.Clamp01(glowAlpha * _intensity);
                _haloImage.color = c;
            }
            if (_frontHaloImage != null)
            {
                var c = _frontHaloImage.color;
                c.a = Mathf.Clamp01(glowAlpha * 0.9f * _intensity);
                _frontHaloImage.color = c;
            }
            // Dust alpha + count (rebuild by toggling off particles beyond count)
            int targetCount = Mathf.Max(0, Mathf.RoundToInt(dustCount * _intensity));
            for (int i = 0; i < _dust.Count; i++)
            {
                if (_dust[i]?.image != null)
                {
                    var c = _dust[i].image.color;
                    c.a = Mathf.Clamp01(dustColor.a * _intensity);
                    _dust[i].image.color = c;
                }
                if (_dust[i]?.rect != null)
                {
                    _dust[i].rect.gameObject.SetActive(i < targetCount);
                }
            }
        }

        /// <summary>
        /// Builds and starts the VFX (halo, shadow, dust, pulse). Idempotent.
        /// </summary>
        public void Play()
        {
            if (_isPlaying) return;
            _isPlaying = true;

            if (targetPanel == null)
            {
                Debug.LogWarning("[NeonMenuVFX] targetPanel is null — cannot build VFX.");
                return;
            }

            // Re-read intensity in case OnEnable ran before the manager initialized
            _intensity = GraphicsQualityManager.MenuVFXIntensity;
            if (_intensity <= 0f)
            {
                // VFX disabled — don't build anything
                _isPlaying = false;
                return;
            }

            EnsureShadow();
            EnsureHalo();
            EnsureDustRoot();
            SpawnDust();
            ApplyIntensityToVisuals();
            StartPulse();
        }

        /// <summary>
        /// Stops the VFX and removes all generated GameObjects. Call before destroying the panel.
        /// </summary>
        public void Stop()
        {
            _isPlaying = false;
            if (_pulseRoutine != null) { StopCoroutine(_pulseRoutine); _pulseRoutine = null; }
            if (_haloGO != null) { Destroy(_haloGO); _haloGO = null; }
            if (_frontHaloGO != null) { Destroy(_frontHaloGO); _frontHaloGO = null; }
            if (_shadowGO != null) { Destroy(_shadowGO); _shadowGO = null; }
            if (_dustRoot != null) { Destroy(_dustRoot.gameObject); _dustRoot = null; }
            _dust.Clear();
        }

        // ─────────────────────────────────────────────────────────────────
        // Halo
        // ─────────────────────────────────────────────────────────────────
        private void EnsureHalo()
        {
            if (_haloGO != null) return;
            if (glowSize <= 0f) return;

            _haloGO = new GameObject("NeonHalo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _haloGO.transform.SetParent(targetPanel.parent, false);
            _haloGO.transform.SetAsFirstSibling();  // behind the panel

            _haloRect = _haloGO.GetComponent<RectTransform>();
            _haloRect.anchorMin = new Vector2(0.5f, 0.5f);
            _haloRect.anchorMax = new Vector2(0.5f, 0.5f);
            _haloRect.pivot = new Vector2(0.5f, 0.5f);
            _haloRect.sizeDelta = targetPanel.rect.size + new Vector2(glowSize * 2f, glowSize * 2f);
            _haloRect.anchoredPosition = targetPanel.anchoredPosition;
            _haloRect.localScale = Vector3.one;

            _haloImage = _haloGO.GetComponent<Image>();
            _haloImage.sprite = BuildRadialSprite(256, neonColor);
            _haloImage.color = new Color(neonColor.r, neonColor.g, neonColor.b, glowAlpha);
            _haloImage.raycastTarget = false;

            // Front halo (in front of the panel — visible glow on top of buttons)
            _frontHaloGO = new GameObject("NeonHaloFront", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _frontHaloGO.transform.SetParent(targetPanel.parent, false);
            _frontHaloGO.transform.SetAsLastSibling();  // in front of the panel

            _frontHaloRect = _frontHaloGO.GetComponent< RectTransform>();
            _frontHaloRect.anchorMin = new Vector2(0.5f, 0.5f);
            _frontHaloRect.anchorMax = new Vector2(0.5f, 0.5f);
            _frontHaloRect.pivot = new Vector2(0.5f, 0.5f);
            _frontHaloRect.sizeDelta = targetPanel.rect.size + new Vector2(glowSize * 2f, glowSize * 2f);
            _frontHaloRect.anchoredPosition = targetPanel.anchoredPosition;
            _frontHaloRect.localScale = Vector3.one;

            _frontHaloImage = _frontHaloGO.GetComponent<Image>();
            _frontHaloImage.sprite = BuildRadialSprite(256, neonColor);
            _frontHaloImage.color = new Color(neonColor.r, neonColor.g, neonColor.b, glowAlpha * 0.9f);
            _frontHaloImage.raycastTarget = false;
        }

        // ─────────────────────────────────────────────────────────────────
        // Shadow
        // ─────────────────────────────────────────────────────────────────
        private void EnsureShadow()
        {
            if (_shadowGO != null) return;
            if (targetImage == null || shadowBlur <= 0f) return;

            // Skip if target Image already has a Shadow component (avoid double-shadow)
            var existing = targetImage.GetComponent<Shadow>();
            if (existing == null)
            {
                var sh = targetImage.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, shadowAlpha);
                sh.effectDistance = shadowOffset;
                // Shadow component doesn't have a blur radius; blur is approximated by offset distance
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Dust
        // ─────────────────────────────────────────────────────────────────
        private void EnsureDustRoot()
        {
            if (_dustRoot != null) return;
            var go = new GameObject("DustRoot", typeof(RectTransform));
            go.transform.SetParent(targetPanel.parent, false);
            _dustRoot = go.GetComponent<RectTransform>();
            _dustRectSetup(_dustRoot, targetPanel.anchoredPosition, targetPanel.rect.size);
            _dustRoot.SetAsFirstSibling();  // behind everything
        }

        private void _dustRectSetup(RectTransform rt, Vector2 anchored, Vector2 size)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size + new Vector2(dustSpawnRadius * 2f, dustSpawnRadius * 2f);
            rt.anchoredPosition = anchored;
            rt.localScale = Vector3.one;
        }

        private void SpawnDust()
        {
            if (_dustRoot == null) return;
            for (int i = 0; i < dustCount; i++)
            {
                var go = new GameObject($"Dust_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(_dustRoot, false);
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);

                float startX = Random.Range(-dustSpawnRadius, dustSpawnRadius);
                float startY = Random.Range(-dustSpawnRadius, dustSpawnRadius);
                rect.anchoredPosition = new Vector2(startX, startY);

                float scale = Random.Range(dustMinScale, dustMaxScale);
                rect.sizeDelta = new Vector2(2f, 2f);
                rect.localScale = new Vector3(scale, scale, 1f);

                var img = go.GetComponent<Image>();
                img.sprite = BuildRadialSprite(32, dustColor);
                img.color = dustColor;
                img.raycastTarget = false;

                _dust.Add(new DustParticle
                {
                    rect = rect,
                    image = img,
                    speed = Random.Range(dustMinSpeed, dustMaxSpeed),
                    startY = startY,
                    driftX = Random.Range(-3f, 3f),
                    phase = Random.Range(0f, Mathf.PI * 2f)
                });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Pulse + dust update
        // ─────────────────────────────────────────────────────────────────
        private void StartPulse()
        {
            if (_pulseRoutine != null) StopCoroutine(_pulseRoutine);
            _pulseRoutine = StartCoroutine(PulseRoutine());
        }

        private IEnumerator PulseRoutine()
        {
            while (_isPlaying)
            {
                float t = Time.unscaledTime * pulseSpeed;
                if (_haloImage != null)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(t);
                    float alpha = (glowAlpha + (pulse - 0.5f) * pulseAmount * 2f) * _intensity;
                    _haloImage.color = new Color(neonColor.r, neonColor.g, neonColor.b, Mathf.Clamp01(alpha));
                    float scale = 1f + (pulse - 0.5f) * 0.04f;
                    _haloRect.localScale = new Vector3(scale, scale, 1f);
                }

                // Front halo pulse (bright, visible on top of buttons)
                if (_frontHaloImage != null)
                {
                    float pulseF = 0.5f + 0.5f * Mathf.Sin(t + 1.5f);
                    float a = (glowAlpha * 0.9f + (pulseF - 0.5f) * pulseAmount * 2f) * _intensity;
                    _frontHaloImage.color = new Color(neonColor.r, neonColor.g, neonColor.b, Mathf.Clamp01(a));
                    float s = 1f + (pulseF - 0.5f) * 0.06f;
                    _frontHaloRect.localScale = new Vector3(s, s, 1f);
                }

                // Update dust positions
                UpdateDust(t);
                yield return null;
            }
        }

        private void UpdateDust(float pulseTime)
        {
            for (int i = 0; i < _dust.Count; i++)
            {
                var d = _dust[i];
                if (d == null || d.rect == null) continue;
                float y = d.rect.anchoredPosition.y + d.speed * Time.unscaledDeltaTime;
                float x = d.rect.anchoredPosition.x + Mathf.Sin(pulseTime * 0.5f + d.phase) * d.driftX * Time.unscaledDeltaTime;
                if (y > dustSpawnRadius)
                {
                    y = -dustSpawnRadius;
                    x = Random.Range(-dustSpawnRadius, dustSpawnRadius);
                }
                d.rect.anchoredPosition = new Vector2(x, y);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Radial sprite generation (no Asset Store, no Textures folder needed)
        // ─────────────────────────────────────────────────────────────────

        // Shared radial sprite cache (keyed by resolution) to avoid rebuilding every time
        private static Sprite _cachedRadialSprite64;
        private static Sprite _cachedRadialSprite32;
        private static Sprite _cachedRadialSprite256;

        private static Sprite BuildRadialSprite(int size, Color color)
        {
            Sprite cached = null;
            if (size == 64) cached = _cachedRadialSprite64;
            else if (size == 32) cached = _cachedRadialSprite32;
            else if (size == 256) cached = _cachedRadialSprite256;
            if (cached != null) return cached;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float r = (size * 0.5f) - 1f;
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;  // ease out for soft falloff
                    tex.SetPixel(x, y, new Color(color.r, color.g, color.b, a * color.a));
                }
            }
            tex.Apply(false, true);
            var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            sp.name = "RadialNeonSprite_" + size;

            if (size == 64) _cachedRadialSprite64 = sp;
            else if (size == 32) _cachedRadialSprite32 = sp;
            else if (size == 256) _cachedRadialSprite256 = sp;
            return sp;
        }

        private void OnDestroy()
        {
            Stop();
        }
    }
}
