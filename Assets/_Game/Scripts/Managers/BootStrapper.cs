using _Game.Scripts.Enemies;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

public static class BootStrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void BeforeSceneLoad()
    {
        GameManager gameManager = GameManager.Instance;
    }
}

public static class PhotonTypes
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterTypes()
    {
        PhotonPeer.RegisterType(typeof(MosquitosHorde.HordePositionKey), (byte)'H', SerializeHordePositionKey,
            DeserializeHordePositionKey);
        
        PhotonPeer.RegisterType(typeof(MosquitoQueen.QueenState), (byte)'E', SerializeQueenState, DeserializeQueenState);
    }

    static byte[] SerializeHordePositionKey(object customType)
    {
        var d = (MosquitosHorde.HordePositionKey)customType;
        byte[] bytes = new byte[8];
        int i = 0;
        Protocol.Serialize(d.col, bytes, ref i);
        Protocol.Serialize(d.row, bytes, ref i);
        return bytes;
    }

    static object DeserializeHordePositionKey(byte[] data)
    {
        var d = new MosquitosHorde.HordePositionKey();
        int i = 0;
        Protocol.Deserialize(out d.col, data, ref i);
        Protocol.Deserialize(out d.row, data, ref i);
        return d;
    }
    
    private static byte[] SerializeQueenState(object obj)
    {
        var s = (MosquitoQueen.QueenState)obj;
        return new[] { (byte)s.Phase, (byte)s.BehaviourIndex, (byte)s.MovementIndex, (byte)s.MosquitoPatrolMovementIndex };
    }

    private static object DeserializeQueenState(byte[] bytes)
    {
        return new MosquitoQueen.QueenState((MosquitoQueen.QueenPhase)bytes[0], bytes[1], bytes[2], bytes[3]);
    }
}

public static class PhotonHandlers
{
    public static double Now()
    {
        return PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode ? PhotonNetwork.Time : Time.timeAsDouble;
    }
    
    public static bool CanUseNetwork => PhotonNetwork.InRoom && PhotonNetwork.IsConnected && !PhotonNetwork.OfflineMode;
    
    /// <summary>Quem decide. Sem sala, o próprio client é a autoridade.</summary>
    public static bool IsAuthority => !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;
}