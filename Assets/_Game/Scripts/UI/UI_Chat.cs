using System;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UI_Chat : MonoBehaviourPunCallbacks
{
    [Header("UI Elements")]
    public Button chatButton;
    public GameObject chatOverlay;
    public TextMeshProUGUI chatContent;
    public TMP_InputField chatInputField;
    public TextMeshProUGUI roomNameText;
    public TextMeshProUGUI playersCountText;
    
    public PhotonView PhotonView { get; private set; }

    private void Awake()
    {
        PhotonView = GetComponent<PhotonView>();
    }

    private void Start()
    {
        if (!PhotonNetwork.InRoom) return;
        
        InitChatUI(PhotonNetwork.CurrentRoom.Name, PhotonNetwork.CurrentRoom.PlayerCount, PhotonNetwork.CurrentRoom.MaxPlayers);
    }

    private void Update()
    {
        if(Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
        {
            if (chatInputField.isFocused)
            {
                SendChatMessage();
            }
        }
    }
    
    public void OnChatButtonClicked()
    {
        chatOverlay.SetActive(!chatOverlay.activeSelf);
    }

    public override void OnJoinedRoom()
    {
        InitChatUI(PhotonNetwork.CurrentRoom.Name, PhotonNetwork.CurrentRoom.PlayerCount, PhotonNetwork.CurrentRoom.MaxPlayers);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        OnPlayerEnteredRoom(newPlayer.NickName, PhotonNetwork.CurrentRoom.PlayerCount, PhotonNetwork.CurrentRoom.MaxPlayers);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        OnPlayerLeftRoom(otherPlayer.NickName, PhotonNetwork.CurrentRoom.PlayerCount, PhotonNetwork.CurrentRoom.MaxPlayers);
    }

    private void InitChatUI(string roomName, int playerCount, int maxPlayers)
    {
        roomNameText.text = "Room: " + roomName;
        playersCountText.text = "Players: " + playerCount + "/" + maxPlayers;
    }

    private void OnPlayerEnteredRoom(string playerName, int playerCount, int maxPlayers)
    {
        playersCountText.text = "Players: " + playerCount + "/" + maxPlayers;
        
        chatContent.text += "\n \n" + playerName + " has joined the room.";
    }

    private void OnPlayerLeftRoom(string playerName, int playerCount, int maxPlayers)
    {
        playersCountText.text = "Players: " + playerCount + "/" + maxPlayers;
        
        chatContent.text += "\n \n" + playerName + " has left the room.";
    }
    
    private void SendChatMessage()
    {
        string message = chatInputField.text.Trim();
        if (!string.IsNullOrEmpty(message))
        {
            PhotonView.RPC(nameof(ReceiveChatMessage), RpcTarget.All, message);
            chatInputField.text = string.Empty;
        }
    }
    
    [PunRPC]
    public void ReceiveChatMessage(string msg, PhotonMessageInfo info)
    {
        string userName = info.Sender.NickName;
        
        string message = msg.Trim();
        
        if (string.IsNullOrEmpty(message) && string.IsNullOrWhiteSpace(userName)) return;
        
        chatContent.text += "\n \n" + userName + ": " + message;
    }
}
