using Photon.Pun;
using System;
using Unity.VisualScripting;
using UnityEngine;

public class MosquitosLimit : MonoBehaviourPun
{
    private PhotonView _photonView;

    private void Awake()
    {
        _photonView = GetComponent<PhotonView>();
    }

    public static event Action OnMosquitosReachedLimit;
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if(!PhotonNetwork.IsMasterClient) return;

        OnMosquitosReachedLimit?.Invoke();
    }
}
