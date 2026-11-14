using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UI_Lobby : MonoBehaviour, ILobbyCallbacks
{
    [Header("UI Elements")]
    public Transform scrollViewContent;
    public UI_RoomButtom roomButtonPrefab;
    
    [Header("Events")]
    public UnityEvent onRoomNameEmpty;
    public UnityEvent onRoomNameTooLong;
    public UnityEvent onRoomNameValid;
    public UnityEvent<bool> onRoomListUpdate;
    
    private string _customRoomName;

    private void LateUpdate()
    {
        if(Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
        {
            CreateRoom();
        }
    }

    public void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        Dictionary<string, RoomInfo> availableRooms = roomList.ToDictionary(room => room.Name, room => room);
        
        UpdateRoomList(availableRooms);
    }

    private void UpdateRoomList(Dictionary<string, RoomInfo> availableRooms)
    {
        RemoveUnexistingRoomButtons(availableRooms);
        AddNewRoomButtons(availableRooms);
        UpdateRoomsStates(availableRooms);
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
                
                button.SetButtonInteractable(interactable);
                button.UpdateRoomState(room.Key, room.Value.PlayerCount, room.Value.MaxPlayers);
            }
        }
        
        onRoomListUpdate.Invoke(availableRooms.Values.All(room => room.RemovedFromList || !room.IsVisible));
    }
    
    public void OnInsertedRoomName(string roomName)
    {
        _customRoomName = roomName;

        CheckRoomName(roomName);
    }

    private bool CheckRoomName(string roomName)
    {
        if(string.IsNullOrWhiteSpace(roomName))
        {
            onRoomNameEmpty?.Invoke();
            return false;
        }
        
        if (roomName.Length > GameManager.MaxRoomNameLength)
        {
            onRoomNameTooLong?.Invoke();
            return false;
        }
        
        onRoomNameValid?.Invoke();
        return true;
    }

    public void CreateRoom()
    {
        if(!CheckRoomName(_customRoomName)) return;

       MainMenuManager.CreateRoom(_customRoomName);
    }
    
    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    public void OnJoinedLobby() {}

    public void OnLeftLobby() {}

    public void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics) {}
}
