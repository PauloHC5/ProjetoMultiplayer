using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

[RequireComponent(typeof(RectTransform))]
public class UIButtonScaleAnimator : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Scale")]
    [SerializeField] private Vector3 normalScale = Vector3.one;
    [SerializeField] private Vector3 hoverScale = new Vector3(1.15f, 1.15f, 1f);

    [Header("Animation")]
    [SerializeField] private float hoverDuration = 0.15f;
    [SerializeField] private float clickDuration = 0.2f;
    [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool ignoreTimeScale = true;

    [Header("Events")]
    [Tooltip("Invoked when the scale down to zero finishes.")]
    public UnityEvent onClickAnimationFinished;

    private RectTransform rectTransform;
    private Coroutine currentRoutine;
    private bool clicked;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        clicked = false;
        rectTransform.localScale = normalScale;
    }

    private void OnDisable()
    {
        currentRoutine = null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (clicked) return;
        ScaleTo(hoverScale, hoverDuration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (clicked) return;
        ScaleTo(normalScale, hoverDuration);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (clicked) return;
        clicked = true;
        ScaleTo(Vector3.zero, clickDuration, onClickAnimationFinished.Invoke);
    }

    /// <summary>Resets the button to its normal scale (e.g. when reopening a menu).</summary>
    public void ResetButton()
    {
        clicked = false;
        ScaleTo(normalScale, hoverDuration);
    }

    private void ScaleTo(Vector3 target, float duration, System.Action onComplete = null)
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(ScaleRoutine(target, duration, onComplete));
    }

    private IEnumerator ScaleRoutine(Vector3 target, float duration, System.Action onComplete)
    {
        Vector3 start = rectTransform.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rectTransform.localScale = Vector3.LerpUnclamped(start, target, curve.Evaluate(t));
            yield return null;
        }

        rectTransform.localScale = target;
        currentRoutine = null;
        onComplete?.Invoke();
    }
}