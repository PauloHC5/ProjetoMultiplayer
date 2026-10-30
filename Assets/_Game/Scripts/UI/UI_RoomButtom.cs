using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_RoomButtom : MonoBehaviour
{
    public const string RoomNamePrefix = "Room: ";
    public const string RoomPlayerCountPrefix = "Players: ";
    
    public Button Button { get; private set; }

    private TextMeshProUGUI _buttomText;

    private void Awake()
    {
        Button = GetComponent<Button>();
        _buttomText = GetComponentInChildren<TextMeshProUGUI>();
    }

    private void Update()
    {
        _buttomText.color = Button.interactable ? Color.green : Color.gray;
    }

    public void UpdateRoomState(string roomName, int playerCount, int maxPlayers)
    {
        _buttomText.text = RoomNamePrefix + roomName + " \n" + RoomPlayerCountPrefix + playerCount + "/" + maxPlayers;
    }

    public void JoinRoom()
    {
        string roomName = gameObject.name;
        MainMenuManager.JoinRoom(roomName);
        
        Button.interactable = false;
    }
}
