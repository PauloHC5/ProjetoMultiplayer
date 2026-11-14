using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UI_Login : MonoBehaviour
{
    [Header("Events")]
    public UnityEvent onRoomNameEmpty;
    public UnityEvent onRoomNameTooLong;
    public UnityEvent onRoomNameValid;
    public UnityEvent onLoginSuccess;
    
    private string _nickName;

    private void LateUpdate()
    {
        if(Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
        {
            Login();
        }
    }

    public void OnInsertedNickName(string nickName)
    {
        _nickName = nickName;

        CheckNickname();
    }

    public void Login()
    {
        if(!CheckNickname()) return;
        
        onLoginSuccess?.Invoke();
        MainMenuManager.Login(_nickName);
    }
    
    private bool CheckNickname()
    {
        if (string.IsNullOrEmpty(_nickName))
        {
            onRoomNameEmpty?.Invoke();
            return false;
        }
        
        if(_nickName.Length > GameManager.MaxNickNameLength)
        {
            onRoomNameTooLong?.Invoke();
            return false;
        }

        onRoomNameValid?.Invoke();
        return true;
    }
}
