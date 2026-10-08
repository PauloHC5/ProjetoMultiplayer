using System;
using System.Collections;
using System.Collections.Generic;
using _Game.Scripts;
using AYellowpaper.SerializedCollections;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;


public class UI_Game : MonoBehaviour
{
    private struct PlayerUIStatus
    {
        public GameObject PlayerUIStatusObject;
        public Image PlayerIcon;
        public TextMeshProUGUI PlayerNicknameText;
        public TextMeshProUGUI ScoreCounterText;
        public TextMeshProUGUI LivesCounterText;
    }
    
    private struct WaveProgressBar
    {
        public Slider WaveSlider;
        public TextMeshProUGUI WaveProgressText;
    }
    
    private struct MatchPreparationUI
    {
        public Button StartGameButton;
        public TextMeshProUGUI AwaitingHostText;
    }
    
    [Header("UI Elements")]
    public TextMeshProUGUI gameCountdownText;
    
    [Header("UI Screens")]
    public GameObject gameOverUIScreen;
    public GameObject matchPreparationUIScreen;
    
    [Header("Wave UI Elements")]
    public TextMeshProUGUI waveCounterText;
    public GameObject waveProgressBarObj;
    
    [Header("Player Status UI Elements")]
    public GameObject statusBarUI;
    public Transform pickupStatusUI;
    
    [Header("UI Prefabs")]
    public GameObject playerStatusPrefab;

    [SerializedDictionary("Pickup Type", "Sprite")]
    public AYellowpaper.SerializedCollections.SerializedDictionary<Pickup.PickupType, Sprite> pickupTypeSprites;

    private PlayerUIStatus _localPlayerStatus;
    private bool _bGameIsRunning = false;
    private WaveProgressBar _waveProgressBar;
    private MatchPreparationUI _matchPreparationUI;

    // Constants for finding child components in the player status prefab
    private const string PlayerNicknameTextComponentName = "Player Nickname Text (TMP)";
    private const string ScoreCounterTextComponentName = "Score Counter Text (TMP)";
    private const string LivesCounterTextComponentName = "Lives Counter Text (TMP)";

    private void Awake()
    {
        gameCountdownText.enabled = false;
        waveCounterText.gameObject.SetActive(false);
        waveProgressBarObj.SetActive(false);
        
        _localPlayerStatus = new PlayerUIStatus
        {
            PlayerUIStatusObject = null,
            PlayerIcon = null,
            PlayerNicknameText = null,
            ScoreCounterText = null,
            LivesCounterText = null
        };        
        
        
        _waveProgressBar = new WaveProgressBar
        {
            WaveSlider = waveProgressBarObj.GetComponentInChildren<Slider>(),
            WaveProgressText = waveProgressBarObj.GetComponentInChildren<TextMeshProUGUI>()
        };
        
        for(int i = 0; i < matchPreparationUIScreen.transform.childCount; i++)
        {
            Debug.LogWarning(matchPreparationUIScreen.transform.GetChild(i).gameObject.name);
        }
        
        _matchPreparationUI = new MatchPreparationUI
        {
            StartGameButton = matchPreparationUIScreen.transform.GetChild(0).GetComponent<Button>(),
            AwaitingHostText = matchPreparationUIScreen.transform.GetChild(1).GetComponent<TextMeshProUGUI>()
        };
    }

    private void Start()
    {
        _bGameIsRunning = true;
        
        if(PhotonNetwork.IsMasterClient)
        {
            _matchPreparationUI.StartGameButton.gameObject.SetActive(true);
            _matchPreparationUI.AwaitingHostText.gameObject.SetActive(false);
        }
        else
        {
            _matchPreparationUI.StartGameButton.gameObject.SetActive(false);
            _matchPreparationUI.AwaitingHostText.gameObject.SetActive(true);
        }
    }

    private void OnGameCountdownChanged(int countdown)
    {
        gameCountdownText.enabled = countdown >= 0;
        gameCountdownText.text = countdown.ToString();
        
        if(countdown == 0)
        {
            gameCountdownText.text = "GO!";
        }
    }

    private void UpdateScore()
    {
        if(PhotonNetwork.LocalPlayer == null || _localPlayerStatus.ScoreCounterText == null) return;
        
        if(PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(GameMode.PlayerScoreKey, out var index))
        {
            int currentScore = (int)index;
            _localPlayerStatus.ScoreCounterText.text = currentScore.ToString();
        }
    }
    
    private void OnLivesChanged(int lives)
    {
        if(lives <= 0)
        {            
            _localPlayerStatus.LivesCounterText.text = "0";
            return;
        }
        _localPlayerStatus.LivesCounterText.text = lives.ToString();
    }
    
    private void OnPlayerCharacterSpawned(Player player, PlayerCharacter playerCharacter)
    {
        if(player == null || playerCharacter == null) return;
        
        if(player.IsLocal)
        {
            SpawnPlayerStatusUI(player, playerCharacter);
            playerCharacter.OnPickupCollected += OnPickupCollected;
            playerCharacter.OnPlayerDeath += OnPlayerCharacterDestroyed;
        }
    }

