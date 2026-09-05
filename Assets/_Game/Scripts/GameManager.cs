using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : IPersistentSinglenton<GameManager>
{
    public string _currentScene;
    public Action<string> OnSceneLoaded;

    [SerializeField]
    private AudioManager _audioManager;

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

    public void RegisterOnLoadedScene(Action<String> callback)
    {
        OnSceneLoaded += callback;
    }

    public void UnregisterOnLoadedScene(Action<String> callback)
    {
        OnSceneLoaded -= callback;
    }
}
