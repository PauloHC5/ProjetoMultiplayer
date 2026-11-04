using System;
using _Game.Scripts;
using UnityEngine;

public enum AnimMode
{
    PingPong,   
    Continuous 
}

[CreateAssetMenu(fileName = "LissajousSettings", menuName = "Scriptable Objects/LissajousSettings")]
public class LissajouCurveSettings : ScriptableObject
{
    [Range(0, 20)] public float A = 4f;
    public bool animateA = false;
    public Vector2 aScaleRange = new Vector2(0.6f, 1.2f);
    public float aFrequency = 0.5f; 
    
    [Space(20)]
    
    [Range(0, 20)] public float B = 4f;
    public bool animateB = false;
    public Vector2 bScaleRange = new Vector2(0.6f, 1.2f);
    public float bFrequency = 0.5f;
    [Range(0f, 1f)] public float bPhase = 0f;
    
    [Space(20)]
    
    [Range(0, 20)] public float C = 5f;
    public bool C_Random = false;
    
    [Space(20)]
    
    [Range(0, 20)] public float D = 4f;
    public bool D_Random = false;
    
    [Space]
    
    [Header("Delta")]
    [Range(0, 20)] public float delta = 2.3f;
    public bool delta_Random = false;
    public bool animateDelta = false;
    public AnimMode deltaMode = AnimMode.PingPong;
    public float deltaMin = 0f;
    public float deltaMax = 6.28f;
    public float deltaFrequency = 0.1f;
    
    [Header("Transition Mode")]
    public CurveTransitionMode transitionMode = CurveTransitionMode.Blend;

    [Header("Time Multiplier")]
    [Range(0, 20)] public float t_multipler = 2f;
    
    [Header("Offsets")]
    [Range(-20, 20)] public float xOffset = 0f;
    [Range(-20, 20)] public float yOffset = 0f;
    
    [Header("Multiplier")]
    [Range(0, 20)] public float multiplier = 1f;
    
    public Action OnChange;

    private void OnValidate()
    {
        OnChange?.Invoke();
    }
}
