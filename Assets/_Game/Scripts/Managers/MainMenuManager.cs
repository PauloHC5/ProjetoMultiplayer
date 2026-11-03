using System;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviourPunCallbacks
{
    public UI_Login login;
    public UI_Lobby lobby;

    public const int MaxNickNameLength = 18;
    public const string GameSceneName = "GameScene";

    private Dictionary<string, RoomInfo> _availableRooms = new Dictionary<string, RoomInfo>();

    private void Awake()
    {
        login.gameObject.SetActive(true);
        lobby.gameObject.SetActive(false);
        
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    public static void Login(string nickName)
    {
        if (string.IsNullOrEmpty(nickName))
        {
            Debug.LogError("Nickname cannot be empty to login.");
            return;
        }

        PhotonNetwork.NickName = nickName;
        PhotonNetwork.ConnectUsingSettings();
    }
    
    public static void JoinRoom(string roomName)
    {
        if(string.IsNullOrEmpty(roomName))
        {
            Debug.LogError("Room name cannot be empty to join a room.");
            return;
        }
        
        if (PhotonNetwork.NetworkClientState != ClientState.JoinedLobby)
        {
            Debug.LogWarning("Cannot join room right now, client state: " + PhotonNetwork.NetworkClientState);
            return;
        }
        
        PhotonNetwork.JoinRoom(roomName);
    }

    public override void OnConnectedToMaster()
    {
        login.gameObject.SetActive(false);
        
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        lobby.gameObject.SetActive(true);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("MainMenuManager: Joined room " + PhotonNetwork.CurrentRoom.Name);
        
        string roomSceneName = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("sceneName", out object value) ? value.ToString() : null;

        LoadRoomScene(roomSceneName);
    }
    
    private void LoadRoomScene(string sceneName)
    {
        if(string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("Room scene name is not set in the room's custom properties. Cannot load the room scene.");
            return;
        }
        
        if(SceneManager.GetActiveScene().name == sceneName) return;
        
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel(sceneName);
        }
        else
        {
            Debug.LogWarning("Only the master client can load the room scene.");
        }
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        // Update the available rooms dictionary based on the received room list
        foreach (var info in roomList)
        {
            if (info.RemovedFromList) // If the room is removed from the list, remove it from the available rooms dictionary
                _availableRooms.Remove(info.Name);
            
            else // If the room is added or updated, add or update it in the available rooms dictionary
                _availableRooms[info.Name] = info;
        }
        
        Debug.Log($"Updated room list. Total available rooms: {_availableRooms.Count}");
        
        lobby.UpdateRoomList(_availableRooms);
    }

    public static void CreateRoom(string roomName)
    {
        if(string.IsNullOrEmpty(roomName))
        {
            Debug.LogError("Room name cannot be empty to create a room.");
            return;
        }

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = 3,
            IsVisible = true,
            IsOpen = true,
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable
            {
                { "sceneName",  GameSceneName }
            }
        };

        PhotonNetwork.CreateRoom(roomName, options, TypedLobby.Default);
    }
}
