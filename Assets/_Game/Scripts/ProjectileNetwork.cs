using _Game.Scripts;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class ProjectileNetwork : SceneSingleton<ProjectileNetwork>
{
    public static void RequestDestroy(int instigatorId, int shotId)
    {
        Instance.photonView.RPC(nameof(RpcDestroy), RpcTarget.Others, instigatorId, shotId);
    }
    
    [PunRPC]
    private void RpcDestroy(int instigatorId, int shotId)
    {
        Projectile.DestroyRemote(instigatorId, shotId);
        
        Debug.Log($"Projectile {instigatorId} destroyed {shotId}");
    }
}
