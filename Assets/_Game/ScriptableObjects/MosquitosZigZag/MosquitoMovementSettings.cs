using UnityEngine;

[CreateAssetMenu(fileName = "MosquitoZigZagMovement", menuName = "Scriptable Objects/MosquitoZigZagMovement")]
public class MosquitoMovementSettings : ScriptableObject
{
    [Header("Horizontal Movement")]
    [Range(0f, 50f)]
    [SerializeField] private float horizontalZigzagFrequency = 1.0f;
    [Range(0f, 10f)]
    [SerializeField] private float horizontalZigzagAmplitude = 1.0f;
    
    [Header("Vertical Movement")]
    [Range(0f, 50f)]
    [SerializeField] private float verticalZigzagFrequency = 1.0f;
    [Range(0f, 10f)]
    [SerializeField] private float verticalZigzagAmplitude = 1.0f;
    
    public float HorizontalZigzagFrequency => horizontalZigzagFrequency;
    public float HorizontalZigzagAmplitude => horizontalZigzagAmplitude;
    public float VerticalZigzagFrequency => verticalZigzagFrequency;
    public float VerticalZigzagAmplitude => verticalZigzagAmplitude;
}
