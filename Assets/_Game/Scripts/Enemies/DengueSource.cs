using System;
using System.Collections;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class DengueSource : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float spawnInterval = 3f;
    
    [Header("Prefabs")]
    public GameObject[] moquitoPrefabs;
    
    [Header("Debug")]
    public bool debugMode;

    private PhotonView _photonView;
    private MosquitosHorde _mosquitosHorde;

    public Action<DengueSource> OnDengueSourceDestroyed;

    private void Awake()
    {
        _photonView = GetComponent<PhotonView>();
    }

    private void Start()
    {
        _mosquitosHorde = FindAnyObjectByType<MosquitosHorde>();

        if(_mosquitosHorde == null)
        {
            OnDengueSourceDestroyed?.Invoke(this);
            PhotonNetwork.Destroy(gameObject);
        }                  

        InvokeRepeating(nameof(RequestSpawnMosquito), spawnInterval, spawnInterval);
    }

    private void RequestSpawnMosquito()
    {
        if(!PhotonNetwork.IsMasterClient) return;

        if (debugMode)
            Debug.Log($"Requesting spawn mosquito");

        _mosquitosHorde.RequestSpawnMosquito(transform.position);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        OnDengueSourceDestroyed?.Invoke(this);
        PhotonNetwork.Destroy(gameObject);
    }
}
