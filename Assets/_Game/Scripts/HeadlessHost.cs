using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HeadlessHost : MonoBehaviourPunCallbacks
{
    public UI_Chat chatUI;
    
    void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        
        if(!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
            PhotonNetwork.NickName = "TestPlayer_" + Random.Range(1000, 9999);
            Debug.Log("Connecting to Photon server as " + PhotonNetwork.NickName);
        }
        else
        {
            Debug.Log("Already connected to Photon server as " + PhotonNetwork.NickName);
        }
    }

    public override void OnConnectedToMaster()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        
        RoomOptions options = new RoomOptions
        {
            MaxPlayers = Random.Range(1, 3),
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable
            {
                { "sceneName", sceneName }
            },
            IsVisible = true
        };
        
        string roomName = GenerateRandomRoomName();
        
        PhotonNetwork.JoinOrCreateRoom(roomName, options, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Host holding the room: " + PhotonNetwork.CurrentRoom.Name);
        
        if (PhotonNetwork.IsMasterClient && SceneManager.GetActiveScene().name != PhotonNetwork.CurrentRoom.CustomProperties["sceneName"].ToString())
        {
            PhotonNetwork.LoadLevel(SceneManager.GetActiveScene().name);
        }
        
        chatUI.InitChatUI(PhotonNetwork.CurrentRoom.Name, PhotonNetwork.CurrentRoom.PlayerCount, PhotonNetwork.CurrentRoom.MaxPlayers);
    }
    
    private string GenerateRandomRoomName()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new System.Random();
        return new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        int currentPlayers = PhotonNetwork.CurrentRoom.PlayerCount;
        int maxPlayers = PhotonNetwork.CurrentRoom.MaxPlayers;
        chatUI?.OnPlayerEnteredRoom(currentPlayers, maxPlayers);
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        int currentPlayers = PhotonNetwork.CurrentRoom.PlayerCount;
        int maxPlayers = PhotonNetwork.CurrentRoom.MaxPlayers;
        chatUI?.OnPlayerLeftRoom(currentPlayers, maxPlayers);
    }
}