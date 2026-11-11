using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_Login : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_InputField nickNameInputField;
    public Button loginButton;
    public TextMeshProUGUI warningText;
    public GameObject warningPanel;

    [Header("Debug")]
    public bool generateRandomNickName = true;
    public string randomNickNamePrefix = "Player_";
    
    private string _nickName;

    private void Start()
    {
        if (generateRandomNickName)
        {
            string randomNickName = randomNickNamePrefix + UnityEngine.Random.Range(1000, 9999);
            nickNameInputField.text = randomNickName;
            _nickName = randomNickName;
            loginButton.gameObject.SetActive(true);
            warningPanel.SetActive(false);
            warningText.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        nickNameInputField.onEndEdit.AddListener(OnInsertedNickName);
    }

    private void OnDisable()
    {
        nickNameInputField.onEndEdit.RemoveListener(OnInsertedNickName);
    }

    private void OnInsertedNickName(string nickName)
    {
        _nickName = nickName;
        
        if (string.IsNullOrEmpty(_nickName))
        {
            warningPanel.SetActive(true);
            warningText.gameObject.SetActive(true);
            warningText.text = "Nickname cannot be empty.";
            loginButton.gameObject.SetActive(false);
            return;
        }
        
        if(_nickName.Length > MainMenuManager.MaxNickNameLength)
        {
            warningPanel.SetActive(true);
            warningText.gameObject.SetActive(true);
            warningText.text = $"Nickname cannot exceed {MainMenuManager.MaxNickNameLength} characters.";
            loginButton.gameObject.SetActive(false);
            nickNameInputField.text = string.Empty;
            return;
        }

        loginButton.gameObject.SetActive(true);
        warningPanel.SetActive(false);
        warningText.gameObject.SetActive(false);
    }

    public void Login()
    {
        MainMenuManager.Login(_nickName);
    }
}
