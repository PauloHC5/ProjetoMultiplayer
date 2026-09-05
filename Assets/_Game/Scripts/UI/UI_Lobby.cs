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
    public RoomButtom roomButtonPrefab;
    public TMP_InputField roomNameInputField;
    public TextMeshProUGUI warningText;
    public Button createRoomButton;
    
    private string _customRoomName;
    
    private const int MaxRoomNameLength = 38;

    private void OnEnable()
    {
        roomNameInputField.onEndEdit.AddListener(OnInsertedRoomName);
    }

    private void OnDisable()
    {
        roomNameInputField.onEndEdit.RemoveAllListeners();
    }

    public void UpdateRoomList(List<RoomInfo> roomList)
    {
        RemoveUnexistingRoomButtons(roomList);
        AddNewRoomButtons(roomList);
        CheckFullRooms(roomList);
    }
    
    private void RemoveUnexistingRoomButtons(List<RoomInfo> roomList)
    {
        HashSet<string> existingRoomNames = new HashSet<string>();
        for (var index = 0; index < roomList.Count; index++)
        {
            if(roomList[index].RemovedFromList) continue;
            
            var room = roomList[index];
            existingRoomNames.Add(room.Name);
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

    private void AddNewRoomButtons(List<RoomInfo> roomList)
    {
        foreach (var room in roomList)
        {
            if(room.RemovedFromList) continue;
            
            Transform existingButton = scrollViewContent.Find(room.Name);
            
            if (existingButton != null) continue;
            
            RoomButtom newButton = Instantiate(roomButtonPrefab, scrollViewContent);
            newButton.name = room.Name;
            newButton.InitRoomButton(room.Name, room.PlayerCount, room.MaxPlayers);
        }
    }
    
    private void CheckFullRooms(List<RoomInfo> roomList)
    {
        foreach (var room in roomList)
        {
            if(room.RemovedFromList) continue;
            
            Transform existingButton = scrollViewContent.Find(room.Name);
            
            if (existingButton == null) continue;
            
            RoomButtom button = existingButton.GetComponent<RoomButtom>();
            if (button != null)
            {
                button.Button.interactable = room.PlayerCount != room.MaxPlayers;
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
