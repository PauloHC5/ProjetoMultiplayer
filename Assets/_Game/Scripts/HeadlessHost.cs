using System;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public class HeadlessHost : MonoBehaviourPunCallbacks
{
    public string roomName;
    public UI_Chat chatUI;
    
    public bool bDebugMode = false;
    
    void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        
        if(!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.NickName = "TestPlayer_" + Random.Range(1000, 9999);
            if(bDebugMode)
                Debug.Log("Connecting to Photon server as " + PhotonNetwork.NickName);
            PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            if(bDebugMode)
                Debug.Log("Already connected to Photon server as " + PhotonNetwork.NickName);
        }
    }

    private void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            chatUI.InitChatUI(PhotonNetwork.CurrentRoom.Name, PhotonNetwork.CurrentRoom.PlayerCount, PhotonNetwork.CurrentRoom.MaxPlayers);
        }
            
    }

    public override void OnConnectedToMaster()
    {
        if (this.roomName != string.Empty)
        {
            if(bDebugMode)
                Debug.Log("Tentando entrar na sala: " + this.roomName);
        
            PhotonNetwork.JoinRoom(this.roomName);
            // NÃO faça nada com o valor de retorno, nem dê return aqui
            // o resultado real vem via OnJoinedRoom ou OnJoinRoomFailed
        }
        else
        {
            CreateRoom();
        }
    }

    private void CreateRoom()
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
        if(bDebugMode)
            Debug.Log("Host holding the room: " + PhotonNetwork.CurrentRoom.Name);
        
        if (PhotonNetwork.IsMasterClient && SceneManager.GetActiveScene().name != PhotonNetwork.CurrentRoom.CustomProperties["sceneName"].ToString())
        {
            PhotonNetwork.LoadLevel(SceneManager.GetActiveScene().name);
        }
        
        chatUI?.InitChatUI(PhotonNetwork.CurrentRoom.Name, PhotonNetwork.CurrentRoom.PlayerCount, PhotonNetwork.CurrentRoom.MaxPlayers);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        if(bDebugMode)
            Debug.LogWarning($"Falha ao entrar na sala '{this.roomName}': {message}. Criando uma nova.");
    
        CreateRoom();
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
        chatUI?.OnPlayerEnteredRoom(newPlayer.NickName, currentPlayers, maxPlayers);
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        int currentPlayers = PhotonNetwork.CurrentRoom.PlayerCount;
        int maxPlayers = PhotonNetwork.CurrentRoom.MaxPlayers;
        chatUI?.OnPlayerLeftRoom(otherPlayer.NickName, currentPlayers, maxPlayers);
    }
}