using System;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public class AutoHostARoom : MonoBehaviourPunCallbacks
{
    public string roomName;
    public int roomMaxPlayers = 0;
    
    public bool bDebugMode = false;
    
    void Awake()
    {
        if (PhotonNetwork.IsConnected)
        {
            if(bDebugMode)
                Debug.Log("Already connected to Photon server as " + PhotonNetwork.NickName  +". Interrupting auto-hosting.");
            return;
        }
        
        PhotonNetwork.AutomaticallySyncScene = true;
        
        PhotonNetwork.NickName = "TestPlayer_" + Random.Range(1000, 9999);
        if(bDebugMode)
            Debug.Log("Connecting to Photon server as " + PhotonNetwork.NickName);
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        if (this.roomName != string.Empty)
        {
            if(bDebugMode)
                Debug.Log("Trying to join room: " + this.roomName);
        
            PhotonNetwork.JoinRoom(this.roomName);
        }
        else
        {
            CreateRoom();
        }
    }

    private void CreateRoom(string inRoomName = "")
    {
        string sceneName = SceneManager.GetActiveScene().name;
        
        RoomOptions options = new RoomOptions
        {
            MaxPlayers = roomMaxPlayers != 0 ? roomMaxPlayers : Random.Range(1, 3),
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable
            {
                { "sceneName", sceneName }
            },
            IsVisible = true
        };
        
        string roomNameToCreate = inRoomName != string.Empty ? inRoomName : GenerateRandomRoomName();
        
        PhotonNetwork.JoinOrCreateRoom(roomNameToCreate, options, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        if(bDebugMode)
            Debug.Log("Joined room: " + PhotonNetwork.CurrentRoom.Name);
        
        if (PhotonNetwork.IsMasterClient && SceneManager.GetActiveScene().name != PhotonNetwork.CurrentRoom.CustomProperties["sceneName"].ToString())
        {
            PhotonNetwork.LoadLevel(SceneManager.GetActiveScene().name);
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        if(bDebugMode)
            Debug.LogWarning($"Failed to join room '{this.roomName}': {message}. Creating a new one.");

        if (message == "Game full") // If the room is full, create a new room with a random name
        {
            CreateRoom();
        }
        else
        {
            CreateRoom(roomName);
        }
    }

    private string GenerateRandomRoomName()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new System.Random();
        return new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}