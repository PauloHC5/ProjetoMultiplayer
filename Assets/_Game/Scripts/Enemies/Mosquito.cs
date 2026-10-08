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
    [SerializeField] private MosquitoMovement mosquitoMovement;
    public MosquitoMovement MosquitoMovement => mosquitoMovement;
    
    [Header("Debug")]
    public bool debugMode;
    public Vector2 debugGoToPosition;
    
    private float _phaseOffset;
    private Vector2 _anchorPos;
    private Vector2 _moveStartPos;
    private Vector2 _moveTargetPos;
    private Vector3 _originalScale;
    private double _moveStartTime;
    private bool _isMoving;
    private MosquitosHorde.HordePositionKey _currentHordePosition;
    public MosquitosHorde.HordePositionKey CurrentHordePosition => _currentHordePosition;
    
    [HideInInspector] public int mosquitoIndex;
    
    private int _projectilesLayer;
    private int _defaultLayer;
    private int _barrierLayer;
    private Coroutine _moveRoutine;
    
    public event Action OnMosquitoHitBoundaries;
    public event Action<int, int> OnMosquitoKilled;
    
    private void Awake()
    {
        _projectilesLayer = LayerMask.NameToLayer("Projectiles");
        _defaultLayer = LayerMask.NameToLayer("Default");
        _barrierLayer = LayerMask.NameToLayer("Barrier");
        _originalScale = transform.localScale;
    }

    private void Start()
    {
        _anchorPos = transform.localPosition;
        _phaseOffset = (mosquitoIndex * 137.5f) % (Mathf.PI * 2f);
        mosquitoMovement.OnStart(_phaseOffset, this);

        StartCoroutine(ScaleFrom0T1Interpolation());
    }

    private void Update()
    {
        double now = PhotonNetwork.Time;
        
        if (_isMoving)
        {
            float t = Mathf.Clamp01((float)((now - _moveStartTime) / positionInterpolationDuration));
            _anchorPos = Vector2.Lerp(_moveStartPos, _moveTargetPos, Mathf.SmoothStep(0f, 1f, t));
            if (t >= 1f) _isMoving = false;
        }
        
        transform.localPosition = mosquitoMovement.Movement(PhotonNetwork.Time, _anchorPos);
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
        
        if(other.gameObject.layer == _barrierLayer)
        {
            OnMosquitoKilled?.Invoke(mosquitoIndex, -1);
            BarriersManager.Instance.DamageBarrier(other.gameObject);
        }
    }
    
    public void ChangeMosquitoHordePosition(MosquitosHorde.HordePositionKey hordePositionKey, Vector2 newPos, double startTime, Vector2 newAnchorPos = default(Vector2))
    {
        if(newAnchorPos != default(Vector2))
        {
            _anchorPos = newAnchorPos;
        }
        
        _currentHordePosition = hordePositionKey;
        GoToNewPosition(newPos, startTime);
    }
    
    public void GoToNewPosition(Vector2 newPos, double startTime)
    {
        _moveStartPos = _anchorPos;
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

    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if(!debugMode) return;
        
        mosquitoMovement.OnValidate();
        
        GoToNewPosition(debugGoToPosition, PhotonNetwork.Time + 0.1f);
    }
#endif
}
