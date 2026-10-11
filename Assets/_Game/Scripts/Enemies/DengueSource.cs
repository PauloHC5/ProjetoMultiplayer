using System;
using System.Collections;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class DengueSource : MonoBehaviour
{
    public GameObject[] moquitoPrefabs;
    
    [Header("Debug")]
    public bool debugMode;

    private PhotonView _photonView;
    private MosquitosHorde _mosquitosHorde;

    private int test = 0;

    private void Awake()
    {
        _photonView = GetComponent<PhotonView>();
    }

    private void Start()
    {
        _mosquitosHorde = FindAnyObjectByType<MosquitosHorde>();

        if(_mosquitosHorde == null)
        {
            //PhotonNetwork.Destroy(gameObject);
        }                  

        InvokeRepeating(nameof(RequestSpawnMosquito), 3f, 3f);
    }

    private void RequestSpawnMosquito()
    {
        if(!PhotonNetwork.IsMasterClient) return;

        if (debugMode)
            Debug.Log($"Requesting spawn mosquito");

        _mosquitosHorde.RequestSpawnMosquito(transform.position);
    }
}
