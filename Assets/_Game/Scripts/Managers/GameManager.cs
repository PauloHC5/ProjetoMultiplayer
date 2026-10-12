using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : IPersistentSinglenton<GameManager>
{
    public string _currentScene;
    public string mainMenuSceneName;
    public Action<string> OnSceneLoaded;

    [SerializeField]
    private AudioManager _audioManager;

    public AudioManager AudioManager => _audioManager;

    protected override void Awake()
    {
        base.Awake();
        
        if(string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.LogError("Main menu scene name is not set in GameManager.");
        }
    }

    void Start()
    {
        _audioManager?.InitializePool();
    }

    void Update()
    {
        
    }

    public void LoadScene(string name)
    {
        AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(name, LoadSceneMode.Single);

        if (asyncOperation == null) return;

        asyncOperation.completed += (AsyncOperation obj) =>
        {
            _currentScene = name;
            OnSceneLoaded?.Invoke(name);
        };
    }
    
    public static void LoadMainMenu()
    {
        Instance.LoadScene(Instance.mainMenuSceneName);
    }

    public void RegisterOnLoadedScene(Action<String> callback)
    {
        OnSceneLoaded += callback;
    }

    public void UnregisterOnLoadedScene(Action<String> callback)
    {
        OnSceneLoaded -= callback;
    }
}
