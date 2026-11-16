using System;
using System.Collections;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

[RequireComponent(typeof(RectTransform))]
public class UIButtonScaleAnimator : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Serializable]
    private enum ButtonOnClickAction
    {
        None,
        ShrinkButton,
        Pulse
    }
    
    [Header("Scale")]
    [SerializeField] private Vector3 normalScale = Vector3.one;
    [SerializeField] private Vector3 hoverScale = new Vector3(1.15f, 1.15f, 1f);

    [Header("Animation")]
    [SerializeField] private ButtonOnClickAction onClickAction = ButtonOnClickAction.ShrinkButton;
    
    [ShowIf("onClickAction", ButtonOnClickAction.Pulse)]
    [SerializeField] private Vector3 pulseScale = new Vector3(1.2f, 1.2f, 1f);
    
    [SerializeField] private float hoverDuration = 0.15f;
    [SerializeField] private float clickDuration = 0.2f;
    [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool ignoreTimeScale = true;
    [SerializeField] private bool scaleUpOnEnable = true;

    [Header("Events")]
    
    [Tooltip("Invoked when the pointer enters the button.")]
    public UnityEvent onPointerEnter;
    
    [Tooltip("Invoked when the scale down to zero finishes.")]
    public UnityEvent onClickAnimationFinished;

    private RectTransform _rectTransform;
    private Coroutine _currentRoutine;
    private bool _clicked;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        _clicked = false;
        _rectTransform.localScale = scaleUpOnEnable ? Vector3.zero : normalScale;
        
        if(scaleUpOnEnable) ScaleTo(normalScale, hoverDuration);
    }

    private void OnDisable()
    {
        _currentRoutine = null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_clicked) return;
        ScaleTo(hoverScale, hoverDuration);
        onPointerEnter?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_clicked) return;
        ScaleTo(normalScale, hoverDuration);
        
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_clicked || eventData.button != PointerEventData.InputButton.Left) return;
        
        switch (onClickAction)
        {
            case ButtonOnClickAction.ShrinkButton:
                ShrinkButton();
                break;
            case ButtonOnClickAction.Pulse:
                _currentRoutine = StartCoroutine(PulseAnimation());
                break;
        }
    }

    /// <summary>Resets the button to its normal scale (e.g. when reopening a menu).</summary>
    public void ResetButton()
    {
        _clicked = false;
        ScaleTo(normalScale, hoverDuration);
    }
    
    public void ShrinkButton()
    {
        _clicked = true;
        ScaleTo(Vector3.zero, clickDuration, onClickAnimationFinished.Invoke);
    }

    private void ScaleTo(Vector3 target, float duration, System.Action onComplete = null)
    {
        if(!gameObject.activeInHierarchy) return;
        
        if (_currentRoutine != null) StopCoroutine(_currentRoutine);
        _currentRoutine = StartCoroutine(ScaleRoutine(target, duration, onComplete));
    }

    private IEnumerator ScaleRoutine(Vector3 target, float duration, System.Action onComplete)
    {
        Vector3 start = _rectTransform.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _rectTransform.localScale = Vector3.LerpUnclamped(start, target, curve.Evaluate(t));
            yield return null;
        }

        _rectTransform.localScale = target;
        _currentRoutine = null;
        onComplete?.Invoke();
    }
    
    private IEnumerator PulseAnimation()
    {
        _clicked = true;
        Vector3 originalScale = _rectTransform.localScale;
        Vector3 targetScale = pulseScale;
        float halfDuration = clickDuration / 2f;

        // Scale up
        yield return ScaleRoutine(targetScale, halfDuration, null);
        
        // Scale back down
        yield return ScaleRoutine(originalScale, halfDuration, () =>
        {
            _clicked = false; // Reset clicked state after pulse animation
        });
    }
}