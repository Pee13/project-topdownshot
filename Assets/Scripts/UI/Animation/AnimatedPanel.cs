using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace TopDownTacticalAI.UI.Animation
{
    /// <summary>
    /// Panel with Fade + Slide enter/exit animations.
    /// Uses UITheme for animation curve and duration.
    /// </summary>
    public class AnimatedPanel : MonoBehaviour
    {
        public enum SlideDirection { None, FromLeft, FromRight, FromTop, FromBottom }

        [Header("Animation Settings")]
        // Made public (was private) so Editor scripts like MenuSetupWizard.cs can set values directly.
        // No need for SerializedProperty/Reflection (previously private caused Error CS1061 in editor scripts).
        [SerializeField] public SlideDirection _slideDirection = SlideDirection.FromRight;
        [SerializeField] private float _slideDistance = 300f;
        [SerializeField] public bool _animateOnEnable = true;
        [SerializeField] private bool _animateOnDisable = true;

        [Header("References (auto-find if empty)")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _rectTransform;

        private Vector2 _originalPosition;
        private Vector2 _hiddenPosition;
        private Coroutine _currentAnimation;
        private bool _isVisible = false;

        private void Awake()
        {
            // Ensure CanvasGroup component exists and is properly initialized
            // Always use GetComponent — if the serialized field is null OR points to a different instance,
            // fall back to the one on this GameObject (and add one if needed). This is required because
            // _canvasGroup is private with [SerializeField], and a null serialized ref in the Inspector
            // used to cause UnassignedReferenceException on first access.
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    Debug.LogError("Failed to add CanvasGroup to " + gameObject.name);
                    return;
                }
            }

            // Initialize CanvasGroup properties for proper animation behavior
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null) return; // guard against missing RectTransform
            _originalPosition = _rectTransform.anchoredPosition;
            CalculateHiddenPosition();
        }

        private void OnEnable()
        {
            // Always start hidden by default — Panel is shown explicitly via Pause()/Show() from controller.
            // Previously called ShowInstant()/Show() here which made panels visible at game start even
            // when the controller intended them to be hidden. The controller's Awake() calls
            // HideAllPanelsInstant() which is the only place that decides visibility at startup.
            //
            // If the user wants the panel to animate-in on enable, they can call Show() manually
            // after enabling the GameObject. The previous _animateOnEnable field is preserved for
            // backward compatibility but its behaviour changed: true = animate in (Show),
            // false = show instantly (ShowInstant). Either way, the panel starts hidden
            // (alpha=0, blocksRaycasts=false) and is made visible by a controller.
            //
            // NOTE: We do NOT auto-show on enable anymore. Callers must call Show()/Pause() explicitly.
        }

        private void OnDisable()
        {
            if (_currentAnimation != null)
                StopCoroutine(_currentAnimation);
        }

        private void CalculateHiddenPosition()
        {
            _hiddenPosition = _originalPosition;
            switch (_slideDirection)
            {
                case SlideDirection.FromLeft: _hiddenPosition.x -= _slideDistance; break;
                case SlideDirection.FromRight: _hiddenPosition.x += _slideDistance; break;
                case SlideDirection.FromTop: _hiddenPosition.y += _slideDistance; break;
                case SlideDirection.FromBottom: _hiddenPosition.y -= _slideDistance; break;
            }
        }

        /// <summary>Show the panel with animation.</summary>
        public void Show(float duration = -1f, AnimationCurve curve = null, System.Action onComplete = null)
        {
            if (_isVisible) { onComplete?.Invoke(); return; }
            _isVisible = true;
            gameObject.SetActive(true);

            if (_currentAnimation != null) StopCoroutine(_currentAnimation);
            _currentAnimation = StartCoroutine(AnimateShow(duration, curve, onComplete));
        }

        /// <summary>Hide the panel with animation (or instantly if _animateOnDisable is false).</summary>
        public void Hide(float duration = -1f, AnimationCurve curve = null, System.Action onComplete = null)
        {
            if (!_isVisible) { onComplete?.Invoke(); return; }
            _isVisible = false;

            if (!_animateOnDisable)
            {
                HideInstant();
                onComplete?.Invoke();
                return;
            }

            if (_currentAnimation != null) StopCoroutine(_currentAnimation);
            _currentAnimation = StartCoroutine(AnimateHide(duration, curve, onComplete));
        }

        /// <summary>Show instantly with no animation.</summary>
        public void ShowInstant()
        {
            _isVisible = true;
            gameObject.SetActive(true);
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            _rectTransform.anchoredPosition = _originalPosition;
        }

        /// <summary>Hide instantly with no animation.</summary>
        public void HideInstant()
        {
            _isVisible = false;
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _rectTransform.anchoredPosition = _hiddenPosition;
            gameObject.SetActive(false);
        }

        private IEnumerator AnimateShow(float duration, AnimationCurve curve, System.Action onComplete)
        {
            float fadeDur = duration > 0 ? duration : 0.25f;
            float slideDur = duration > 0 ? duration : 0.3f;
            AnimationCurve fadeCurve = curve ?? AnimationCurve.EaseInOut(0, 0, 1, 1);
            // Originally used AnimationCurve.EaseOut(...) which doesn't exist in the Unity API (only EaseInOut is provided as a static helper).
            // Causes Error CS0117 — using EaseInOut instead, which gives a similar result (smooth at both ends).
            AnimationCurve slideCurve = curve ?? AnimationCurve.EaseInOut(0, 0, 1, 1);

            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            _rectTransform.anchoredPosition = _hiddenPosition;

            float elapsed = 0f;
            while (elapsed < Mathf.Max(fadeDur, slideDur))
            {
                elapsed += Time.unscaledDeltaTime;

                float fadeT = fadeDur > 0 ? Mathf.Clamp01(elapsed / fadeDur) : 1f;
                float slideT = slideDur > 0 ? Mathf.Clamp01(elapsed / slideDur) : 1f;

                _canvasGroup.alpha = fadeCurve.Evaluate(fadeT);
                _rectTransform.anchoredPosition = Vector2.LerpUnclamped(_hiddenPosition, _originalPosition, slideCurve.Evaluate(slideT));

                yield return null;
            }

            _canvasGroup.alpha = 1f;
            _rectTransform.anchoredPosition = _originalPosition;
            onComplete?.Invoke();
        }

        private IEnumerator AnimateHide(float duration, AnimationCurve curve, System.Action onComplete)
        {
            float fadeDur = duration > 0 ? duration : 0.2f;
            float slideDur = duration > 0 ? duration : 0.25f;
            AnimationCurve fadeCurve = curve ?? AnimationCurve.EaseInOut(0, 0, 1, 1);
            // Originally used AnimationCurve.EaseIn(...) which also doesn't exist in the Unity API — using EaseInOut instead.
            AnimationCurve slideCurve = curve ?? AnimationCurve.EaseInOut(0, 0, 1, 1);

            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            float elapsed = 0f;
            while (elapsed < Mathf.Max(fadeDur, slideDur))
            {
                elapsed += Time.unscaledDeltaTime;

                float fadeT = fadeDur > 0 ? Mathf.Clamp01(elapsed / fadeDur) : 1f;
                float slideT = slideDur > 0 ? Mathf.Clamp01(elapsed / slideDur) : 1f;

                _canvasGroup.alpha = 1f - fadeCurve.Evaluate(fadeT);
                _rectTransform.anchoredPosition = Vector2.LerpUnclamped(_originalPosition, _hiddenPosition, slideCurve.Evaluate(slideT));

                yield return null;
            }

            _canvasGroup.alpha = 0f;
            _rectTransform.anchoredPosition = _hiddenPosition;
            gameObject.SetActive(false);
            onComplete?.Invoke();
        }

        public bool IsVisible => _isVisible;
        public bool IsAnimating => _currentAnimation != null;
    }
}
