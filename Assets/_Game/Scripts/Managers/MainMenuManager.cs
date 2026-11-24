using System;
using System.Collections.Generic;
using _Game.Scripts;
using Photon.Pun;
using Photon.Realtime;
using PixeLadder.EasyTransition;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MainMenuManager : SceneSingleton<MainMenuManager>
{
    private enum MenuState
    {
        PressAnyKey,
        Login,
        Lobby,
        JoiningRoom
    }

    [Header("Menu States Events")] 
    public UnityEvent onPressAnyKeyState;
    public UnityEvent onLoginState;
    public UnityEvent onLobbyState;
    public UnityEvent onJoiningRoomState;
    
    [Header("Music")]
    public AudioClip mainMenuMusic;
    

    private MenuState _currentMenuState = MenuState.PressAnyKey;
    
    private Dictionary<string, RoomInfo> _availableRooms = new Dictionary<string, RoomInfo>();

    protected override void Awake()
    {
        base.Awake();

        PhotonNetwork.AutomaticallySyncScene = true;
    }

    private void Start()
    {
        if(PhotonNetwork.IsConnected && PhotonNetwork.NickName != null)
        {
            if(PhotonNetwork.InRoom)
                PhotonNetwork.LeaveRoom();
            
            PhotonNetwork.JoinLobby();
        }
        else
        {
            ChangeMenuState(MenuState.PressAnyKey);
        }
    }

    private void Update()
    {
        if(_currentMenuState == MenuState.PressAnyKey && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            ChangeMenuState(MenuState.Login);
        }
    }

    private void ChangeMenuState(MenuState newState)
    {
        switch (newState)
        {
            case MenuState.PressAnyKey:
                onPressAnyKeyState?.Invoke();
                break;
            case MenuState.Login:
                onLoginState?.Invoke();
                break;
            case MenuState.Lobby:
                onLobbyState?.Invoke();
                break;
            case MenuState.JoiningRoom:
                onJoiningRoomState?.Invoke();
                break;
        }
        
        _currentMenuState = newState;
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
        
        Instance.ChangeMenuState(MenuState.JoiningRoom);
        
        PhotonNetwork.JoinRoom(roomName);
        
        //SceneTransitioner.Instance.PlayTransition(() => DoJoinRoom(roomName));
    }

    private static void DoJoinRoom(string roomName)
    {
        
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        ChangeMenuState(MenuState.Lobby);
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
            GameManager.Instance.LoadScene(GameManager.Instance.gameSceneName);
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
    }

    public static void CreateRoom(string roomName)
    {
        if(string.IsNullOrEmpty(roomName))
        {
            Debug.LogError("Room name cannot be empty to create a room.");
            return;
        }
        
        if(PhotonNetwork.NetworkClientState != ClientState.JoinedLobby)
        {
            Debug.LogWarning("Cannot create room right now, client state: " + PhotonNetwork.NetworkClientState);
            return;
        }

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = 3,
            IsVisible = true,
            IsOpen = true,
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable
            {
                { "sceneName",  GameManager.Instance.gameSceneName }
            }
        };
        
        Instance.ChangeMenuState(MenuState.JoiningRoom);

        PhotonNetwork.CreateRoom(roomName, options, TypedLobby.Default);
    }
}
