using System;
using System.Collections.Generic;
using _Game.Scripts;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public struct PlayerUIStatus
{
    public GameObject gameObject;
    public Image PlayerIcon;
    public TextMeshProUGUI PlayerNicknameText;
    public TextMeshProUGUI ScoreCounterText;
    public TextMeshProUGUI LivesCounterText;
}

public class UI_Game : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject playerStatusPrefab;
    public GameObject statusBarUI;
    public TextMeshProUGUI gameCountdownText;
    
    private readonly Dictionary<int, PlayerUIStatus> _plauerUIStatus = new Dictionary<int, PlayerUIStatus>();
    
    private const string PlayerNicknameTextComponentName = "Player Nickname Text (TMP)";
    private const string ScoreCounterTextComponentName = "Score Counter Text (TMP)";
    private const string LivesCounterTextComponentName = "Lives Counter Text (TMP)";

    private void Awake()
    {
        //livesCounterText.text = GameMode.Instance.StartingLives.ToString();
        //scoreCounterText.text = "0";
    }

    private void Start()
    {
        //gameCountdownText.text = GameMode.Instance.StartGameDelay.ToString();
        //livesCounterText.text = GameMode.Instance.StartingLives.ToString();
    }
    
    private void OnGameCountdownChanged(int countdown)
    {
        gameCountdownText.text = countdown.ToString();
        
        if(countdown == 0)
        {
            gameCountdownText.text = "GO!";
        }
    }

    private void UpdateScore(int points)
    {
        //scoreCounterText.text = points.ToString();
    }
    
    private void OnLivesChanged(int lives)
    {
        if(lives <= 0)
        {
            //livesCounterText.text = "0";
            return;
        }
        //livesCounterText.text = lives.ToString();
    }
    
    private void OnPlayerLeft(Player player)
    {
        if (!_plauerUIStatus.ContainsKey(player.ActorNumber))
            return;

        var playerStatusText = _plauerUIStatus[player.ActorNumber];
        Destroy(playerStatusText.gameObject);
        _plauerUIStatus.Remove(player.ActorNumber);
    }

    private void SpawnPlayerStatusUI(Player player, PlayerCharacter playerCharacter)
    {
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
            gameObject = playerStatusGO,
            PlayerIcon = playerIcon,
            PlayerNicknameText = nicknameText,
            ScoreCounterText = scoreCounterText,
            LivesCounterText = livesCounterText
        };
        
        Color playerColor = playerCharacter.SpriteRenderer.color;
        
        playerIcon.color = playerColor;
        playerStatus.PlayerNicknameText.text = player.NickName;
        playerStatus.PlayerNicknameText.color = playerColor;
        playerStatus.ScoreCounterText.text = "0";
        playerStatus.ScoreCounterText.color = playerColor;
        playerStatus.LivesCounterText.text = GameMode.Instance.StartingLives.ToString();
        playerStatus.LivesCounterText.color = playerColor;

        _plauerUIStatus.Add(player.ActorNumber, playerStatus);
    }

    private void OnEnable()
    {
        GameMode.OnLivesChanged += OnLivesChanged;
        GameMode.OnGameCooldownChanged += OnGameCountdownChanged;
        GameMode.OnScoreChanged += UpdateScore;
        GameMode.OnPlayerCharacterSpawned += SpawnPlayerStatusUI;
        GameMode.OnPlayerLeft += OnPlayerLeft;
    }

    private void OnDisable()
    {
        GameMode.OnScoreChanged -= UpdateScore;
        GameMode.OnGameCooldownChanged -= OnGameCountdownChanged;
        GameMode.OnLivesChanged -= OnLivesChanged;
        GameMode.OnPlayerCharacterSpawned -= SpawnPlayerStatusUI;
        GameMode.OnPlayerLeft -= OnPlayerLeft;
    }
}
