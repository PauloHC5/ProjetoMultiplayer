using System;
using System.Collections;
using _Game.Scripts;
using Photon.Pun;
using UnityEngine;

public enum MosquitoType
{
    Normal,
    Infected,
    Queen
}

public class Mosquito : MonoBehaviour
{
    [Header("Settings")]
    public int points;
    public MosquitoType type;
    
    [Header("ZigZag Movement")]
    [Range(0f, 50f)]
    public float horizontalZigzagFrequency = 1.0f;
    [Range(0f, 10f)]
    public float horizontalZigzagAmplitude = 1.0f;
    [Range(0f, 50f)]
    public float verticalZigzagFrequency = 1.0f;
    [Range(0f, 10f)]
    public float verticalZigzagAmplitude = 1.0f;
    public float amountToIncreaseAmplitude = 0.5f;
    public float amplitudeIncreaseDuration = 2f;
    public float frequencyDecreaseAmount = 5f;
        
    
    private float _lastX;
    private float _lastY;
    private float _horizontalPhase;
    private float _verticalPhase;
    private int _animationFrame;
    private float phaseOffset; 
    
    [HideInInspector] public int mosquitoIndex;
    
    private int _projectilesLayer;
    private int _defaultLayer;
    
    public event Action OnMosquitoHitBoundaries;
    public event Action<int, int> OnMosquitoKilled;
    
    private void Awake()
    {
        _projectilesLayer = LayerMask.NameToLayer("Projectiles");
        _defaultLayer = LayerMask.NameToLayer("Default");
    }

    private void Start()
    {
        phaseOffset = (mosquitoIndex * 137.5f) % (Mathf.PI * 2f);
    }

    private void Update()
    {
        ZigZagMovement();
    }

    private void ZigZagMovement()
    {
        double t = PhotonNetwork.Time;

        float x = (float)(Math.Sin(t * horizontalZigzagFrequency + phaseOffset)) * horizontalZigzagAmplitude;
        float y = (float)(Math.Cos(t * verticalZigzagFrequency + phaseOffset)) * verticalZigzagAmplitude;

        transform.localPosition += new Vector3(x - _lastX, y - _lastY, 0);
        _lastX = x;
        _lastY = y;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(other.gameObject.layer == _projectilesLayer)
        {
            var laser = other.GetComponent<Projectile>();
            
            OnMosquitoKilled?.Invoke(mosquitoIndex, laser.InstigatorId);
        }
        
        if(other.gameObject.layer == _defaultLayer)
        {
            OnMosquitoHitBoundaries?.Invoke();
        }
    }

    public void ChangeZigZagMovement()
    {
        if(!gameObject.activeInHierarchy) return;
        
        _lastX = 0f;
        _lastY = 0f;
        
        StartCoroutine(IncreaseAmplitudeCoroutine());

        Mathf.Clamp(horizontalZigzagFrequency -= frequencyDecreaseAmount, 0.1f, 50f);
        Mathf.Clamp(verticalZigzagFrequency -= frequencyDecreaseAmount, 0.1f, 50f);
    }
    
    private IEnumerator IncreaseAmplitudeCoroutine()
    {
        float initialHorizontalAmplitude = horizontalZigzagAmplitude;
        float initialVerticalAmplitude = verticalZigzagAmplitude;
        float targetHorizontalAmplitude = initialHorizontalAmplitude + amountToIncreaseAmplitude;
        float targetVerticalAmplitude = initialVerticalAmplitude + amountToIncreaseAmplitude;

        float duration = amplitudeIncreaseDuration; // Duration of the interpolation
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);

            horizontalZigzagAmplitude = Mathf.Lerp(initialHorizontalAmplitude, targetHorizontalAmplitude, t);
            verticalZigzagAmplitude = Mathf.Lerp(initialVerticalAmplitude, targetVerticalAmplitude, t);

            yield return null;
        }

        // Ensure the final values are set
        horizontalZigzagAmplitude = targetHorizontalAmplitude;
        verticalZigzagAmplitude = targetVerticalAmplitude;
    }
    
    
}
