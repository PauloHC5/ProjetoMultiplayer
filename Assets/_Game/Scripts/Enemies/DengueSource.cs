using System;
using System.Collections;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class DengueSource : MonoBehaviour
{
    public GameObject[] moquitoPrefabs;

    private PhotonView _photonView;
    private MosquitosHorde _mosquitosHorde;

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

        _photonView.RPC(nameof(SpawnMosquito), RpcTarget.All);        
    }


    [PunRPC]
    private void SpawnMosquito()
    {
        var mosquitoToSpawn = moquitoPrefabs[UnityEngine.Random.Range(0, moquitoPrefabs.Length)];        

        Mosquito mosquitoSpawned = Instantiate(mosquitoToSpawn, transform.transform.position, Quaternion.identity).GetComponent<Mosquito>();                 
        
        StartCoroutine(MoveMosquito(mosquitoSpawned));

        //mosquitoSpawned.GoToNewPosition(new Vector2(-14f, 10f), PhotonNetwork.Time + 0.1f);
    }

    private IEnumerator MoveMosquito(Mosquito mosquito)
    {
        yield return new WaitForSeconds(0.3f);

        mosquito.GoToNewPosition(new Vector2(-14f, 10f), PhotonNetwork.Time + 0.1f);

        yield return new WaitForSeconds(3f);

        mosquito.transform.SetParent(_mosquitosHorde.transform);
    }
}
