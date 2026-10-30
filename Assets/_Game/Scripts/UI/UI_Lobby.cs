using System;
using System.Collections.Generic;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_Lobby : MonoBehaviour
{
    [Header("UI Elements")]
    public Transform scrollViewContent;
    public UI_RoomButtom roomButtonPrefab;
    public TMP_InputField roomNameInputField;
    public TextMeshProUGUI warningText;
    public Button createRoomButton;
    
    private string _customRoomName;
    
    public const int MaxRoomNameLength = 38;

    private void OnEnable()
    {
        roomNameInputField.onEndEdit.AddListener(OnInsertedRoomName);
    }

    private void OnDisable()
    {
        roomNameInputField.onEndEdit.RemoveAllListeners();
    }

    public void UpdateRoomList(Dictionary<string, RoomInfo> _availableRooms)
    {
        RemoveUnexistingRoomButtons(_availableRooms);
        AddNewRoomButtons(_availableRooms);
        UpdateRoomsStates(_availableRooms);
    }
    
    private void RemoveUnexistingRoomButtons(Dictionary<string, RoomInfo> availableRooms)
    {
        HashSet<string> existingRoomNames = new HashSet<string>();
        foreach (var room in availableRooms)
        {
            if (room.Value.RemovedFromList) continue;
            existingRoomNames.Add(room.Key);
        }

        for (int i = scrollViewContent.childCount - 1; i >= 0; i--)
        {
            Transform child = scrollViewContent.GetChild(i);
            if (!existingRoomNames.Contains(child.name))
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void AddNewRoomButtons(Dictionary<string, RoomInfo> availableRooms)
    {
        foreach (var room in availableRooms)
        {
            if(room.Value.RemovedFromList) continue;
            
            Transform existingButton = scrollViewContent.Find(room.Key);
            
            if (existingButton != null) continue;
            
            UI_RoomButtom newButton = Instantiate(roomButtonPrefab, scrollViewContent);
            newButton.name = room.Key;
        }
    }
    
    private void UpdateRoomsStates(Dictionary<string, RoomInfo> availableRooms)
    {
        foreach (var room in availableRooms)
        {
            if(room.Value.RemovedFromList) continue;
            
            Transform existingButton = scrollViewContent.Find(room.Key);
            
            if (existingButton == null) continue;
            
            UI_RoomButtom button = existingButton.GetComponent<UI_RoomButtom>();
            if (button != null)
            {
                var interactable = room.Value.PlayerCount < room.Value.MaxPlayers && room.Value.IsOpen && room.Value.IsVisible;
                
                button.Button.interactable = interactable;
                button.UpdateRoomState(room.Key, room.Value.PlayerCount, room.Value.MaxPlayers);
            }
        }
    }
    
    private void OnInsertedRoomName(string roomName)
    {
        if(string.IsNullOrWhiteSpace(roomName))
        {
            warningText.text = "Room name cannot be empty.";
            warningText.gameObject.SetActive(true);
            createRoomButton.gameObject.SetActive(false);
            return;
        }
        
        if (roomName.Length > MaxRoomNameLength)
        {
            warningText.text = $"Room name cannot exceed {MaxRoomNameLength} characters.";
            warningText.gameObject.SetActive(true);
            createRoomButton.gameObject.SetActive(false);
            roomNameInputField.text = string.Empty;
            return;
        }
        
        _customRoomName = roomName;
        warningText.gameObject.SetActive(false);
        createRoomButton.gameObject.SetActive(true);
    }

    public void CreateRoom()
    {
        if(string.IsNullOrWhiteSpace(_customRoomName)) return;

       MainMenuManager.CreateRoom(_customRoomName);
    }
}
