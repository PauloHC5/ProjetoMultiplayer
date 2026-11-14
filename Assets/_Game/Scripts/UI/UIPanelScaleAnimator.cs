using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(RectTransform))]
public class UIPanelScaleAnimator : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private float showDuration = 0.25f;
    [SerializeField] private float hideDuration = 0.2f;
    [SerializeField] private AnimationCurve showCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve hideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool ignoreTimeScale = true;
    [SerializeField] private bool showOnEnable = false;

    [Header("Events")]
    public UnityEvent beginShow;
    public UnityEvent onShown;
    
    public UnityEvent beginHide;
    public UnityEvent onHidden;

    private RectTransform rectTransform;
    private Vector3 normalScale;
    private Coroutine currentRoutine;
    private bool isHiding;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        normalScale = rectTransform.localScale; // the scale set in the editor is the "normal" one
    }

    private void OnEnable()
    {
        isHiding = false;
        rectTransform.localScale = Vector3.zero;
        if (showOnEnable) Show();
    }

    /// <summary>Scales down to zero and then deactivates the panel. Use this instead of SetActive(false).</summary>
    public void Hide()
    {
        if (!gameObject.activeInHierarchy || isHiding) return;
        isHiding = true;
        
        beginHide.Invoke();

        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(ScaleRoutine(rectTransform.localScale, Vector3.zero, hideDuration, hideCurve, () =>
        {
            onHidden.Invoke();
            gameObject.SetActive(false);
        }));
    }

    /// <summary>Activates the panel (the scale up plays automatically in OnEnable).</summary>
    public void Show()
    {
        gameObject.SetActive(true);
        beginShow.Invoke();
        
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(ScaleRoutine(Vector3.zero, normalScale, showDuration, showCurve, onShown.Invoke));
    }

    private IEnumerator ScaleRoutine(Vector3 from, Vector3 to, float duration, AnimationCurve curve, System.Action onComplete)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rectTransform.localScale = Vector3.LerpUnclamped(from, to, curve.Evaluate(t));
            yield return null;
        }

        rectTransform.localScale = to;
        currentRoutine = null;
        onComplete?.Invoke();
    }
}