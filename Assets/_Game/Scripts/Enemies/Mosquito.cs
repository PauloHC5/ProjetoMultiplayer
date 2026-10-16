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

public class Mosquito : MonoBehaviour
{
    [Header("Settings")]
    public int points;

    [SerializeField] private MosquitoType type;
    public MosquitoType Type => type;

    public float positionInterpolationDuration = 0.5f;
    public float scaleUpDuration = 0.5f;

    [Header("Zig Zag Movement Settings")]
    [SerializeField] private MosquitoZigZagMovement zigZagMovement;
    public MosquitoZigZagMovement MosquitoZigZagMovement => zigZagMovement;
    
    [Header("Lissajous Curve Settings")]
    [SerializeField] private LissajousCurve lissajousCurve;
    public LissajousCurve LissajousCurve => lissajousCurve;

    [Header("Debug")]
    public bool debugMode;
    public Vector2 debugGoToPosition;
    public bool moveToHordeCenter = false;
    
    private Vector2 _anchorPos;
    private Vector2 _moveStartPos;
    private Vector2 _moveTargetPos;
    private Vector3 _originalScale;
    private double _moveStartTime;
    private bool _isMoving;
    private Collider2D _collider;
    
    [HideInInspector]
    public MosquitosHorde.HordePositionKey hordePositionKey;
    
    private int _projectilesLayer;
    private int _defaultLayer;
    private int _barrierLayer;
    private Coroutine _moveRoutine;
    
    public event Action OnMosquitoHitBoundaries;
    public event Action<Mosquito, int> OnMosquitoKilled;
    
    private void Awake()
    {
        _projectilesLayer = LayerMask.NameToLayer("Projectiles");
        _defaultLayer = LayerMask.NameToLayer("Default");
        _barrierLayer = LayerMask.NameToLayer("Barrier");
        _originalScale = transform.localScale;
        _collider = GetComponent<Collider2D>();
    }

    private void Start()
    {
        _anchorPos = transform.localPosition;
        
        zigZagMovement.OnStart(this);
        lissajousCurve.OnStart(this);
        
        if(debugMode)
            _moveStartPos = _anchorPos;

        StartCoroutine(ScaleFrom0T1Interpolation());
    }

    private void Update()
    {
        double now = PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode ? PhotonNetwork.Time : Time.timeAsDouble;        
        
        if (_isMoving)
        {
            float t = Mathf.Clamp01((float)((now - _moveStartTime) / positionInterpolationDuration));
            _anchorPos = Vector2.Lerp(_moveStartPos, _moveTargetPos, Mathf.SmoothStep(0f, 1f, t));
            if (t >= 1f) _isMoving = false;
        }

        Vector2 zigZagOffset = zigZagMovement.Movement(now);
        Vector2 lissajousOffset = lissajousCurve.Calculate(transform, now);

        if(moveToHordeCenter)
        {
            transform.localPosition = zigZagOffset + lissajousOffset;
        }
        else
        {
            transform.localPosition = _anchorPos + zigZagOffset + lissajousOffset;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(other.gameObject.layer == _projectilesLayer)
        {
            var laser = other.GetComponent<Projectile>();
            
            OnMosquitoKilled?.Invoke(this, laser.InstigatorId);
        }
        
        if(other.gameObject.layer == _defaultLayer)
        {
            //if(lissajousCurve.HasMovementSettings) return;
            
            OnMosquitoHitBoundaries?.Invoke();
        }
        
        if(other.gameObject.layer == _barrierLayer)
        {
            OnMosquitoKilled?.Invoke(this, -1);
            BarriersManager.Instance.DamageBarrier(other.gameObject);
        }
    }
    
    public void GoToNewPosition(Vector2 newPos, double startTime, Vector2? startPos = null)
    {
        _moveStartPos = startPos ?? _anchorPos;
        _moveTargetPos = newPos;
        _moveStartTime = startTime;
        _isMoving = true;
    }

    private IEnumerator ScaleFrom0T1Interpolation()
    {
        float elapsedTime = 0f;

        transform.localScale = Vector3.zero;

        while (elapsedTime < scaleUpDuration)
        {
            float t = elapsedTime / scaleUpDuration;
            transform.localScale = Vector3.Lerp(Vector3.zero, _originalScale, Mathf.SmoothStep(0f, 1f, t));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.localScale = _originalScale;
        _collider.enabled = true;

    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        zigZagMovement.OnValidate();
        lissajousCurve.SwitchCurveSettings(lissajousCurve.Settings);
        
        if(!debugMode) return;
        
        double now = PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode ? PhotonNetwork.Time : Time.timeAsDouble;
        
        GoToNewPosition(debugGoToPosition, now + 0.1f);
    }
#endif
}
