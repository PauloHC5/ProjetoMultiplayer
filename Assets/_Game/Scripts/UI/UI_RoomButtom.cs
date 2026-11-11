using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_RoomButtom : MonoBehaviour
{
    [Header("UI Texts")]
    [SerializeField] private TextMeshProUGUI roomNameText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private TextMeshProUGUI maxPlayersText;

    [Header("UI Images")]
    [SerializeField] private Image joinImage;
    [SerializeField] private Sprite lockedSprite;
    [SerializeField] private Image roomCounterOverlayImage;
    [SerializeField] private Image playersIconImage;
    [SerializeField] private Image playersIconCounterImage;

    [Header("Colors")]
    public Color roomCounterOverlayColorWhenFull;
    public Color playersIconColorWhenFull;
    public Color playersIconCounterColorWhenFull;

    private Sprite _joinImageSprite;
    
    private Color _roomCounterOverlayColorWhenNotFull;
    private Color _playersIconColorWhenNotFull;
    private Color _playersIconCounterColorWhenNotFull;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        
        _joinImageSprite = joinImage.sprite;

        _roomCounterOverlayColorWhenNotFull = roomCounterOverlayImage.color;
        _playersIconColorWhenNotFull = playersIconImage.color;
        _playersIconCounterColorWhenNotFull = playersIconCounterImage.color;
    }
    
    public void SetButtonInteractable(bool isInteractable)
    {
        _button.interactable = isInteractable;
        
        if (isInteractable)
        {
            joinImage.sprite = _joinImageSprite;
            
            roomCounterOverlayImage.color = _roomCounterOverlayColorWhenNotFull;
            playersIconImage.color = _playersIconColorWhenNotFull;
            playersIconCounterImage.color = _playersIconCounterColorWhenNotFull;
            
            playerCountText.color = Color.black;
            maxPlayersText.color = Color.black;
        }
        else
        {
            joinImage.sprite = lockedSprite;

            roomCounterOverlayImage.color = roomCounterOverlayColorWhenFull;
            playersIconImage.color = playersIconColorWhenFull;
            playersIconCounterImage.color = playersIconCounterColorWhenFull;
            
            playerCountText.color = Color.white;
            maxPlayersText.color = Color.white;
        }
    }

    public void UpdateRoomState(string roomName, int playerCount, int maxPlayers)
    {
        roomNameText.text = roomName;
        playerCountText.text = playerCount.ToString();
        maxPlayersText.text = maxPlayers.ToString();
    }

    public void JoinRoom()
    {
        string roomName = gameObject.name;
        MainMenuManager.JoinRoom(roomName);
        
        SetButtonInteractable(false);
    }
}
