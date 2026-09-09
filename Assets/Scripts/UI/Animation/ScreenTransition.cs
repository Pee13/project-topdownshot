using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace TopDownTacticalAI.UI.Animation
{
    /// <summary>
    /// Manages screen transitions (Scene Transitions) with Fade and Blur.
    /// Used as a Singleton that persists across scenes (DontDestroyOnLoad).
    /// </summary>
    public class ScreenTransition : MonoBehaviour
    {
        public static ScreenTransition Instance { get; private set; }

        [Header("References")]
        [SerializeField] private Image _fadeImage;
        [SerializeField] private Image _blurImage; // Optional: for blur effect
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Default Settings")]
        [SerializeField] private float _defaultFadeDuration = 0.5f;
        [SerializeField] private Color _fadeColor = Color.black;
        [SerializeField] private AnimationCurve _fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private Coroutine _currentTransition;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Auto-create UI if missing
            if (_fadeImage == null) CreateFadeImage();
            if (_canvasGroup == null) _canvasGroup = _fadeImage.gameObject.AddComponent<CanvasGroup>();
        }

        private void CreateFadeImage()
        {
            var canvas = GetComponentInChildren<Canvas>();
            if (canvas == null)
            {
                var canvasGO = new GameObject("TransitionCanvas");
                canvasGO.transform.SetParent(transform);
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32767; // Topmost
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            var fadeGO = new GameObject("FadeImage");
            fadeGO.transform.SetParent(canvas.transform);
            _fadeImage = fadeGO.AddComponent<Image>();
            _fadeImage.color = new Color(_fadeColor.r, _fadeColor.g, _fadeColor.b, 0f);
            _fadeImage.raycastTarget = false;

            var rt = _fadeImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>Fade In (black -> transparent) - used when entering a new scene.</summary>
        public void FadeIn(float duration = -1f, System.Action onComplete = null)
        {
            float dur = duration > 0 ? duration : _defaultFadeDuration;
            if (_currentTransition != null) StopCoroutine(_currentTransition);
            _currentTransition = StartCoroutine(FadeRoutine(1f, 0f, dur, onComplete));
        }

        /// <summary>Fade Out (transparent -> black) - used when leaving a scene.</summary>
        public void FadeOut(float duration = -1f, System.Action onComplete = null)
        {
            float dur = duration > 0 ? duration : _defaultFadeDuration;
            if (_currentTransition != null) StopCoroutine(_currentTransition);
            _currentTransition = StartCoroutine(FadeRoutine(0f, 1f, dur, onComplete));
        }

        /// <summary>Custom fade with specified from/to values.</summary>
        public void Fade(float from, float to, float duration, System.Action onComplete = null)
        {
            if (_currentTransition != null) StopCoroutine(_currentTransition);
            _currentTransition = StartCoroutine(FadeRoutine(from, to, duration, onComplete));
        }

        private IEnumerator FadeRoutine(float from, float to, float duration, System.Action onComplete)
        {
            _fadeImage.raycastTarget = true;
            _canvasGroup.blocksRaycasts = true;

            float elapsed = 0f;
            Color c = _fadeColor;
            c.a = from;
            _fadeImage.color = c;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curvedT = _fadeCurve.Evaluate(t);
                c.a = Mathf.Lerp(from, to, curvedT);
                _fadeImage.color = c;
                yield return null;
            }

            c.a = to;
            _fadeImage.color = c;

            if (to == 0f)
            {
                _fadeImage.raycastTarget = false;
                _canvasGroup.blocksRaycasts = false;
            }

            onComplete?.Invoke();
        }

        /// <summary>Load a scene with a fade transition.</summary>
        public void LoadSceneWithTransition(string sceneName, float fadeOutDuration = -1f, float fadeInDuration = -1f)
        {
            StartCoroutine(LoadSceneRoutine(sceneName, fadeOutDuration, fadeInDuration));
        }

        private IEnumerator LoadSceneRoutine(string sceneName, float fadeOutDur, float fadeInDur)
        {
            FadeOut(fadeOutDur);
            yield return new WaitForSecondsRealtime(fadeOutDur > 0 ? fadeOutDur : _defaultFadeDuration);

            AsyncOperation async = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
            while (!async.isDone) yield return null;

            FadeIn(fadeInDur);
        }
    }
}