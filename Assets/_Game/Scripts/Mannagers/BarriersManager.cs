using System;
using System.Collections;
using _Game.Scripts;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

[RequireComponent(typeof(PhotonView))]
public class BarriersManager : SceneSingleton<BarriersManager>
{
    [SerializeField] private GameObject[] barriers;
    [SerializeField] private int barrierInitialHealth = 3;
    [SerializeField] private int damagePerHit = 1;
    
    private PhotonView _photonView;
    
    private const string HealthsKey = "barrierHealths";

    private IEnumerator Start()
    {
        yield return new WaitUntil(() => PhotonNetwork.InRoom);
        
        if (PhotonNetwork.IsMasterClient &&
            !PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(HealthsKey))
        {
            var healths = new int[barriers.Length];
            Array.Fill(healths, barrierInitialHealth);
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { HealthsKey, healths } });
        }

        UpdateBarrierStatus();
    }

    public void DamageBarrier(GameObject barrier)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        int i = Array.IndexOf(barriers, barrier);
        if (i < 0) return;

        var healths = (int[])PhotonNetwork.CurrentRoom.CustomProperties[HealthsKey];
        healths = (int[])healths.Clone();
        healths[i] = Mathf.Max(0, healths[i] - damagePerHit);

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { HealthsKey, healths } });
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(HealthsKey))
        {
            UpdateBarrierStatus();
            Debug.Log("Barrier healths updated");
        }
    }
    
    private void UpdateBarrierStatus()
    {
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(HealthsKey, out var obj)) return;
        var healths = (int[])obj;

        for (int i = 0; i < barriers.Length; i++)
            barriers[i].gameObject.SetActive(healths[i] > 0); // visual em todos os clients
    }

    public void RestoreBarrier()
    {
        // Get one barrier that has been destroyed and restore it
        var healths = (int[])PhotonNetwork.CurrentRoom.CustomProperties[HealthsKey];
        
        if(healths == null) Debug.LogError("Healths array is null in RestoreBarrier");
        
        for (int i = 0; i < healths.Length; i++)
        {
            if (healths[i] <= 0)
            {
                healths = (int[])healths.Clone();
                healths[i] = barrierInitialHealth;
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { HealthsKey, healths } });
                break;
            }
        }
    }
    
    public static bool IsAnyBarrierDestroyed()
    {
        if (!PhotonNetwork.InRoom) return false;
        
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(HealthsKey, out var obj)) return false;
        var healths = (int[])obj;

        for (var index = 0; index < healths.Length; index++)
        {
            if (healths[index] <= 0) return true;
        }

        return false;
    }
}
