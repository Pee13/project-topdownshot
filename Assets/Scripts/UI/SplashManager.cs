using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.UI.Animation;

namespace TopDownTacticalAI.UI
{
    /// <summary>
    /// Drives the first-launch splash screen.
    /// Flow on first launch:
    ///   1. Show progress bar (0%) — start Addressables pre-warm.
    ///   2. Wait for all labeled asset groups to download (progress 0->100%).
    ///   3. Hide progress bar.
    ///   4. Fade 3 logo cards (University -> CSS -> Unity).
    ///   5. Transition to MainMenu.
    /// On subsequent launches, the splash is skipped entirely and the menu
    /// loads directly -- but the cache from the first launch persists, so
    /// MainMenu / Level1 still open with no pop-in.
    /// </summary>
    public class SplashManager : MonoBehaviour
    {
        [Header("-- Timing --")]
        [Tooltip("How long each logo stays fully visible (seconds).")]
        [SerializeField] private float _holdDuration = 3.0f;
        [Tooltip("Fade in/out duration (seconds).")]
        [SerializeField] private float _fadeDuration = 2.0f;

        [Header("-- Scene Names --")]
        [Tooltip("Scene to load after the splash finishes. Defaults to 'MainMenu'.")]
        [SerializeField] private string _mainMenuSceneName = "MainMenu";

        [Header("-- Pre-warm Labels --")]
        [Tooltip("Addressables label groups to pre-warm before showing logos.")]
        [SerializeField] private List<string> _prewarmLabels = new List<string> { "mainmenu", "level1" };

        [Header("-- Optional Real Logos --")]
        [Tooltip("Optional real logo sprites. If empty, runtime placeholder shapes + text are used.")]
        [SerializeField] private Sprite[] _logoSprites;

        // Runtime state
        private Coroutine _splashCoroutine;
        private CanvasGroup[] _cards;

        // Progress UI
        private GameObject _progressContainer;
        private Image _progressFillImage;
        private TextMeshProUGUI _progressPercentText;
        private TextMeshProUGUI _progressStatusText;

        private void Awake()
        {
            // Build the splash UI at runtime so the scene file stays simple.
            BuildSplashUI();
            ShowProgress(false);
        }

        private void Start()
        {
            // Splash แสดงทุกครั้งที่เปิดเกม — ไม่ข้าม ไม่ว่าจะเคยเปิดมาก่อนหรือไม่
            Debug.Log("[SplashManager] Start() called. Running PreWarmThenSplash every launch.");
            _splashCoroutine = StartCoroutine(PreWarmThenSplash());
        }

        private void Update()
        {
            // Splash เล่นครบทุกขั้นตอนเสมอ — ไม่มี skip
        }

        private void LoadMainMenu()
        {
            // Splash ไม่ถูกบันทึกว่าเคยโชว์แล้ว — จะโชว์ใหม่ทุกครั้งที่เปิดเกม
            if (ScreenTransition.Instance != null)
            {
                ScreenTransition.Instance.LoadSceneWithTransition(_mainMenuSceneName);
            }
            else
            {
                SceneManager.LoadScene(_mainMenuSceneName);
            }
        }

        // === UI BUILD ===

