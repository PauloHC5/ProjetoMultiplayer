using _Game.Scripts.Enemies;
using Photon.Pun;
using UnityEngine;

public class TestMosquitoQueenSpawn : MonoBehaviour
{
    public MosquitoQueen mosquitoQueenPrefab;
    public float spawnDelay = 3f;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Invoke(nameof(SpawnMosquitoQueen), spawnDelay);
    }

    private void SpawnMosquitoQueen()
    {
        if(PhotonHandlers.CanUseNetwork)
        {
            if(!PhotonNetwork.IsMasterClient) return;
            
            PhotonNetwork.Instantiate(mosquitoQueenPrefab.name, mosquitoQueenPrefab.transform.position, mosquitoQueenPrefab.transform.rotation);
        }
        else
        {
            Instantiate(mosquitoQueenPrefab);
        }
    }
}
