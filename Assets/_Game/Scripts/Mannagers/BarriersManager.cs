using System;
using System.Collections;
using System.Linq;
using _Game.Scripts;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

[RequireComponent(typeof(PhotonView))]
public class BarriersManager : SceneSingleton<BarriersManager>
{
    [SerializeField] private GameObject[] barriers;
    
    private PhotonView _photonView;
    
    private const string BarriersActiveKey = "BarriersActive";

    private IEnumerator Start()
    {
        yield return new WaitUntil(() => PhotonNetwork.InRoom);
        
        if (PhotonNetwork.IsMasterClient &&
            !PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(BarriersActiveKey))
        {
            bool[] barriersActive = new bool[barriers.Length];
            for (int i = 0; i < barriers.Length; i++)
                barriersActive[i] = true;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { BarriersActiveKey, barriersActive } });
        }

        UpdateBarrierStatus();
    }

    public void DamageBarrier(GameObject barrier)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        int i = Array.IndexOf(barriers, barrier);
        if (i < 0) return;

        bool[] barriersActive = (bool[])PhotonNetwork.CurrentRoom.CustomProperties[BarriersActiveKey];
        barriersActive = (bool[])barriersActive.Clone();
        barriersActive[i] = false;

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { BarriersActiveKey, barriersActive } });
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(BarriersActiveKey))
        {
            UpdateBarrierStatus();
            Debug.Log("Barrier healths updated");
        }
    }
    
    private void UpdateBarrierStatus()
    {
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(BarriersActiveKey, out var obj)) return;
        var barriersActive = (bool[])obj;

        for (int i = 0; i < barriers.Length; i++)
            barriers[i].gameObject.SetActive(barriersActive[i]);
    }

    public void RestoreBarrier()
    {
        // Get one barrier that has been destroyed and restore it
        bool[] barriersActive = (bool[])PhotonNetwork.CurrentRoom.CustomProperties[BarriersActiveKey];
        
        if(barriersActive == null) Debug.LogError("BarriersActive array is null in RestoreBarrier");
        
        for (int i = 0; i < barriersActive.Length; i++)
        {
            if (barriersActive[i] == false)
            {
                barriersActive = (bool[])barriersActive.Clone();
                barriersActive[i] = true;
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { BarriersActiveKey, barriersActive } });
                break;
            }
        }
    }
    
    public static bool IsAnyBarrierDestroyed()
    {
        if (!PhotonNetwork.InRoom) return false;
        
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(BarriersActiveKey, out var obj)) return false;
        var barriersActive = (bool[])obj;

        return barriersActive.Any(t => !t);
    }
}
