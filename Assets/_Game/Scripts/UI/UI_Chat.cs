using System;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UI_Chat : MonoBehaviourPunCallbacks
{
    [Serializable]
    private struct RoomChat
    {
        public GameObject chatObj;
        public TextMeshProUGUI chatContent;
        public TMP_InputField chatInputField;
        public TextMeshProUGUI roomNameText;
        public TextMeshProUGUI playersCountText;                        
        
    }

    [Header("Room Chat UI Elements")]
    [SerializeField] private RoomChat roomChat;

    private void Start()
    {
        if (!PhotonNetwork.InRoom) return;
        
        InitChatUI();
    }

    private void Update()
    {        
        if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
        {
            if (roomChat.chatInputField.isFocused)
            {
                SendChatMessage();
                return;
            }

        }
    }

    public void ToogleChatUI()
    {
        roomChat.chatObj.SetActive(!roomChat.chatObj.activeSelf);
    }

    public override void OnJoinedRoom()
    {
        InitChatUI();
    }
    
    private void RefreshRoomChat()
    {
        roomChat.roomNameText.text = "Room: " + PhotonNetwork.CurrentRoom.Name;
        roomChat.playersCountText.text = "Players: " + PhotonNetwork.CurrentRoom.PlayerCount + "/" + PhotonNetwork.CurrentRoom.MaxPlayers;
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshRoomChat();
        
        roomChat.chatContent.text += "\n \n" + newPlayer.NickName + " has joined the room.";
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshRoomChat();
        
        roomChat.chatContent.text += "\n \n" + otherPlayer.NickName + " has left the room.";
    }

    private void InitChatUI()
    {
        RefreshRoomChat();
    }
    
    private void SendChatMessage()
    {
        string message = roomChat.chatInputField.text.Trim();
        if (!string.IsNullOrEmpty(message))
        {
            photonView.RPC(nameof(ReceiveChatMessage), RpcTarget.All, message);
            roomChat.chatInputField.text = string.Empty;
        }
    }

    [PunRPC]
    public void ReceiveChatMessage(string msg, PhotonMessageInfo info)
    {
        string userName = info.Sender.NickName;
        
        string message = msg.Trim();
        
        if (string.IsNullOrEmpty(message) && string.IsNullOrWhiteSpace(userName)) return;
        
        roomChat.chatContent.text += "\n \n" + userName + ": " + message;
    }

}