    private void SpawnPlayerStatusUI(Player player, PlayerCharacter playerCharacter)
    {
        if (_localPlayerStatus.PlayerUIStatusObject != null) return;

        GameObject playerStatusGO = Instantiate(playerStatusPrefab, Vector3.zero, Quaternion.identity);
        
        if(playerStatusGO == null)
        {
            Debug.LogError("Failed to instantiate player status UI for player: " + player.NickName);
            return;
        }

        playerStatusGO.transform.SetParent(statusBarUI.transform, false);
        
        var playerIcon = playerStatusGO.GetComponentInChildren<Image>();
        var nicknameText = playerStatusGO.transform.Find(PlayerNicknameTextComponentName).GetComponent<TextMeshProUGUI>();
        var scoreCounterText = playerStatusGO.transform.Find(ScoreCounterTextComponentName).GetComponent<TextMeshProUGUI>();
        var livesCounterText = playerStatusGO.transform.Find(LivesCounterTextComponentName).GetComponent<TextMeshProUGUI>();
        
        PlayerUIStatus playerStatus = new PlayerUIStatus
        {
            PlayerUIStatusObject = playerStatusGO,
            PlayerIcon = playerIcon,
            PlayerNicknameText = nicknameText,
            ScoreCounterText = scoreCounterText,
            LivesCounterText = livesCounterText
        };

        Color playerColor = GameMode.GetPlayerColorBasedOnPlayerType(player);

        playerIcon.color = playerColor;
        playerStatus.PlayerNicknameText.text = player.NickName;
        playerStatus.PlayerNicknameText.color = playerColor;
        playerStatus.ScoreCounterText.text = "0";
        playerStatus.ScoreCounterText.color = playerColor;
        playerStatus.LivesCounterText.text = GameMode.Instance.StartingLives.ToString();
        playerStatus.LivesCounterText.color = playerColor;

        _localPlayerStatus = playerStatus;
    }
    
    private void OnPlayerCharacterDestroyed(PlayerCharacter playerCharacter)
    {
        if(!_bGameIsRunning) return;
        
        StopAllCoroutines();
        for(int i = pickupStatusUI.childCount - 1; i >= 0; i--)
        {
            Destroy(pickupStatusUI.GetChild(i).gameObject);
        }
        
        playerCharacter.OnPlayerDeath -= OnPlayerCharacterDestroyed;
        playerCharacter.OnPickupCollected -= OnPickupCollected;
    }
    
    private void OnPickupCollected(Pickup.PickupType pickupType, float duration)
    {
        if(pickupType is Pickup.PickupType._1UP or Pickup.PickupType.Toolbox) return;

        if(!pickupTypeSprites.ContainsKey(pickupType))
        {
            Debug.LogWarning($"Pickup type {pickupType} does not have a corresponding sprite in the dictionary.");
            return;
        }

        // Instantiate image for the pickup type
        var pickupImageGO = new GameObject(pickupType.ToString());
        pickupImageGO.transform.SetParent(pickupStatusUI, false);
        
        var pickupImage = pickupImageGO.AddComponent<Image>();
        pickupImage.GetComponent<RectTransform>().sizeDelta = new Vector2(35, 35);
        pickupImage.preserveAspect = true;
        pickupImage.sprite = pickupTypeSprites[pickupType];
        
        StartCoroutine(PickupIconRoutine(pickupImage, duration));
    }

    private void OnGameOver(int playerCharactersAlive)
    {
        gameCountdownText.enabled = false;
        gameOverUIScreen.SetActive(true);
    }
    
    public void ReturnToMainMenu()
    {
        PhotonNetwork.LeaveRoom();                
    }
    
    private void OnGameStarted(int waveNumber)
    {
        gameCountdownText.enabled = false;
        
        waveCounterText.gameObject.SetActive(true);
        waveProgressBarObj.SetActive(true);
        
        waveCounterText.text = $"Wave: {waveNumber}";
        
        UpdateProgressBar(GameMode.Instance.HordeProgressPercentage);
    }
    
    private IEnumerator PickupIconRoutine(Image pickupImage, float duration)
    {
        if(pickupImage == null) yield break;

        float countdown = duration;
        Color originalColor = pickupImage.color;
        float aThirdDuration = duration / 3f;

        while (countdown > 0)
        {
            if (countdown <= aThirdDuration)
            {
                float alpha = Mathf.PingPong(Time.time * 5, 1);
                pickupImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            }
            countdown -= Time.deltaTime;
            yield return null;
        }

        Destroy(pickupImage.gameObject);
    }
    
    private void UpdateProgressBar(float progress)
    {
        int wholeProgress = Mathf.FloorToInt(progress);

        _waveProgressBar.WaveSlider.value = wholeProgress;
        _waveProgressBar.WaveProgressText.text = $"{wholeProgress}%";
    }

    private void OnEnable()
    {
        GameMode.OnLivesChanged += OnLivesChanged;
        GameMode.OnGameCooldownChanged += OnGameCountdownChanged;
        GameMode.OnScoreChanged += UpdateScore;
        GameMode.OnPlayerCharacterSpawned += OnPlayerCharacterSpawned;
        GameMode.GameStartedEvent += OnGameStarted;
        GameMode.GameOverEvent += OnGameOver;
        GameMode.OnHordeProgressChanged += UpdateProgressBar;
    }

    private void OnDisable()
    {
        GameMode.OnScoreChanged -= UpdateScore;
        GameMode.OnGameCooldownChanged -= OnGameCountdownChanged;
        GameMode.OnLivesChanged -= OnLivesChanged;
        GameMode.OnPlayerCharacterSpawned -= OnPlayerCharacterSpawned;
        GameMode.GameStartedEvent -= OnGameStarted;
        GameMode.GameOverEvent -= OnGameOver;
        GameMode.OnHordeProgressChanged -= UpdateProgressBar;
    }

    private void OnApplicationQuit()
    {
        _bGameIsRunning = false;
    }
}
