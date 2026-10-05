using System;
using System.Collections;
using _Game.Scripts;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

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
    public MosquitoZigZagMovement zigZagMovement;
    public bool bInterpolateZigZagMovement = true;
    public float zigZagInterpolationDuration = 3.0f;
    
    private float _lastX;
    private float _lastY;
    private float _horizontalPhase;
    private float _verticalPhase;
    private float _phaseOffset;
    private PhotonView _photonView;
    
    private float _currentHorizontalFrequency;
    private float _currentHorizontalAmplitude;
    private float _currentVerticalFrequency;
    private float _currentVerticalAmplitude;
    
    [HideInInspector] public int mosquitoIndex;
    
    private int _projectilesLayer;
    private int _defaultLayer;
    private int _barrierLayer;
    
    public event Action OnMosquitoHitBoundaries;
    public event Action<int, int> OnMosquitoKilled;
    
    private void Awake()
    {
        _projectilesLayer = LayerMask.NameToLayer("Projectiles");
        _defaultLayer = LayerMask.NameToLayer("Default");
        _barrierLayer = LayerMask.NameToLayer("Barrier");
        
        _photonView = GetComponent<PhotonView>();
        _photonView.Synchronization = ViewSynchronization.Off;
    }

    private void Start()
    {
        _phaseOffset = (mosquitoIndex * 137.5f) % (Mathf.PI * 2f);
        // Initialize per-instance current values from the ScriptableObject
        if (zigZagMovement != null)
        {
            _currentHorizontalFrequency = zigZagMovement.HorizontalZigzagFrequency;
            _currentHorizontalAmplitude = zigZagMovement.HorizontalZigzagAmplitude;
            _currentVerticalFrequency = zigZagMovement.VerticalZigzagFrequency;
            _currentVerticalAmplitude = zigZagMovement.VerticalZigzagAmplitude;
        }
    }

    private void Update()
    {
        ZigZagMovement();
    }

    private void ZigZagMovement()
    {
        double t = PhotonNetwork.Time;
        float x = 0;
        float y = 0;

        if(bInterpolateZigZagMovement)
        {
            x = (float)(Math.Sin(t * _currentHorizontalFrequency + _phaseOffset)) * _currentHorizontalAmplitude;
            y = (float)(Math.Cos(t * _currentVerticalFrequency + _phaseOffset)) * _currentVerticalAmplitude;
        }
        else
        {
            x = (float)(Math.Sin(t * zigZagMovement.HorizontalZigzagFrequency + _phaseOffset)) * zigZagMovement.HorizontalZigzagAmplitude;
            y = (float)(Math.Cos(t * zigZagMovement.VerticalZigzagFrequency + _phaseOffset)) * zigZagMovement.VerticalZigzagAmplitude;
        }

        transform.localPosition += new Vector3(x - _lastX, y - _lastY, 0);
        _lastX = x;
        _lastY = y;
    }

    public void SyncMosquitoWithMasterClient()
    {
        _photonView.RPC(nameof(RpcRequestSyncState), RpcTarget.MasterClient, mosquitoIndex);
    }
    
    [PunRPC]
    private void RpcRequestSyncState(int index, PhotonMessageInfo info)
    {
        _photonView.RPC(nameof(SendSyncState), info.Sender, _lastX, _lastY, _horizontalPhase, _verticalPhase, _currentHorizontalFrequency, _currentHorizontalAmplitude, _currentVerticalFrequency, _currentVerticalAmplitude, _phaseOffset, index, gameObject.activeSelf, transform.position.x, transform.position.y);
    }
    
    [PunRPC]
    private void SendSyncState(float lastX, float lastY, float horizontalPhase, float verticalPhase, float currentHorizontalFrequency, float currentHorizontalAmplitude, float currentVerticalFrequency, float currentVerticalAmplitude, float phaseOffset, int index, bool isActive, float posX, float posY)
    {
        _lastX = lastX;
        _lastY = lastY;
        _horizontalPhase = horizontalPhase;
        _verticalPhase = verticalPhase;
        _currentHorizontalFrequency = currentHorizontalFrequency;
        _currentHorizontalAmplitude = currentHorizontalAmplitude;
        _currentVerticalFrequency = currentVerticalFrequency;
        _currentVerticalAmplitude = currentVerticalAmplitude;
        _phaseOffset = phaseOffset;
        mosquitoIndex = index;
        gameObject.SetActive(isActive);
        transform.position = new Vector3(posX, posY, transform.localPosition.z);
    }

    public void SetZigZagMovementSettings(MosquitoZigZagMovement newSettings)
    {
        _lastX = 0f;
        _lastY = 0f;

        if(bInterpolateZigZagMovement)
        {
            StartCoroutine(InterpolateToNewZigZagSettings(newSettings, zigZagInterpolationDuration));
        }
        else
        {
            zigZagMovement = newSettings;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(other.gameObject.layer == _projectilesLayer)
        {
            var laser = other.GetComponent<Projectile>();
            
            _photonView.RPC(nameof(RpcReplicateDeath), RpcTarget.All, laser.InstigatorId);
        }
        
        if(other.gameObject.layer == _defaultLayer)
        {
            OnMosquitoHitBoundaries?.Invoke();
        }
        
        if(other.gameObject.layer == _barrierLayer)
        {
            _photonView.RPC(nameof(RpcReplicateDeath), RpcTarget.All, -1); // -1 indicates no specific instigator
        }
    }

    [PunRPC]
    private void RpcReplicateDeath(int instigatorId)
    {
        OnMosquitoKilled?.Invoke(points, instigatorId);
        gameObject.SetActive(false);
    }
    
    private IEnumerator InterpolateToNewZigZagSettings(MosquitoZigZagMovement newSettings, float duration)
    {
        // Start from the current per-instance values and interpolate towards the target settings
        float initialHorizontalAmplitude = _currentHorizontalAmplitude;
        float initialVerticalAmplitude = _currentVerticalAmplitude;

        float targetHorizontalAmplitude = newSettings.HorizontalZigzagAmplitude;
        float targetVerticalAmplitude = newSettings.VerticalZigzagAmplitude;

        float elapsedTime = 0f;

        // Interpolate amplitude to 0
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            
            // Lerp per-instance current values towards 0
            _currentHorizontalAmplitude = Mathf.Lerp(initialHorizontalAmplitude, 0f, t);
            _currentVerticalAmplitude = Mathf.Lerp(initialVerticalAmplitude, 0f, t);
            
            yield return null;
        }
        
        _currentHorizontalFrequency = newSettings.HorizontalZigzagFrequency;
        _currentVerticalFrequency = newSettings.VerticalZigzagFrequency;
        
        elapsedTime = 0f; // Reset elapsed time for the next interpolation

        // Interpolate to the new settings
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);

            // Lerp per-instance current values
            _currentHorizontalAmplitude = Mathf.Lerp(0f, targetHorizontalAmplitude, t);
            _currentVerticalAmplitude = Mathf.Lerp(0f, targetVerticalAmplitude, t);

            yield return null;
        }

        // Ensure the final values are set
        // Ensure the final per-instance values are set to the targets
        _currentHorizontalAmplitude = targetHorizontalAmplitude;
        _currentVerticalAmplitude = targetVerticalAmplitude;
        
    }
}
