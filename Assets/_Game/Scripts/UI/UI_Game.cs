using System.Collections;
using System.Collections.Generic;
using _Game.Scripts;
using AYellowpaper.SerializedCollections;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class UI_Game : MonoBehaviour, IInRoomCallbacks, IMatchmakingCallbacks
{
    private struct PlayerUIStatus
    {
        public GameObject PlayerUIStatusObject;
        public Image PlayerIcon;
        public Image ScoreAndLivesOverlay;
        public Image NickNameOverlay;
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
        public Image AwaitingHostText;
    }
    
    [Header("UI Elements")]
    public GameObject gameCountDownOverlay;
    public TextMeshProUGUI gameCountdownText;
    public GameObject outOfLives;
    public GameObject stopInvasionWarning;
    public GameObject queenIncoming;
    
    [Header("UI Screens")]
    public GameObject gameOverUIScreen;
    public GameObject matchPreparationUIScreen;

    [Header("Wave UI Elements")] 
    public GameObject waveOverlay;
    public TextMeshProUGUI waveCounterText;
    public GameObject waveProgressBarObj;
    
    [Header("Player Status UI Elements")]
    public GameObject statusBarUI;
    public Transform pickupStatusUI;
    public SerializedDictionary<PlayerType, Sprite> playerIconSprites;
    public SerializedDictionary<int, Sprite> playerRankIconSprites;
    
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
    
    
    [Space(20)]
    
    public SerializedDictionary<PlayerType, Color> playerTypeColors;

    private PlayerUIStatus _localPlayerStatus;
    private bool _bGameIsRunning = false;
    private WaveProgressBar _waveProgressBar;
    private MatchPreparationUI _matchPreparationUI;

    private void Awake()
    {
        gameCountDownOverlay.SetActive(false);
        gameCountdownText.enabled = false;
        stopInvasionWarning.SetActive(false);
        waveOverlay.SetActive(false);
        waveCounterText.gameObject.SetActive(false);
        waveProgressBarObj.SetActive(false);
        outOfLives.SetActive(false);
        
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
            AwaitingHostText = matchPreparationUIScreen.transform.GetChild(1).GetComponent<Image>()
        };

        _matchPreparationUI.StartGameButton.onClick.AddListener(StartWave);
    }

    private void Start()
    {
        _bGameIsRunning = true;                
    }

    public void OnJoinedRoom()
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
    
    public void OnMasterClientSwitched(Player newMasterClient)
    {
        Debug.Log($"Master client switched to: {newMasterClient.NickName} (ActorNumber: {newMasterClient.ActorNumber})");
        
        if(PhotonNetwork.LocalPlayer.ActorNumber == newMasterClient.ActorNumber)
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

    private void StartWave()
    {
        GameMode.Instance.StartWave();
    }

    private void OnGameCountdownChanged(int countdown)
    {
        matchPreparationUIScreen.SetActive(false);

        gameCountdownText.enabled = countdown >= 0;
        gameCountDownOverlay.SetActive(countdown >= 0);
        gameCountdownText.text = countdown.ToString();
        
        if(countdown == 0)
        {
            gameCountdownText.enabled = false;
            stopInvasionWarning.SetActive(true);
        }
    }

    private void OnWaveCompleted(int waveNumber)
    {
        gameCountDownOverlay.SetActive(false);
        gameCountdownText.enabled = true;
        gameCountdownText.text = $"Wave {waveNumber} Completed!";

        StartCoroutine(HideWaveCompletedMessageAfterDelay(3f));
    }

    private IEnumerator HideWaveCompletedMessageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        gameCountDownOverlay.SetActive(false);
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
            outOfLives.SetActive(true);
            return;
        }
        
        outOfLives.SetActive(false);
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
        
        var ScoreAndLivesOverlay = playerStatusGO.transform.GetChild(0).GetComponent<Image>();
        var playerIcon = playerStatusGO.transform.GetChild(1).GetComponent<Image>();
        var scoreCounterText = playerStatusGO.transform.GetChild(4).GetComponent<TextMeshProUGUI>();
        var livesCounterText = playerStatusGO.transform.GetChild(2).GetComponent<TextMeshProUGUI>();
        var NickNameOverlay = playerStatusGO.transform.GetChild(6).GetComponent<Image>();
        var nicknameText = playerStatusGO.transform.GetChild(7).GetComponent<TextMeshProUGUI>();
        
        PlayerUIStatus playerStatus = new PlayerUIStatus
        {
            PlayerUIStatusObject = playerStatusGO,
            PlayerIcon = playerIcon,
            ScoreAndLivesOverlay = ScoreAndLivesOverlay,
            NickNameOverlay = NickNameOverlay,
            PlayerNicknameText = nicknameText,
            ScoreCounterText = scoreCounterText,
            LivesCounterText = livesCounterText
        };

        Color playerColor = playerTypeColors[GameMode.GetPlayerType(player)];

        playerIcon.sprite = playerIconSprites[GameMode.GetPlayerType(player)];
        playerStatus.PlayerNicknameText.text = player.NickName;
        playerStatus.PlayerNicknameText.color = GameMode.GetPlayerColorBasedOnPlayerType(player);
        playerStatus.ScoreCounterText.text = "0";
        playerStatus.ScoreCounterText.color = playerColor;
        playerStatus.LivesCounterText.text = GameMode.Instance.StartingLives.ToString();
        playerStatus.LivesCounterText.color = playerColor;
        playerStatus.NickNameOverlay.color = playerColor;
        playerStatus.ScoreAndLivesOverlay.color = playerColor;

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
        gameCountDownOverlay.SetActive(false);
        gameCountdownText.enabled = false;
        outOfLives.SetActive(false);
        matchPreparationUIScreen.SetActive(false);
        waveOverlay.SetActive(false);
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
        if(playerRankIconSprites == null || !playerRankIconSprites.ContainsKey(rank))
        {
            Debug.LogError("No sprite found for player rank: " + rank);
            return;
        }

        GameObject scoreInfoGO = Instantiate(playerScoreInfoPrefab, scoreBoardContent);
        
        var scoreRankIcon = scoreInfoGO.transform.GetChild(0).GetComponent<Image>();
        var playerIcon = scoreInfoGO.transform.GetChild(2).GetComponent<Image>();
        var playerNicknameText = scoreInfoGO.transform.GetChild(3).GetComponent<TextMeshProUGUI>();
        var playerScoreCounterText = scoreInfoGO.transform.GetChild(5).GetComponent<TextMeshProUGUI>();

        Color playerColor = GameMode.GetPlayerColorBasedOnPlayerType(player);
        
        scoreInfoGO.GetComponent<Image>().color = playerColor;
        
        scoreRankIcon.sprite = playerRankIconSprites[rank];
        scoreRankIcon.color = playerColor;
        playerIcon.sprite = playerIconSprites[GameMode.GetPlayerType(player)];
        playerNicknameText.text = player.NickName;
        playerNicknameText.color = playerColor;
        playerScoreCounterText.text = score.ToString();
    }
    
    public void ReturnToMainMenu()
    {
        PhotonNetwork.LeaveRoom();                
    }
    
    private void OnWaveStarted(int waveNumber)
    {
        gameCountDownOverlay.SetActive(false);
        gameCountdownText.enabled = false;
        stopInvasionWarning.SetActive(false);
        
        waveOverlay.SetActive(true);
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
        queenIncoming.SetActive(true);

        yield return new WaitForSeconds(3f);

        queenIncoming.SetActive(false);
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
        GameMode.WaveCompletedEvent += OnWaveCompleted;
        GameMode.RoomReady += OnRoomReady;
        
        PhotonNetwork.AddCallbackTarget(this);
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
        GameMode.WaveCompletedEvent -= OnWaveCompleted;
        GameMode.RoomReady -= OnRoomReady;
        
        PhotonNetwork.RemoveCallbackTarget(this);
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

    public void OnPlayerEnteredRoom(Player newPlayer) { }

    public void OnPlayerLeftRoom(Player otherPlayer) { }

    public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) { }

    public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) { }

    public void OnFriendListUpdate(List<FriendInfo> friendList) { }

    public void OnCreatedRoom() { }

    public void OnCreateRoomFailed(short returnCode, string message) { }

    public void OnJoinRoomFailed(short returnCode, string message) { }

    public void OnJoinRandomFailed(short returnCode, string message) { }

    public void OnLeftRoom() { }
}
