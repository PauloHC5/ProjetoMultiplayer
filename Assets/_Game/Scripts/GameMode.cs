using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace _Game.Scripts
{
    public class GameMode : SceneSingleton<GameMode>
    {
        [Header("Game Mode")]
        [SerializeField] private int startGameDelay = 3;
        [SerializeField] private int startingLives = 3;
        [SerializeField] private Player[] playerPrefabs = new Player[3];
        [SerializeField] private Invaders invadersPrefab;
        
        [Header("Debug")]
        public bool bDebugMode = false;
        
        private int _gameCountdown;
        private int _currentLives;
        private Invaders _invaders;
        private int _currentScore;
        private bool _bGameStarted = false;
        
        private readonly Vector3 _playerSpawnPosition = new Vector3(0, -13f, 0);
        
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
            if(bDebugMode)
                Debug.Log("GameMode.Start");
            
            _currentLives = startingLives;
            //FindAnyObjectByType<Player>().OnPlayerDestroyed += OnPlayerDestroyed;
            
            OnInvaderKilled += AddPoints;
        }
        
        public override void OnJoinedRoom()
        {
            base.OnJoinedRoom();
            
            if(bDebugMode)
                Debug.LogWarning("GameMode.OnJoinedRoom");
            
            var playersInRoom = PhotonNetwork.CurrentRoom.Players.Count;
            
            PhotonNetwork.Instantiate(playerPrefabs[playersInRoom - 1].name, _playerSpawnPosition, Quaternion.identity);
        }
        
        private IEnumerator StartGameCooldown()
        {
            _bGameStarted = true;
            _gameCountdown = startGameDelay;
            OnGameCooldownChanged?.Invoke(_gameCountdown);
            while (_gameCountdown > -1)
            {
                if (bDebugMode)
                    Debug.Log($"GameMode.StartGameCooldown: {_gameCountdown}");
                
                yield return new WaitForSeconds(1.0f);
                OnGameCooldownChanged?.Invoke(--_gameCountdown);
            }
            
            if(bDebugMode)
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
            if(bDebugMode)
                Debug.Log("GameMode.WaveCompleted");
            
            _invaders.OnInvadersDestroyed -= WaveCompleted;
            
            StartCoroutine(StartGameCooldown());
        }
        
        private void OnPlayerDestroyed()
        {
            if(bDebugMode)
                Debug.Log("GameMode.OnPlayerDestroyed");
            
            _currentLives--;
            OnLivesChanged?.Invoke(_currentLives);
            _invaders.enabled = false;

            if (_currentLives <= 0)
            {
                if(bDebugMode)
                    Debug.Log("GameMode.OnPlayerDestroyed: Game Over");
                return;
            }
            

            StartCoroutine(PlayerDestroyedCooldown());
        }
        
        private IEnumerator PlayerDestroyedCooldown()
        {
            yield return new WaitForSeconds(2.0f);
            
            _invaders.enabled = true;
            
            //var player = Instantiate(playerPrefab);
            //player.OnPlayerDestroyed += OnPlayerDestroyed;
            
        }
    }
}