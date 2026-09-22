using System.Collections.Generic;
using _Game.Scripts;
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
    
    [Header("UI Elements")]
    public GameObject playerStatusPrefab;
    public GameObject statusBarUI;
    public TextMeshProUGUI gameCountdownText;
    public GameObject gameOverUI;

    private PlayerUIStatus _localPlayerStatus;

    // Constants for finding child components in the player status prefab
    private const string PlayerNicknameTextComponentName = "Player Nickname Text (TMP)";
    private const string ScoreCounterTextComponentName = "Score Counter Text (TMP)";
    private const string LivesCounterTextComponentName = "Lives Counter Text (TMP)";

    private void Awake()
    {
        _localPlayerStatus = new PlayerUIStatus
        {
            PlayerUIStatusObject = null,
            PlayerIcon = null,
            PlayerNicknameText = null,
            ScoreCounterText = null,
            LivesCounterText = null
        };        
    }    
    
    private void OnGameCountdownChanged(int countdown)
    {
        gameCountdownText.text = countdown.ToString();
        
        if(countdown == 0)
        {
            gameCountdownText.text = "GO!";
        }


        gameCountdownText.enabled = countdown >= 0;
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
    
    private void OnGameOver(int playerCharactersAlive)
    {
        gameCountdownText.enabled = false;
        gameOverUI.SetActive(true);
    }
    
    public void ReturnToMainMenu()
    {
        PhotonNetwork.LeaveRoom();                
    }
    
    private void OnGameStarted()
    {
        gameCountdownText.enabled = false;
    }

    private void OnEnable()
    {
        GameMode.OnLivesChanged += OnLivesChanged;
        GameMode.OnGameCooldownChanged += OnGameCountdownChanged;
        GameMode.OnScoreChanged += UpdateScore;
        GameMode.OnPlayerCharacterSpawned += SpawnPlayerStatusUI;
        GameMode.GameStartedEvent += OnGameStarted;
        GameMode.GameOverEvent += OnGameOver;
    }

    private void OnDisable()
    {
        GameMode.OnScoreChanged -= UpdateScore;
        GameMode.OnGameCooldownChanged -= OnGameCountdownChanged;
        GameMode.OnLivesChanged -= OnLivesChanged;
        GameMode.OnPlayerCharacterSpawned -= SpawnPlayerStatusUI;
        GameMode.GameStartedEvent -= OnGameStarted;
        GameMode.GameOverEvent -= OnGameOver;
    }
}
