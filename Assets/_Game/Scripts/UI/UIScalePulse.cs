using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class UIScalePulse : MonoBehaviour
{
    [Header("Scale")]
    [SerializeField] private Vector3 minScale = Vector3.one;
    [SerializeField] private Vector3 maxScale = new Vector3(1.2f, 1.2f, 1f);

    [Header("Animation")]
    [SerializeField] private float duration = 0.8f;        // time for one way (min -> max)
    [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool ignoreTimeScale = true;  // useful for menus while the game is paused

    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        rectTransform.localScale = minScale;
    }

    private void Update()
    {
        float time = ignoreTimeScale ? Time.unscaledTime : Time.time;

        // PingPong goes 0 -> duration -> 0 ... normalized here to 0..1
        float t = Mathf.PingPong(time, duration) / duration;

        rectTransform.localScale = Vector3.LerpUnclamped(minScale, maxScale, curve.Evaluate(t));
    }
}