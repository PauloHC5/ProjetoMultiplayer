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
    private struct RoomChat
    {
        public GameObject chatObj;
        public TextMeshProUGUI chatTypeText;
        public TextMeshProUGUI chatContent;
        public TMP_InputField chatInputField;
        public TextMeshProUGUI roomNameText;
        public TextMeshProUGUI playersCountText;                        
        
    }

    private struct PlayerChat
    {
        public GameObject chatObj;
        public TextMeshProUGUI chatTypeText;
        public TextMeshProUGUI chatContent;
        public TMP_InputField chatInputField;
        public Player player;
    }

    private struct PlayerChatButton
    {
        public Button button;
        public TextMeshProUGUI playerNicknameText;
        public Image playerIcon;        
        public PlayerChat owningPlayerChat;
    }

    [Header("Room RoomChat UI Elements")]    
    [SerializeField] private RoomChat roomChat;
    [SerializeField] private Transform chatButtonsGroup;
    [SerializeField] private Transform playerChatOverlay;
    
    [Header("RoomChat Prefabs")]
    [SerializeField] private GameObject playerChatPrefab;
    [SerializeField] private Button playerChatButtonPrefab;
    
    
    public PhotonView PhotonView { get; private set; }

    private PlayerChatButton[] _playersChatButtons = new PlayerChatButton[3];

    private const string ChatContentTextName = "Chat Content Text";        
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
        if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
        {
            if (roomChat.chatInputField.isFocused)
            {
                SendChatMessage();
                return;
            }

            // check if any of the player chat input fields are focused
            for (int i = 0; i < _playersChatButtons.Length; i++)
            {
                if (_playersChatButtons[i].owningPlayerChat.chatInputField == null) continue;
                if (_playersChatButtons[i].owningPlayerChat.chatInputField.isFocused)
                {
                    string message = _playersChatButtons[i].owningPlayerChat.chatInputField.text.Trim();
                    SendPrivateChatMessage(message, _playersChatButtons[i].owningPlayerChat.player, _playersChatButtons[i].owningPlayerChat);                    
                    return;
                }
            }
            }
    }

    public void ToogleChatUI()
    {
        roomChat.chatObj.SetActive(!roomChat.chatObj.activeSelf);
        chatButtonsGroup.gameObject.SetActive(!chatButtonsGroup.gameObject.activeSelf);
        playerChatOverlay.gameObject.SetActive(!playerChatOverlay.gameObject.activeSelf);
    }
    
    private void OnPlayerChatButtonClicked(PlayerChatButton playerChatButton)
    {
        for (int i = 0; i < _playersChatButtons.Length; i++)
        {
            if (_playersChatButtons[i].button == null) continue;

            _playersChatButtons[i].owningPlayerChat.chatObj.SetActive(false);
            _playersChatButtons[i].button.interactable = true;
        }
        
        playerChatButton.owningPlayerChat.chatObj.SetActive(true);
        playerChatButton.button.interactable = false;
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

        var i = Array.FindIndex(_playersChatButtons, chat => chat.button == null);
        var playerChat = CreateChatForPlayer(newPlayer);
        _playersChatButtons[i] = CreateChatButtonForPlayer(newPlayer, playerChat);
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
            
            var playerChat = CreateChatForPlayer(player);
            _playersChatButtons[i] = CreateChatButtonForPlayer(player, playerChat);
        }
    }

    private PlayerChat CreateChatForPlayer(Player player)
    {
        PlayerChat playerChat = new PlayerChat
        {
            chatObj = Instantiate(playerChatPrefab, playerChatOverlay),
            chatInputField = null,
            player = player,
            chatTypeText = null
        };

        var textMeshesProos = playerChat.chatObj.GetComponentsInChildren<TextMeshProUGUI>();
        foreach (var textMeshPro in textMeshesProos)
        {
            switch (textMeshPro.name)
            {
                case ChatContentTextName:
                    playerChat.chatContent = textMeshPro;
                    break;                                                        
                case ChatTypeTextName:
                    playerChat.chatTypeText = textMeshPro;
                    playerChat.chatTypeText.text = "RoomChat with " + player.NickName;
                    break;
            }
        }

        playerChat.chatInputField = playerChat.chatObj.GetComponentInChildren<TMP_InputField>();

        playerChat.player = player;        
        
        playerChat.chatObj.SetActive(false);
        return playerChat;
    }
    
    private PlayerChatButton CreateChatButtonForPlayer(Player player, PlayerChat playerChat)
    {
        PlayerChatButton chatButton = new PlayerChatButton
        {
            button = Instantiate(playerChatButtonPrefab, chatButtonsGroup),
            playerNicknameText = null,
            playerIcon = null,
            owningPlayerChat = playerChat
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
            // Unity doesn't have Color.orange by default — use an RGB approximation
            playerIconColor = new Color(1f, 0.65f, 0f);
            break;
        default:
            playerIconColor = Color.white;
            break;
    }

    chatButton.playerIcon.color = playerIconColor;
    
    chatButton.button.onClick.AddListener(() => OnPlayerChatButtonClicked(chatButton));

    return chatButton;
    }
    
    private void RemoveChatForPlayer(Player player)
    {
        for (int i = 0; i < _playersChatButtons.Length; i++)
        {
            if (_playersChatButtons[i].owningPlayerChat.player != player) continue;
            
            Destroy(_playersChatButtons[i].owningPlayerChat.chatObj);
            
            _playersChatButtons[i].owningPlayerChat = default;           

            _playersChatButtons[i].button.onClick.RemoveAllListeners();
            Destroy(_playersChatButtons[i].button.gameObject);
            _playersChatButtons[i] = default;

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

    private void SendPrivateChatMessage(string msg, Player player, PlayerChat playerChat)
    {        
        if(player == null || string.IsNullOrEmpty(msg)) return;

        PhotonView.RPC(nameof(ReceivePrivateChatMessage), player, msg);
        playerChat.chatContent.text += "\n \n" + msg;
        playerChat.chatInputField.text = string.Empty;
    }
    
    [PunRPC]
    public void ReceiveChatMessage(string msg, PhotonMessageInfo info)
    {
        string userName = info.Sender.NickName;
        
        string message = msg.Trim();
        
        if (string.IsNullOrEmpty(message) && string.IsNullOrWhiteSpace(userName)) return;
        
        roomChat.chatContent.text += "\n \n" + userName + ": " + message;
    }

    [PunRPC]
    public void ReceivePrivateChatMessage(string msg, PhotonMessageInfo info)
    {
        string userName = info.Sender.NickName;

        string message = msg.Trim();

        if (string.IsNullOrEmpty(message) && string.IsNullOrWhiteSpace(userName)) return;
        // Find the PlayerChat for the sender
        PlayerChat? senderChat = null;
        foreach (var playerChatButton in _playersChatButtons)
        {
            if (playerChatButton.owningPlayerChat.player == info.Sender)
            {
                senderChat = playerChatButton.owningPlayerChat;
                break;
            }
        }
        if (senderChat.HasValue)
        {
            senderChat.Value.chatContent.text += "\n \n" + message;
        }
    }
}
