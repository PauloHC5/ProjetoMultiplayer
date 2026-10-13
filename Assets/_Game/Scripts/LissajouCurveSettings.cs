using System;
using UnityEngine;

[CreateAssetMenu(fileName = "LissajousSettings", menuName = "Scriptable Objects/LissajousSettings")]
public class LissajouCurveSettings : ScriptableObject
{
    [Range(0, 20)] public float A = 4f;
    public bool A_Random = false;
    [Range(0, 20)] public float B = 4f;
    public bool B_Random = false;
    [Range(0, 20)] public float C = 5f;
    public bool C_Random = false;
    [Range(0, 20)] public float D = 4f;
    public bool D_Random = false;
    [Range(0, 20)] public float delta = 2.3f;
    public bool delta_Random = false;
    
    [Range(0, 5)] public float t_multipler = 2f;
    [Range(-20, 20)] public float xOffset = 0f;
    [Range(-20, 20)] public float yOffset = 0f;
    
    [Header("Multiplier")]
    [Range(0, 20)] public float multiplier = 1f;
    public bool pulseMultiplier = false;
    [Range(0, 5)]
    public float pulseMin = 0.5f;
    [Range(0, 5)]
    public float pulseMax = 1.5f;
    [Range(0, 5)]
    public float pulseFrequency = 0.5f;
    
    public Action OnChange;

    private void OnValidate()
    {
        OnChange?.Invoke();
    }
}
