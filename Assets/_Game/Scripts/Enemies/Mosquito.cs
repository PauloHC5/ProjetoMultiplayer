using System;
using System.Collections;
using _Game.Scripts;
using _Game.Scripts.Enemies;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Serialization;

public enum MosquitoType
{
    Normal,
    Infected,
    Queen
}

[RequireComponent(typeof(PhotonView), typeof(PhotonTransformView))]
public class Mosquito : MonoBehaviour
{
    [Header("Settings")]
    public int points;
    public MosquitoType type;
    [SerializeField] private MosquitoMovement mosquitoMovement;
    public MosquitoMovement MosquitoMovement => mosquitoMovement;
    
    
    private float _horizontalPhase;
    private float _verticalPhase;
    private int _animationFrame;
    private float phaseOffset;
    
    
    [HideInInspector] public int mosquitoIndex;
    
    private int _projectilesLayer;
    private int _defaultLayer;
    private int _barrierLayer;
    private Vector2 _originalPos;
    
    public event Action OnMosquitoHitBoundaries;
    public event Action<int, int> OnMosquitoKilled;
    
    private void Awake()
    {
        _projectilesLayer = LayerMask.NameToLayer("Projectiles");
        _defaultLayer = LayerMask.NameToLayer("Default");
        _barrierLayer = LayerMask.NameToLayer("Barrier");        
    }

    private void Start()
    {
        _originalPos = transform.localPosition;

        phaseOffset = (mosquitoIndex * 137.5f) % (Mathf.PI * 2f);
        
        mosquitoMovement.OnStart(phaseOffset, this);
    }

    private void Update()
    {
        transform.localPosition = mosquitoMovement.Movement(PhotonNetwork.Time) + _originalPos;
    }

    /*private void ZigZagMovement()
    {
        double t = PhotonNetwork.Time;
        float x = 0;
        float y = 0;

        if(bInterpolateZigZagMovement)
        {
            x = (float)(Math.Sin(t * _currentHorizontalFrequency + phaseOffset)) * _currentHorizontalAmplitude;
            y = (float)(Math.Cos(t * _currentVerticalFrequency + phaseOffset)) * _currentVerticalAmplitude;
        }
        else
        {
            x = (float)(Math.Sin(t * movementSettings.HorizontalZigzagFrequency + phaseOffset)) * movementSettings.HorizontalZigzagAmplitude;
            y = (float)(Math.Cos(t * movementSettings.VerticalZigzagFrequency + phaseOffset)) * movementSettings.VerticalZigzagAmplitude;
        }

        transform.localPosition = new Vector2(x, y) + _originalPos;
    }*/

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
        
        if(other.gameObject.layer == _barrierLayer)
        {
            OnMosquitoKilled?.Invoke(mosquitoIndex, -1);
            BarriersManager.Instance.DamageBarrier(other.gameObject);
        }
    }
}
