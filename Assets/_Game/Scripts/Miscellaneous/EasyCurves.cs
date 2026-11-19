using UnityEngine;

public static class EasyCurves
{
    public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
    public static float InQuad(float t) => t * t;

    public static float InBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return c3 * t * t * t - c1 * t * t;
    }

    public static float OutBounce(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;
        if (t < 1f / d1) return n1 * t * t;
        if (t < 2f / d1) return n1 * (t -= 1.5f / d1) * t + 0.75f;
        if (t < 2.5f / d1) return n1 * (t -= 2.25f / d1) * t + 0.9375f;
        return n1 * (t -= 2.625f / d1) * t + 0.984375f;
    }

    public static float InOutBounce(float t)
    {
        return t < 0.5 ? (1 - OutBounce(1 - 2 * t)) / 2
                        : (1 + OutBounce(2 * t - 1)) / 2;
    }

    public static float OutElastic(float t)
    {
        const float c4 = (2f * Mathf.PI) / 3f;
        return t == 0f ? 0f
             : t == 1f ? 1f
             : Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
    }

    public static float InOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c2 = c1 * 1.525f;
        return t < 0.5
            ? (Mathf.Pow(2f * t, 2f) * ((c2 + 1f) * 2f * t - c2)) / 2f
            : (Mathf.Pow(2f * t - 2f, 2f) * ((c2 + 1f) * (t * 2f - 2f) + c2) + 2f) / 2f;
    }

    public static float Amplify(float eased, float t, float amplitude)
    => t + (eased - t) * amplitude;
}
