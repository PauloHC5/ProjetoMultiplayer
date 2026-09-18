using System;
using _Game.Scripts;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UI_Chat : MonoBehaviourPunCallbacks
{
    [Serializable]
    private struct Chat
    {
        public GameObject chatObj;
        public TextMeshProUGUI chatTypeText;
        public TextMeshProUGUI chatContent;
        public TMP_InputField chatInputField;
        public TextMeshProUGUI roomNameText;
        public TextMeshProUGUI playersCountText;
        public ChatButton chatButton;
        
        [HideInInspector]
        public int owningPlayerId;
        
    }
    [Serializable]
    private struct ChatButton
    {
        public Button button;
        public TextMeshProUGUI playerNicknameText;
        public Image playerIcon;
    }
    
    [Header("Room Chat UI Elements")]
    [SerializeField] private Chat roomChat;
    [SerializeField] private Transform chatButtonsGroup;
    
    [Header("Chat Prefabs")]
    [SerializeField] private GameObject playerChatPrefab;
    [SerializeField] private Button playerChatButtonPrefab;
    
    
    public PhotonView PhotonView { get; private set; }
    
    private Chat[] _playersChats = new Chat[3];

    private const string ChatContentTextName = "Chat Content Text";
    private const string RoomNameTextName = "Room Name Text";
    private const string PlayersCountTextName = "Players Count Text";
    private const string PlayerNicknameTextName = "Player Nickname Text";
    private const string PlayerIconName = "Player Icon";
    private const string ChatTypeTextName = "Chat Type Text";

    private void Awake()
    {
        PhotonView = GetComponent<PhotonView>();
    }

    private void Start()
    {
        if (!PhotonNetwork.InRoom) return;
        
        InitChatUI();
    }

    private void Update()
    {
        if(Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
        {
            if (roomChat.chatInputField.isFocused)
            {
                SendChatMessage();
            }
        }
    }
    
    public void OnRoomChatButtonClicked()
    {
        roomChat.chatObj.SetActive(true);

        for (int i = 0; i < _playersChats.Length; i++)
        {
            if(_playersChats[i].chatObj != null)
                _playersChats[i].chatObj.SetActive(false);
        }
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
        
        _playersChats[PhotonNetwork.PlayerList.Length - 1] = CreateChatForPlayer(newPlayer);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshRoomChat();
        
        roomChat.chatContent.text += "\n \n" + otherPlayer.NickName + " has left the room.";
        
        RemoveChatForPlayer(otherPlayer);
    }

    private void InitChatUI()
    {
        RefreshRoomChat();

        // Create chat UI for each player in the room, excluding the local player
        for(int i = 0; i < PhotonNetwork.PlayerList.Length; i++)
        {
            Player player = PhotonNetwork.PlayerList[i];
            if (player == null || PhotonNetwork.LocalPlayer.ActorNumber == player.ActorNumber) continue;
            
            _playersChats[i] = CreateChatForPlayer(player);
        }
    }

    private Chat CreateChatForPlayer(Player player)
    {
        Chat playerChat = new Chat
        {
            chatObj = Instantiate(playerChatPrefab, transform),
            chatContent = null,
            chatInputField = null,
            roomNameText = null,
            playersCountText = null,
            owningPlayerId = 0,
        };

        var textMeshesProos = playerChat.chatObj.GetComponentsInChildren<TextMeshProUGUI>();
        foreach (var textMeshPro in textMeshesProos)
        {
            switch (textMeshPro.name)
            {
                case ChatContentTextName:
                    playerChat.chatContent = textMeshPro;
                    break;
                case RoomNameTextName:
                    playerChat.roomNameText = textMeshPro;
                    playerChat.roomNameText.text = "Room: " + PhotonNetwork.CurrentRoom.Name;
                    break;
                case PlayersCountTextName:
                    playerChat.playersCountText = textMeshPro;
                    playerChat.playersCountText.text = "Players: " + PhotonNetwork.CurrentRoom.PlayerCount + "/" + PhotonNetwork.CurrentRoom.MaxPlayers;
                    break;
                case ChatTypeTextName:
                    playerChat.chatTypeText = textMeshPro;
                    playerChat.chatTypeText.text = "Chat with " + player.NickName;
                    break;
            }
        }
        
        playerChat.owningPlayerId = player.ActorNumber;
        playerChat.chatButton = CreateChatButtonForPlayer(player);
        
        playerChat.chatObj.SetActive(false);
        return playerChat;
    }
    
    private ChatButton CreateChatButtonForPlayer(Player player)
    {
        ChatButton chatButton = new ChatButton
        {
            button = Instantiate(playerChatButtonPrefab, chatButtonsGroup),
            playerNicknameText = null,
            playerIcon = null
        };

        chatButton.playerNicknameText = chatButton.button.transform.Find(PlayerNicknameTextName).GetComponent<TextMeshProUGUI>();
        chatButton.playerIcon = chatButton.button.transform.Find(PlayerIconName).GetComponent<Image>();

        chatButton.playerNicknameText.text = player.NickName;
        
        Color playerIconColor;
        PlayerType playerType = GameMode.GetPlayerType(player);

        switch (playerType)
        {
            case PlayerType.Player1:
                playerIconColor = Color.green;
                break;
            case PlayerType.Player2:
                playerIconColor = Color.blue;
                break;
            case PlayerType.Player3:
                playerIconColor = Color.orange;
                break;
            default:
                playerIconColor = Color.white;
                break;
        }
        
        chatButton.playerIcon.color = playerIconColor;
        chatButton.button.onClick.AddListener(() =>
        {
            for (int i = 0; i < _playersChats.Length; i++)
            {
                if (_playersChats[i].owningPlayerId == player.ActorNumber)
                    _playersChats[i].chatObj.SetActive(!_playersChats[i].chatObj.activeSelf);
                else
                {
                    _playersChats[i].chatObj?.SetActive(false);
                }
            }
        });

        return chatButton;
    }
    
    private void RemoveChatForPlayer(Player player)
    {
        for (int i = 0; i < _playersChats.Length; i++)
        {
            if (_playersChats[i].owningPlayerId != player.ActorNumber) continue;
            
            Destroy(_playersChats[i].chatButton.button.gameObject);
            Destroy(_playersChats[i].chatObj);
            _playersChats[i] = default;
            break;
        }
    }
    
    private void SendChatMessage()
    {
        string message = roomChat.chatInputField.text.Trim();
        if (!string.IsNullOrEmpty(message))
        {
            PhotonView.RPC(nameof(ReceiveChatMessage), RpcTarget.All, message);
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
