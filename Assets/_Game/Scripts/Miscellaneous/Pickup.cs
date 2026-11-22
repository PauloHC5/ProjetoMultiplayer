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
        Medkit,
        
        Default
    }
    
    [Header("Pickup Settings")]
    [SerializeField] private PickupType pickupType = PickupType.Default;
    [SerializeField] private float pickupDuration = 5.0f;
    [SerializeField] private Transform pickupSprite;
    [SerializeField] private Transform pickupShadowSprite;
    
    [Header("PickupMovement Settings")]
    public float frequency = 5.0f;
    public float amplitude = 1.0f;
    
    [Header("PickupShadow Settings")]
    public float shadowFrequency = 5.0f;
    public float shadowAmplitude = 0.1f;
    public float shadowScaleMultiplier = 0.5f;
    
    [Header("SFX")]
    public AudioCue pickupCollectedSfx;
    
    private float _initialY;
    private float _movementOffsetTime;
    private Vector3 _initialPosition;
    
    private PhotonView _photonView;
    
    public PickupType Type => pickupType;
    public float Duration => pickupDuration;

    private void Awake()
    {
        _movementOffsetTime = UnityEngine.Random.Range(0f, Mathf.PI * 2);
        _photonView = GetComponent<PhotonView>();
        _initialPosition = pickupSprite.localPosition;
        _initialY = pickupSprite.localPosition.y;
    }

    private void Update()
    {
        PickupSpriteMovement();
        PickupShadowScaleMovement();
    }
    
    private void PickupSpriteMovement()
    {
        float y = _initialY + Mathf.Sin(Time.time * frequency + _movementOffsetTime) * amplitude;
        pickupSprite.localPosition = _initialPosition + new Vector3(
            0f,
            y,
            0f);
    }
    
    private void PickupShadowScaleMovement()
    {
        float scale = 1f + Mathf.Sin(Time.time * shadowFrequency + _movementOffsetTime) * shadowAmplitude;
        pickupShadowSprite.localScale = new Vector3(scale, scale, 1f) * shadowScaleMultiplier;
    }

    public void RequestDestroyPickup()
    {
        if (PhotonHandlers.CanUseNetwork)
        {
            if (PhotonNetwork.IsMasterClient)
                RpcDestroyPickup();
            else
                _photonView.RPC(nameof(RpcDestroyPickup), RpcTarget.MasterClient);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    [PunRPC]
    private void RpcDestroyPickup()
    {
        PhotonNetwork.Destroy(gameObject);
    }
}
