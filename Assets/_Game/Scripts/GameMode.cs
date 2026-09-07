using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace _Game.Scripts
{
    public class GameMode : SceneSingleton<GameMode>
    {
        [Header("Game Mode")]
        [SerializeField] private int startGameDelay = 3;
        [SerializeField] private int startingLives = 3;
        [SerializeField] private Player playerPrefab;
        [SerializeField] private Invaders invadersPrefab;
        
        private int _gameCountdown;
        private int _currentLives;
        private Invaders _invaders;
        private int _currentScore;
        
        public int StartingLives => startingLives;
        public int StartGameDelay => startGameDelay;
        
        public static Action<int> OnGameCooldownChanged;
        public static Action<int> OnLivesChanged;
        public static Action<int> OnInvaderKilled;
        public static Action<int> OnScoreChanged;

        protected override void Awake()
        {
            base.Awake();
        }

        private void Start()
        {
            Debug.Log("GameMode.Start");
            _currentLives = startingLives;
            StartCoroutine(StartGameCooldown());
            FindAnyObjectByType<Player>().OnPlayerDestroyed += OnPlayerDestroyed;
            
            OnInvaderKilled += AddPoints;
        }
        
        private IEnumerator StartGameCooldown()
        {
            _gameCountdown = startGameDelay;
            OnGameCooldownChanged?.Invoke(_gameCountdown);
            while (_gameCountdown > -1)
            {
                Debug.Log($"GameMode.StartGameCooldown: {_gameCountdown}");
                yield return new WaitForSeconds(1.0f);
                OnGameCooldownChanged?.Invoke(--_gameCountdown);
            }
            
            Debug.Log("GameMode.StartGameCooldown: Game Started");
            
            _invaders = Instantiate(invadersPrefab);
            _invaders.OnInvadersDestroyed += WaveCompleted;
        }
        
        private void AddPoints(int points)
        {
            _currentScore += points;
            OnScoreChanged?.Invoke(_currentScore);
        }
        
        private void WaveCompleted()
        {
            Debug.Log("GameMode.WaveCompleted");
            
            _invaders.OnInvadersDestroyed -= WaveCompleted;
            
            StartCoroutine(StartGameCooldown());
        }
        
        private void OnPlayerDestroyed()
        {
            Debug.Log("GameMode.OnPlayerDestroyed");
            _currentLives--;
            OnLivesChanged?.Invoke(_currentLives);
            _invaders.enabled = false;

            if (_currentLives <= 0)
            {
                Debug.Log("GameMode.OnPlayerDestroyed: Game Over");
                return;
            }
            

            StartCoroutine(PlayerDestroyedCooldown());
        }
        
        private IEnumerator PlayerDestroyedCooldown()
        {
            yield return new WaitForSeconds(2.0f);
            
            _invaders.enabled = true;
            
            var player = Instantiate(playerPrefab);
            player.OnPlayerDestroyed += OnPlayerDestroyed;
            
        }
    }
}