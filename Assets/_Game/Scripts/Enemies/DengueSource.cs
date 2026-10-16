using System;
using System.Collections;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class DengueSource : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int points = 100;
    [SerializeField] private int chanceToSoawnMosquitoOnSpawn = 50; // Percentage chance to spawn a mosquito when the DengueSource spawns
    [SerializeField] private float minSpawnInterval = 3f;
    [SerializeField] private float maxSpawnInterval = 6f;
    
    [Header("Prefabs")]
    public GameObject[] moquitoPrefabs;
    
    [Header("Debug")]
    public bool debugMode;
    
    private MosquitosHorde _mosquitosHorde;

    public Action<DengueSource, int, int> OnDengueSourceDestroyed;

    private void Start()
    {
        _mosquitosHorde = FindAnyObjectByType<MosquitosHorde>();
        
        if (_mosquitosHorde == null)
        {
            Destroy();
            return;
        }
        
        _mosquitosHorde.AllMosquitosDestroyedEvent += DestroyWhenAllMosquitosDestroyed;
        
        var randomValue = UnityEngine.Random.Range(0, 100);
        if (randomValue < chanceToSoawnMosquitoOnSpawn)
        {
            RequestSpawnMosquito();
        }

        Invoke(nameof(RequestSpawnMosquito), UnityEngine.Random.Range(minSpawnInterval, maxSpawnInterval));
    }

    private void RequestSpawnMosquito()
    {
        if(!PhotonNetwork.IsMasterClient) return;

        if (debugMode)
            Debug.Log($"Requesting spawn mosquito");

        _mosquitosHorde.RequestSpawnMosquito(transform.position);
        
        Invoke(nameof(RequestSpawnMosquito), UnityEngine.Random.Range(minSpawnInterval, maxSpawnInterval));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        int playerID = other.GetComponent<PlayerCharacter>().GetPlayerId();
        
        Destroy(playerID);
    }

    private void Destroy(int playerID = -1)
    {
        if(!PhotonNetwork.IsMasterClient) return;
        
        OnDengueSourceDestroyed?.Invoke(this, playerID, points);
        PhotonNetwork.Destroy(gameObject);
    }
    
    private void DestroyWhenAllMosquitosDestroyed()
    {
        Destroy();
    }

    private void OnDestroy()
    {
        if(_mosquitosHorde != null)
            _mosquitosHorde.AllMosquitosDestroyedEvent -= DestroyWhenAllMosquitosDestroyed;
    }
}
