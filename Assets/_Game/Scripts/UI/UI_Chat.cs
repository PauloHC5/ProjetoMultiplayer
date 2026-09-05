using System;
using Photon.Pun;
using TMPro;
using UnityEngine;

public class UI_Chat : MonoBehaviour
{
    [Header("UI Elements")]
    public Transform scrollViewContent;
    public TextMeshProUGUI chatMessagePrefab;
    public TMP_InputField chatInputField;
    public TextMeshProUGUI roomNameText;
    public TextMeshProUGUI playersCountText;
    
    private PhotonView _photonView;

    private void Awake()
    {
        _photonView = GetComponent<PhotonView>();
    }

    public void InitChatUI(string roomName, int playerCount, int maxPlayers)
    {
        roomNameText.text = "Room: " + roomName;
        playersCountText.text = "Players: " + playerCount + "/" + maxPlayers;
    }

    public void OnPlayerEnteredRoom(int playerCount, int maxPlayers)
    {
        playersCountText.text = "Players: " + playerCount + "/" + maxPlayers;
    }

    public void OnPlayerLeftRoom(int playerCount, int maxPlayers)
    {
        playersCountText.text = "Players: " + playerCount + "/" + maxPlayers;
    }
    
    public void SendChatMessage()
    {
        string message = chatInputField.text.Trim();
        if (!string.IsNullOrEmpty(message))
        {
            _photonView.RPC("ReceiveChatMessage", RpcTarget.AllBuffered, message);
            chatInputField.text = string.Empty;
        }
    }
    
    [PunRPC]
    public void ReceiveChatMessage(string msg, PhotonMessageInfo info)
    {
        string userName = info.Sender.NickName;
        
        string message = msg.Trim();
        if (!string.IsNullOrEmpty(message) || !string.IsNullOrWhiteSpace(userName))
        {
            TextMeshProUGUI newMessage = Instantiate(chatMessagePrefab, scrollViewContent);
            newMessage.text = userName + ": " + message;
            chatInputField.text = string.Empty;
        }
    }
}
