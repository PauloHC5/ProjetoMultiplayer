using System;
using System.Collections;
using _Game.Scripts;
using Photon.Pun;
using UnityEngine;

public class Mosquito : MonoBehaviour
{
    [Header("Settings")]
    public int points;
    
    [Header("ZigZag Movement")]
    [Range(0f, 50f)]
    public float horizontalZigzagFrequency = 1.0f;
    [Range(0f, 10f)]
    public float horizontalZigzagAmplitude = 1.0f;
    [Range(0f, 50f)]
    public float verticalZigzagFrequency = 1.0f;
    [Range(0f, 10f)]
    public float verticalZigzagAmplitude = 1.0f;
    
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
}
