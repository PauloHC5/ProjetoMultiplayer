using System;
using Photon.Pun;
using UnityEngine;

public class Pickup : MonoBehaviour
{
    public enum PickupType
    {
        _1UP,
        FasterFire,
        PiercingProjectiles,
        Repellent,
        SpeedBuff,
        TripleFire,
        Toolbox,
        
        Default
    }
    
    [Header("Pickup Settings")]
    [SerializeField] private PickupType pickupType = PickupType.Default;
    [SerializeField] private float pickupDuration = 5.0f;
    
    [Header("PickupMovement Settings")]
    public float frequency = 5.0f;
    public float amplitude = 1.0f;
    
    [Header("SFX")]
    public AudioCue pickupSfx;
    
    private float _initialY;
    private float _movementOffsetTime;
    
    private PhotonView _photonView;
    
    public PickupType Type => pickupType;
    public float Duration => pickupDuration;

    private void Awake()
    {
        _initialY = transform.localPosition.y;
        _movementOffsetTime = UnityEngine.Random.Range(0f, Mathf.PI * 2);
        _photonView = GetComponent<PhotonView>();
    }

    private void Update()
    {
        PickupMovement();
    }
    
    private void PickupMovement()
    {
        float y = _initialY + Mathf.Sin(Time.time * frequency + _movementOffsetTime) * amplitude;
        transform.localPosition = new Vector3(
            transform.localPosition.x,
            y,
            transform.localPosition.z);
    }
}