        private void BuildSplashUI()
        {
            // Canvas (Screen Space Overlay)
            var canvasGO = new GameObject("SplashCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            // Black background
            var bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bg = bgGO.GetComponent<Image>();
            bg.color = Color.black;
            bg.raycastTarget = false;
            StretchToParent(bgGO.GetComponent<RectTransform>());

            // 3 logo cards
            // ทั้ง 3 sprite มี wordmark (ชื่อ) อยู่ใน sprite เองแล้ว
            // ดังนั้นไม่ต้องเพิ่ม title text ด้านล่าง เพื่อให้ sprite ขยายได้เต็มที่
            string[] labels = new string[] {
                null,   // มหาวิทยาลัย - sprite มี wordmark อยู่แล้ว
                null,   // CSS - sprite มี wordmark อยู่แล้ว
                null    // Unity - sprite มี wordmark อยู่แล้ว
            };
            string[] subs = new string[] {
                null,
                null,
                null
            };

            _cards = new CanvasGroup[3];
            for (int i = 0; i < 3; i++)
            {
                _cards[i] = CreateLogoCard(canvasGO.transform, i, labels[i], subs[i]);
                _cards[i].alpha = 0f;
                _cards[i].interactable = false;
                _cards[i].blocksRaycasts = false;
            }

            BuildProgressUI(canvasGO.transform);
        }

        // === PROGRESS BAR UI ===

        private void BuildProgressUI(Transform parent)
        {
            // Container anchored as a band at the very bottom of the canvas.
            // The band is kept compact (~120px) and pushed flush against the
            // bottom edge so it never overlaps the title text of any logo card.
            var containerGO = new GameObject("ProgressContainer",
                typeof(RectTransform), typeof(CanvasGroup));
            containerGO.transform.SetParent(parent, false);
            var crt = containerGO.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 0f);
            crt.anchorMax = new Vector2(1f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.offsetMin = new Vector2(0f, 0f);
            crt.offsetMax = new Vector2(0f, 120f); // 120px tall band at the bottom
            _progressContainer = containerGO;
            var containerCG = containerGO.GetComponent<CanvasGroup>();
            containerCG.alpha = 0f; // hidden until we start pre-warm
            containerCG.interactable = false;
            containerCG.blocksRaycasts = false;

            // Status text (top of band)
            var statusGO = new GameObject("Status",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            statusGO.transform.SetParent(containerGO.transform, false);
            _progressStatusText = statusGO.GetComponent<TextMeshProUGUI>();
            _progressStatusText.text = "Preparing assets...";
            _progressStatusText.color = Color.white;
            _progressStatusText.alignment = TextAlignmentOptions.Center;
            _progressStatusText.fontSize = 18;
            var statusRT = statusGO.GetComponent<RectTransform>();
            statusRT.anchorMin = new Vector2(0f, 0.55f);
            statusRT.anchorMax = new Vector2(1f, 1f);
            statusRT.offsetMin = Vector2.zero;
            statusRT.offsetMax = Vector2.zero;
            statusRT.anchoredPosition = Vector2.zero;

            // Progress percent (small text in middle of bar)
            var percentGO = new GameObject("Percent",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            percentGO.transform.SetParent(containerGO.transform, false);
            _progressPercentText = percentGO.GetComponent<TextMeshProUGUI>();
            _progressPercentText.text = "0%";
            _progressPercentText.color = Color.white;
            _progressPercentText.alignment = TextAlignmentOptions.Center;
            _progressPercentText.fontSize = 24;
            _progressPercentText.fontStyle = FontStyles.Bold;
            var percentRT = percentGO.GetComponent<RectTransform>();
            percentRT.anchorMin = new Vector2(0f, 0f);
            percentRT.anchorMax = new Vector2(1f, 0.5f);
            percentRT.offsetMin = Vector2.zero;
            percentRT.offsetMax = Vector2.zero;
            percentRT.anchoredPosition = Vector2.zero;

            // Bar background (dark gray)
            var bgGO = new GameObject("BarBackground",
                typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(containerGO.transform, false);
            var bgImg = bgGO.GetComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            bgImg.raycastTarget = false;
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.2f, 0.05f);
            bgRT.anchorMax = new Vector2(0.8f, 0.45f);
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            bgRT.anchoredPosition = Vector2.zero;

            // Bar fill (green). Uses Image.type=Filled so we just set fillAmount.
            var fillGO = new GameObject("BarFill",
                typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(bgGO.transform, false);
            _progressFillImage = fillGO.GetComponent<Image>();
            _progressFillImage.color = new Color(0.2f, 0.85f, 0.35f, 1f);
            _progressFillImage.raycastTarget = false;
            _progressFillImage.type = Image.Type.Filled;
            _progressFillImage.fillMethod = Image.FillMethod.Horizontal;
            _progressFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            _progressFillImage.fillAmount = 0f;
            var fillRT = fillGO.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            fillRT.anchoredPosition = Vector2.zero;
        }

        private void ShowProgress(bool show)
        {
            if (_progressContainer == null) return;
            _progressContainer.SetActive(show);
            if (show)
            {
                var cg = _progressContainer.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
            }
        }

        private void SetProgress(float t01)
        {
            t01 = Mathf.Clamp01(t01);
            // ปรับความกว้าง fill bar ตาม progress (scale-based) แทน fillAmount
            // เพื่อให้แน่ใจว่า URP 2D render ออกมาตามจริง
            if (_progressFillImage != null)
            {
                _progressFillImage.fillAmount = t01; // ลองวิธี Image.Filled ก่อน
                var rt = _progressFillImage.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = new Vector2(t01, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            if (_progressPercentText != null) _progressPercentText.text = Mathf.RoundToInt(t01 * 100f) + "%";
        }

        private CanvasGroup CreateLogoCard(Transform parent, int index, string title, string sub)
        {
            var cardGO = new GameObject("LogoCard_" + index,
                typeof(RectTransform), typeof(CanvasGroup));
            cardGO.transform.SetParent(parent, false);
            var rt = cardGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            // Use Screen.width/height which reflects actual render resolution (game view in editor)
            float sw = (float)Screen.width;
            float sh = (float)Screen.height;
            if (sw < 1f) sw = 1920f;
            if (sh < 1f) sh = 1080f;

            // Logo image: anchor-stretch to fill most of the canvas, with padding so the title fits below.
            // preserveAspect on the Image handles aspect ratio automatically.
            // When there's no title (e.g. CSS / Unity logos have their own wordmark),
            // reduce the bottom padding so the logo can sit larger.
            // Bottom padding must clear the progress band (~140px) so titles
            // never overlap the progress UI.
            bool hasTitle = !string.IsNullOrEmpty(title);
            float padH = hasTitle ? Mathf.Max(sh * 0.22f, 160f) : sh * 0.08f;
            float padW = sw * 0.10f; // side padding

            if (_logoSprites != null && index < _logoSprites.Length && _logoSprites[index] != null)
            {
                bool isUniversity = (index == 0);

                if (isUniversity)
                {
                    // Layout พิเศษสำหรับโลโก้มหาลัย:
                    //   [Sprite โลโก้]   [ข้อความ "Rajamangala University of Technology Suvarnabhumi"]
                    // ทั้งคู่อยู่กึ่งกลางจอ ไม่ยืดภาพ ตัวอักษรเป็น TMP text ไม่ใช่ sprite (หลีกเลี่ยง text ดำกลืน bg)
                    BuildUniversityLayout(cardGO.transform, _logoSprites[0], sh, sw);
                }
                else
                {
                    var imgGO = new GameObject("LogoImage", typeof(RectTransform), typeof(Image));
                    imgGO.transform.SetParent(cardGO.transform, false);
                    var img = imgGO.GetComponent<Image>();
                    img.sprite = _logoSprites[index];
                    img.preserveAspect = true;
                    img.raycastTarget = false;

                    var irt = imgGO.GetComponent<RectTransform>();
                    irt.anchorMin = new Vector2(0f, 0f);
                    irt.anchorMax = new Vector2(1f, 1f);
                    irt.offsetMin = new Vector2(padW, padH);
                    irt.offsetMax = new Vector2(-padW, -padH);
                    irt.pivot = new Vector2(0.5f, 0.5f);
                    irt.anchoredPosition = Vector2.zero;
                    irt.localScale = Vector3.one;
                }
            }
            else
            {
                var shapeGO = new GameObject("LogoPlaceholder", typeof(RectTransform), typeof(Image));
                shapeGO.transform.SetParent(cardGO.transform, false);
                var shapeImg = shapeGO.GetComponent<Image>();
                shapeImg.color = index == 0 ? new Color(1f, 0.85f, 0.2f)
                    : index == 1 ? new Color(1f, 0.55f, 0.1f)
                    : new Color(0.78f, 0.78f, 0.78f);
                shapeImg.raycastTarget = false;
                var srt = shapeGO.GetComponent<RectTransform>();
                srt.anchorMin = new Vector2(0f, 0f);
                srt.anchorMax = new Vector2(1f, 1f);
                srt.offsetMin = new Vector2(padW, padH);
                srt.offsetMax = new Vector2(-padW, -padH);
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.anchoredPosition = Vector2.zero;
                srt.localScale = Vector3.one;
            }

            // Title text (below logo, horizontally centered)
            if (hasTitle)
            {
                var titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleGO.transform.SetParent(cardGO.transform, false);
                var titleText = titleGO.GetComponent<TextMeshProUGUI>();
                titleText.text = title;
                titleText.fontSize = Mathf.RoundToInt(sh * 0.03f);
                titleText.color = Color.white;
                titleText.alignment = TextAlignmentOptions.Center;
                titleText.enableAutoSizing = true;
                titleText.fontSizeMin = 18;
                titleText.fontSizeMax = Mathf.RoundToInt(sh * 0.05f);
                var titleRT = titleGO.GetComponent<RectTransform>();
                titleRT.anchorMin = new Vector2(0f, 0f);
                titleRT.anchorMax = new Vector2(1f, 0f);
                titleRT.pivot = new Vector2(0.5f, 1f);
                // Title sits at the bottom of the logo image (which is at
                // padH from the canvas bottom). Give the title its own band
                // of ~80px so it never overlaps the image.
                titleRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 80f);
                titleRT.anchoredPosition = new Vector2(0, padH + 40f);

                // Sub text (optional, below title)
                if (!string.IsNullOrEmpty(sub))
                {
                    var subGO = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
                    subGO.transform.SetParent(cardGO.transform, false);
                    var subText = subGO.GetComponent<TextMeshProUGUI>();
                    subText.text = sub;
                    subText.fontSize = Mathf.RoundToInt(sh * 0.015f);
                    subText.color = new Color(1f, 1f, 1f, 0.7f);
                    subText.alignment = TextAlignmentOptions.Center;
                    subText.enableAutoSizing = true;
                    subText.fontSizeMin = 12;
                    subText.fontSizeMax = 24;
                    var subRT = subGO.GetComponent<RectTransform>();
                    subRT.anchorMin = new Vector2(0f, 0f);
                    subRT.anchorMax = new Vector2(1f, 0f);
                    subRT.pivot = new Vector2(0.5f, 1f);
                    subRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 30f);
                    subRT.anchoredPosition = new Vector2(0, padH + 120f);
                }
            }

            return cardGO.GetComponent<CanvasGroup>();
        }

        private static void StretchToParent(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        // === SEQUENCE ===

        private IEnumerator PreWarmThenSplash()
        {
            // Show progress bar immediately.
            ShowProgress(true);
            SetProgress(0f);
            if (_progressStatusText != null)
                _progressStatusText.text = "Preparing assets...";

            // Start pre-warm in background while keeping the splash visible.
            var prewarmLabels = new List<string>(_prewarmLabels);
            bool prewarmDone = false;

            // Launch the Addressables pre-warm coroutine.
            var prewarmRoutine = StartCoroutine(PreWarmRoutine(prewarmLabels, (p) =>
            {
                SetProgress(p);
                if (_progressStatusText != null)
                    _progressStatusText.text = $"Preparing assets... {Mathf.RoundToInt(p * 100f)}%";
                if (p >= 1f)
                    prewarmDone = true;
            }));

            // รอ pre-warm เสร็จ (ไม่มี skip — เล่นครบทุกขั้นตอน)
            while (!prewarmDone)
            {
                yield return null;
            }

            // Show the "100%" completion for a moment so the user can see it.
            // In a release build with remote assets, this naturally takes
            // longer; in the editor with local assets it's instant, so we
            // hold the bar briefly for visibility.
            if (_progressStatusText != null)
                _progressStatusText.text = "Ready";
            yield return new WaitForSecondsRealtime(1.0f);

            // Pre-warm complete — hide progress UI.
            ShowProgress(false);

            // แสดงโลโก้ 3 ใบเสมอ ก่อนเข้า MainMenu
            yield return StartCoroutine(RunSplashSequence());
        }

        private IEnumerator PreWarmRoutine(List<string> labels, System.Action<float> onProgress)
        {
            // Track the "real" progress from the pre-warmer.
            float realProgress = 0f;
            bool prewarmFinished = false;
            // Also animate a "displayed" progress that lags behind the real
            // progress by a small amount, so the user can see the bar move
            // even if the underlying operation completes near-instantly
            // (e.g. local assets in the editor).
            float displayedProgress = 0f;
            const float displayCatchUp = 4f; // seconds to go 0 -> 1 (slow enough to see)
            const float minVisibleTime = 3.5f; // minimum time the bar is shown
            float startTime = Time.realtimeSinceStartup;

            // Kick off the pre-warmer in the background. The callback updates
            // realProgress; the for-loop below drives the displayed progress.
            var prewarmCoroutine = StartCoroutine(WrapPreWarm(labels, (p) =>
            {
                realProgress = Mathf.Clamp01(p);
                prewarmFinished = (p >= 1f);
            }));

            // Drive the displayed progress until pre-warm is done AND we've
            // shown the bar for at least minVisibleTime AND the bar is
            // visually at 100%.
            while (true)
            {
                displayedProgress = Mathf.MoveTowards(displayedProgress, realProgress,
                    Time.unscaledDeltaTime / displayCatchUp);
                onProgress?.Invoke(displayedProgress);

                float elapsed = Time.realtimeSinceStartup - startTime;
                if (prewarmFinished && elapsed >= minVisibleTime && displayedProgress >= 0.999f)
                {
                    break;
                }
                yield return null;
            }

            // Make sure we hit 100% before exiting.
            displayedProgress = 1f;
            onProgress?.Invoke(1f);
        }

        private IEnumerator WrapPreWarm(List<string> labels, System.Action<float> onProgress)
        {
            yield return AssetPreWarmer.PreWarmAsync(labels, onProgress);
            onProgress?.Invoke(1f);
        }

        private IEnumerator RunSplashSequence()
        {
            if (_cards == null || _cards.Length == 0)
            {
                LoadMainMenu();
                yield break;
            }

            for (int i = 0; i < _cards.Length; i++)
            {
                var card = _cards[i];
                if (card == null) continue;

                // Fade in.
                yield return StartCoroutine(FadeCanvasGroup(card, 0f, 1f, _fadeDuration));

                // Hold.
                yield return new WaitForSecondsRealtime(_holdDuration);

                // Fade out.
                yield return StartCoroutine(FadeCanvasGroup(card, 1f, 0f, _fadeDuration));
            }

            _splashCoroutine = null;
            LoadMainMenu();
        }

        private static IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
        {
            if (cg == null) yield break;
            float elapsed = 0f;
            cg.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                cg.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            cg.alpha = to;
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_mainMenuSceneName))
                _mainMenuSceneName = "MainMenu";
        }

        // === UNIVERSITY LOGO LAYOUT ===
        // วาง sprite โลโก้มหาลัยกลางจอ + ข้อความ "Rajamangala University of Technology Suvarnabhumi"
        // ชิดใต้โลโก้ (ไม่ห่างเกินไป) ใช้ TMP text สีขาว (ไม่ใช้ sprite text — กันกลืนกับ bg ดำ)
        // รองรับ sprite ทั้งแนวตั้ง (0.55:1) และแนวนอน (5:1) — ใช้ aspect ratio จาก sprite โดยตรง
        private void BuildUniversityLayout(Transform parent, Sprite logoSprite, float sh, float sw)
        {
            // อ่าน aspect ratio จาก sprite จริง — fallback เป็น 0.55:1 (แนวตั้ง) ถ้าโหลดไม่ได้
            float spriteAspect = 0.55f;
            if (logoSprite != null && logoSprite.bounds.size.x > 0f && logoSprite.bounds.size.y > 0f)
            {
                spriteAspect = logoSprite.bounds.size.x / logoSprite.bounds.size.y;
            }

            // คำนวณขนาดโลโก้: จำกัดที่ 50% ความสูงจอ เพื่อเหลือที่ให้ text ด้านล่าง
            float maxLogoH = sh * 0.50f;
            float maxLogoW = sw * 0.50f;
            float logoH, logoW;
            // ลอง scale ตาม width ก่อน
            logoW = maxLogoW;
            logoH = logoW / spriteAspect;
            if (logoH > maxLogoH)
            {
                // สูงเกิน — scale ตาม height
                logoH = maxLogoH;
                logoW = logoH * spriteAspect;
            }
            float textH = sh * 0.12f; // พื้นที่ข้อความ (เพิ่มจากเดิม)
            float gap = 30f; // ระยะห่างระหว่างโลโก้กับข้อความ

            // โลโก้ sprite — anchor กลางจอ ขยับขึ้นครึ่ง textH+gap เพื่อให้ text อยู่กลางจอพอดี
            var logoGO = new GameObject("UniversityLogoSprite",
                typeof(RectTransform), typeof(Image));
            logoGO.transform.SetParent(parent, false);
            var lImg = logoGO.GetComponent<Image>();
            lImg.sprite = logoSprite;
            lImg.preserveAspect = true;
            lImg.raycastTarget = false;
            var lRT = logoGO.GetComponent<RectTransform>();
            lRT.anchorMin = new Vector2(0.5f, 0.5f);
            lRT.anchorMax = new Vector2(0.5f, 0.5f);
            lRT.pivot = new Vector2(0.5f, 0.5f);
            lRT.sizeDelta = new Vector2(logoW, logoH);
            // ขยับโลโก้ขึ้นเท่ากับครึ่งหนึ่งของ (textH + gap) เพื่อให้ text อยู่กลางจอ
            lRT.anchoredPosition = new Vector2(0f, (textH + gap) * 0.5f);

            // ข้อความชื่อมหาลัย (TMP) อยู่กลางจอพอดี (ใต้โลโก้)
            var textGO = new GameObject("UniversityNameText",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(parent, false);
            var tTmp = textGO.GetComponent<TextMeshProUGUI>();
            tTmp.text = "Rajamangala University of Technology Suvarnabhumi";
            tTmp.color = Color.white;
            tTmp.fontStyle = FontStyles.Bold;
            tTmp.alignment = TextAlignmentOptions.Center;
            tTmp.enableAutoSizing = true;
            tTmp.fontSizeMin = 18;
            tTmp.fontSizeMax = 56;
            tTmp.raycastTarget = false;

            var tRT = textGO.GetComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0.5f, 0.5f);
            tRT.anchorMax = new Vector2(0.5f, 0.5f);
            tRT.pivot = new Vector2(0.5f, 0.5f);
            tRT.sizeDelta = new Vector2(sw * 0.9f, textH);
            // ขยับ text ลงเท่ากับครึ่งหนึ่งของ logoH + gap + ครึ่ง textH
            tRT.anchoredPosition = new Vector2(0f, -(logoH * 0.5f + gap + textH * 0.5f) + textH * 0.5f);
        }
    }
}
