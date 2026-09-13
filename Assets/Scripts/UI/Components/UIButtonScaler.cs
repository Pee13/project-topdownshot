using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TopDownTacticalAI.UI.Components
{
    /// <summary>
    /// Adds Scale animation to buttons on Hover and Press.
    /// Automatically used with UITheme.ApplyToButton().
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class UIButtonScaler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private Button _button;
        private RectTransform _rectTransform;
        private Vector3 _originalScale;
        private float _hoverScale = 1.05f;
        private float _pressScale = 0.95f;
        private AnimationCurve _curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        private float _animationDuration = 0.1f;
        private bool _isHovering = false;
        private bool _isPressing = false;

        public void Initialize(float hoverScale, float pressScale, AnimationCurve curve, float duration = 0.1f)
        {
            _hoverScale = hoverScale;
            _pressScale = pressScale;
            _curve = curve;
            _animationDuration = duration;
            _button = GetComponent<Button>();
            _rectTransform = GetComponent<RectTransform>();
            _originalScale = _rectTransform.localScale;
        }

        private void Awake()
        {
            _button = GetComponent<Button>();
            _rectTransform = GetComponent<RectTransform>();
            _originalScale = _rectTransform.localScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_button != null && _button.interactable)
            {
                _isHovering = true;
                StopAllCoroutines();
                StartCoroutine(ScaleTo(_hoverScale));
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovering = false;
            if (!_isPressing)
            {
                StopAllCoroutines();
                StartCoroutine(ScaleTo(1f));
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_button != null && _button.interactable)
            {
                _isPressing = true;
                StopAllCoroutines();
                StartCoroutine(ScaleTo(_pressScale));
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressing = false;
            StopAllCoroutines();
            StartCoroutine(ScaleTo(_isHovering ? _hoverScale : 1f));
        }

        private System.Collections.IEnumerator ScaleTo(float targetScale)
        {
            float elapsed = 0f;
            Vector3 startScale = _rectTransform.localScale;
            Vector3 endScale = _originalScale * targetScale;

            while (elapsed < _animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _animationDuration);
                float curvedT = _curve.Evaluate(t);
                _rectTransform.localScale = Vector3.LerpUnclamped(startScale, endScale, curvedT);
                yield return null;
            }
            _rectTransform.localScale = endScale;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _rectTransform.localScale = _originalScale;
            _isHovering = false;
            _isPressing = false;
        }
    }
}