using System;
using System.Collections;
using AYellowpaper.SerializedCollections;
using Photon.Pun;
using PixeLadder.EasyTransition;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : IPersistentSinglenton<GameManager>
{
    public string _currentScene;
    public string mainMenuSceneName;
    public string gameSceneName;
    public Action<string> OnSceneLoaded;

    public SerializedDictionary<string, AudioClip> sceneMusic;

    [SerializeField]
    private AudioManager _audioManager;
    public AudioManager AudioManager => _audioManager;

    public const int MaxNickNameLength = 18;
    public const int MaxRoomNameLength = 11;

    private bool _initialized;
    
    public static bool AlreadyShownTutorial = false;

    protected override void Awake()
    {
        base.Awake();

        if (string.IsNullOrEmpty(mainMenuSceneName))
            Debug.LogError("Main menu scene name is not set in GameManager.");
    }

    void OnEnable()  { SceneManager.sceneLoaded += HandleSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= HandleSceneLoaded; }

    void Start()
    {
        _audioManager?.InitializePool();

        _currentScene = SceneManager.GetActiveScene().name;
        PlayMusicForScene(_currentScene);

        _initialized = true;
    }

    // Ponto único de entrada para trocar de cena
    public void LoadScene(string name)
    {
        StartCoroutine(LoadSceneRoutine(name));
    }
    
    private IEnumerator LoadSceneRoutine(string name)
    {
        bool reachedMidPoint = false;

        SceneTransitioner.Instance.PlayTransition(() =>
        {
            reachedMidPoint = true;
        });

        yield return new WaitUntil(() => reachedMidPoint);
        
        if (PhotonNetwork.InRoom)
        {
            // Dentro da sala só o MasterClient decide a cena;
            // os demais seguem via AutomaticallySyncScene
            if (!PhotonNetwork.IsMasterClient)
            {
                Debug.LogWarning("Apenas o MasterClient pode trocar de cena dentro da sala.");
                yield break;
            }

            PhotonNetwork.LoadLevel(name);
        }
        else
        {
            // Offline / menu / lobby: carregamento local normal
            SceneManager.LoadSceneAsync(name, LoadSceneMode.Single);
        }
    }

    // Roda em TODOS os clients, não importa quem iniciou o load
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!_initialized) return; // ignora a cena inicial (o Start cuida dela)

        _currentScene = scene.name;
        PlayMusicForScene(scene.name);
        OnSceneLoaded?.Invoke(scene.name);
    }

    private void PlayMusicForScene(string sceneName)
    {
        if (sceneMusic.TryGetValue(sceneName, out AudioClip musicClip))
            AudioManager.PlayMusic(musicClip, bLoop: true);
        else
            Debug.LogWarning($"No music found for scene: {sceneName}");
    }

    public static void LoadMainMenu()
    {
        if (PhotonNetwork.InRoom)
        {
            // Sai da sala primeiro; o load do menu acontece em OnLeftRoom
            PhotonNetwork.LeaveRoom();
            return;
        }

        Instance.LoadScene(Instance.mainMenuSceneName);
    }

    public void RegisterOnLoadedScene(Action<string> callback)   { OnSceneLoaded += callback; }
    public void UnregisterOnLoadedScene(Action<string> callback) { OnSceneLoaded -= callback; }
}