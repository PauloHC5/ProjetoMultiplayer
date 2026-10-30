using System.Collections;
using System.Collections.Generic;
using _Game.Scripts;
using AYellowpaper.SerializedCollections;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
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
    public TextMeshProUGUI outOfLivesText;
    public TextMeshProUGUI queenIncomingText;
    
    [Header("UI Screens")]
    public GameObject gameOverUIScreen;
    public GameObject matchPreparationUIScreen;
    
    [Header("Wave UI Elements")]
    public TextMeshProUGUI waveCounterText;
    public GameObject waveProgressBarObj;
    
    [Header("Player Status UI Elements")]
    public GameObject statusBarUI;
    public Transform pickupStatusUI;
    public SerializedDictionary<PlayerType, Sprite> playerIconSprites;
    
    [Header("Game Over UI Elements")]
    public TextMeshProUGUI gameOverReasonText;
    public TextMeshProUGUI wavesReachedText;
    public Transform scoreBoardContent;
    
    [Header("UI Prefabs")]
    public GameObject playerStatusPrefab;
    public GameObject playerScoreInfoPrefab;

    [Space(20)]
    
    [SerializedDictionary("Pickup Type", "Sprite")]
    public SerializedDictionary<Pickup.PickupType, Sprite> pickupTypeSprites;

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
        outOfLivesText.gameObject.SetActive(false);
        
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

        _matchPreparationUI = new MatchPreparationUI
        {
            StartGameButton = matchPreparationUIScreen.transform.GetChild(0).GetComponent<Button>(),
            AwaitingHostText = matchPreparationUIScreen.transform.GetChild(1).GetComponent<TextMeshProUGUI>()
        };

        _matchPreparationUI.StartGameButton.onClick.AddListener(StartWave);
    }

    private void Start()
    {
        _bGameIsRunning = true;                
    }

    private void OnMasterClientChanged(bool isMasterClient)
    {
        if (isMasterClient)
        {
            _matchPreparationUI.StartGameButton.gameObject.SetActive(true);
            _matchPreparationUI.AwaitingHostText.gameObject.SetActive(false);
            Debug.Log("You are the host. You can start the game.");
        }
        else
        {
            _matchPreparationUI.StartGameButton.gameObject.SetActive(false);
            _matchPreparationUI.AwaitingHostText.gameObject.SetActive(true);
            Debug.Log("You are not the host. Waiting for the host to start the game.");
        }
    }

    private void StartWave()
    {
        //GameMode.Start
        GameMode.Instance.StartWave();
    }

    private void OnGameCountdownChanged(int countdown)
    {
        matchPreparationUIScreen.SetActive(false);

        gameCountdownText.enabled = countdown >= 0;
        gameCountdownText.text = countdown.ToString();
        
        if(countdown == 0)
        {
            gameCountdownText.text = "Stop the Invasion!";
        }
    }

    private void OnWaveCompleted(int waveNumber)
    {
        gameCountdownText.enabled = true;
        gameCountdownText.text = $"Wave {waveNumber} Completed!";

        StartCoroutine(HideWaveCompletedMessageAfterDelay(3f));
    }

    private IEnumerator HideWaveCompletedMessageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        gameCountdownText.enabled = false;
        matchPreparationUIScreen.SetActive(true);
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
            outOfLivesText.gameObject.SetActive(true);
            return;
        }
        
        outOfLivesText.gameObject.SetActive(false);
        _localPlayerStatus.LivesCounterText.text = lives.ToString();
    }
    
    private void OnPlayerCharacterSpawned(Player player, PlayerCharacter playerCharacter)
    {
        if(player == null || playerCharacter == null) return;
        
        if(player.IsLocal)
        {
            SpawnPlayerStatusUI(player);
            playerCharacter.OnPickupCollected += OnPickupCollected;
            playerCharacter.OnPlayerDeath += OnPlayerCharacterDestroyed;
        }
    }

    private void SpawnPlayerStatusUI(Player player)
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

        playerIcon.sprite = playerIconSprites[GameMode.GetPlayerType(player)];
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

    private void OnGameOver(GameMode.GameOverReason reason)
    {
        // Hide all other UI elements
        gameCountdownText.enabled = false;
        outOfLivesText.gameObject.SetActive(false);
        matchPreparationUIScreen.SetActive(false);
        waveCounterText.gameObject.SetActive(false);
        waveProgressBarObj.SetActive(false);
        
        // Unsubscribe from events to prevent further updates
        GameMode.OnLivesChanged -= OnLivesChanged;
        GameMode.OnGameCooldownChanged -= OnGameCountdownChanged;
        GameMode.OnScoreChanged -= UpdateScore;
        GameMode.OnPlayerCharacterSpawned -= OnPlayerCharacterSpawned;
        GameMode.WaveStartedEvent -= OnWaveStarted;
        GameMode.GameOverEvent -= OnGameOver;
        GameMode.OnHordeProgressChanged -= UpdateProgressBar;
        GameMode.OnMasterClientChanged -= OnMasterClientChanged;

        switch (reason)
        {
            case GameMode.GameOverReason.AllPlayersDied:
                gameOverReasonText.text = "All Players Died";
                break;
            case GameMode.GameOverReason.MosquitosReachedLimit:
                gameOverReasonText.text = "Mosquitos Reached Limit";
                break;
        }
        
        wavesReachedText.text = $"Waves Reached: {GameMode.Instance.CurrentWave}";

        SetupScoreBoard();
        
        gameOverUIScreen.SetActive(true);
    }

    private void SetupScoreBoard()
    {
        // Get all players and their scores
        var players = PhotonNetwork.CurrentRoom.Players.Values;
        List<Player> sortedPlayers = new List<Player>(players);

        // sort players by score in ascending order of the score
        sortedPlayers.Sort((p1, p2) => {
            int score1 = GameMode.GetPlayerScore(p1);
            int score2 = GameMode.GetPlayerScore(p2);
            return score2.CompareTo(score1); // Descending order
        });

        for (int i = 0; i < sortedPlayers.Count; i++)
        {
            CreateScoreInfoForPlayer(sortedPlayers[i], i + 1, GameMode.GetPlayerScore(sortedPlayers[i]));
        }
    }

    private void CreateScoreInfoForPlayer(Player player, int rank, int score)
    {
        GameObject scoreInfoGO = Instantiate(playerScoreInfoPrefab, scoreBoardContent);
        
        var scoreRankText = scoreInfoGO.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        var playerIcon = scoreInfoGO.transform.GetChild(1).GetComponent<Image>();
        var playerNicknameText = scoreInfoGO.transform.GetChild(2).GetComponent<TextMeshProUGUI>();
        var playerScoreText = scoreInfoGO.transform.GetChild(3).GetComponent<TextMeshProUGUI>();

        Color playerColor = GameMode.GetPlayerColorBasedOnPlayerType(player);
        
        scoreRankText.text = rank.ToString();
        scoreRankText.color = playerColor;
        playerIcon.sprite = playerIconSprites[GameMode.GetPlayerType(player)];
        playerNicknameText.text = player.NickName;
        playerNicknameText.color = playerColor;
        playerScoreText.text = "Score: " + score;
        playerScoreText.color = playerColor;
    }
    
    public void ReturnToMainMenu()
    {
        PhotonNetwork.LeaveRoom();                
    }
    
    private void OnWaveStarted(int waveNumber)
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

        if(progress >= 100)
        {
            StartCoroutine(QueenIncomingRoutine());
        }
    }

    private IEnumerator QueenIncomingRoutine()
    {
        queenIncomingText.gameObject.SetActive(true);

        yield return new WaitForSeconds(3f);

        queenIncomingText.gameObject.SetActive(false);
    }
    
    private void OnRoomReady()
    {
        if (PhotonNetwork.IsMasterClient)
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

    private void OnEnable()
    {
        GameMode.OnLivesChanged += OnLivesChanged;
        GameMode.OnGameCooldownChanged += OnGameCountdownChanged;
        GameMode.OnScoreChanged += UpdateScore;
        GameMode.OnPlayerCharacterSpawned += OnPlayerCharacterSpawned;
        GameMode.WaveStartedEvent += OnWaveStarted;
        GameMode.GameOverEvent += OnGameOver;
        GameMode.OnHordeProgressChanged += UpdateProgressBar;
        GameMode.OnMasterClientChanged += OnMasterClientChanged;
        GameMode.WaveCompletedEvent += OnWaveCompleted;
        
        GameMode.RoomReady += OnRoomReady;
    }

    private void OnDisable()
    {
        GameMode.OnScoreChanged -= UpdateScore;
        GameMode.OnGameCooldownChanged -= OnGameCountdownChanged;
        GameMode.OnLivesChanged -= OnLivesChanged;
        GameMode.OnPlayerCharacterSpawned -= OnPlayerCharacterSpawned;
        GameMode.WaveStartedEvent -= OnWaveStarted;
        GameMode.GameOverEvent -= OnGameOver;
        GameMode.OnHordeProgressChanged -= UpdateProgressBar;
        GameMode.OnMasterClientChanged -= OnMasterClientChanged;
        GameMode.WaveCompletedEvent -= OnWaveCompleted;
        GameMode.RoomReady -= OnRoomReady;
    }

    private void OnApplicationQuit()
    {
        _bGameIsRunning = false;
    }

    private void OnDestroy()
    {
        if (_matchPreparationUI.StartGameButton != null)
        {
            _matchPreparationUI.StartGameButton.onClick.RemoveListener(StartWave);
        }
    }
}
