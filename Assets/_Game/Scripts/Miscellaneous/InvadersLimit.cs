using Photon.Pun;
using System;
using Unity.VisualScripting;
using UnityEngine;

public class InvadersLimit : MonoBehaviourPun
{
    private PhotonView _photonView;

    private void Awake()
    {
        _photonView = GetComponent<PhotonView>();
    }

    public static event Action OnInvadersLimitReached;
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if(!PhotonNetwork.IsMasterClient) return;

        OnInvadersLimitReached?.Invoke();
    }
}
