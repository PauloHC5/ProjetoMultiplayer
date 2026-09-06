using System;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class UI_Chat : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI chat;
    public TMP_InputField chatInputField;
    public TextMeshProUGUI roomNameText;
    public TextMeshProUGUI playersCountText;
    
    public PhotonView PhotonView { get; private set; }

    private void Awake()
    {
        PhotonView = GetComponent<PhotonView>();
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

    public void InitChatUI(string roomName, int playerCount, int maxPlayers)
    {
        roomNameText.text = "Room: " + roomName;
        playersCountText.text = "Players: " + playerCount + "/" + maxPlayers;
    }

    public void OnPlayerEnteredRoom(string playerName, int playerCount, int maxPlayers)
    {
        playersCountText.text = "Players: " + playerCount + "/" + maxPlayers;
        
        chat.text += "\n \n" + playerName + " has joined the room.";
    }

    public void OnPlayerLeftRoom(string playerName, int playerCount, int maxPlayers)
    {
        playersCountText.text = "Players: " + playerCount + "/" + maxPlayers;
        
        chat.text += "\n \n" + playerName + " has left the room.";
    }
    
    public void SendChatMessage()
    {
        string message = chatInputField.text.Trim();
        if (!string.IsNullOrEmpty(message))
        {
            PhotonView.RPC("ReceiveChatMessage", RpcTarget.All, message);
            chatInputField.text = string.Empty;
        }
    }
    
    [PunRPC]
    public void ReceiveChatMessage(string msg, PhotonMessageInfo info)
    {
        string userName = info.Sender.NickName;
        
        string message = msg.Trim();
        
        if (string.IsNullOrEmpty(message) && string.IsNullOrWhiteSpace(userName)) return;
        
        chat.text += "\n \n" + userName + ": " + message;
    }
}
