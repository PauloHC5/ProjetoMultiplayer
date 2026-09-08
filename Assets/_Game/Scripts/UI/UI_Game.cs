using System;
using System.Collections.Generic;
using _Game.Scripts;
using TMPro;
using UnityEngine;

public class UI_Game : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI scoreCounterText;
    public TextMeshProUGUI livesCounterText;
    public TextMeshProUGUI gameCountdownText;
    
    private Dictionary<string, TextMeshProUGUI> _plauerUIStatus = new Dictionary<string, TextMeshProUGUI>();

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

        gameCountdownText.gameObject.SetActive(countdown >= 0);
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
    
    private void OnEnable()
    {
        GameMode.OnLivesChanged += OnLivesChanged;
        GameMode.OnGameCooldownChanged += OnGameCountdownChanged;
        GameMode.OnScoreChanged += UpdateScore;
    }

    private void OnDisable()
    {
        GameMode.OnScoreChanged -= UpdateScore;
        GameMode.OnGameCooldownChanged -= OnGameCountdownChanged;
        GameMode.OnLivesChanged -= OnLivesChanged;
    }
}
