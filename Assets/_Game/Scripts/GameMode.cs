using System;
using System.Collections;
using System.Collections.Generic;
using _Game.Scripts.SpaceInvaders;
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
        [SerializeField] private PlayerController playerControllerPrefab;
        [SerializeField] private PlayerCharacter[] playerCharacterPrefabs = new PlayerCharacter[3];
        [SerializeField] private Invaders invadersPrefab;
        
        [Header("Debug")]
        public bool bDebugMode = false;
        
        private int _gameCountdown;
        private int _currentLives;
        private Invaders _invaders;
        private int _currentScore;
        private bool _bGameStarted = false;        

        private readonly Vector3 _playerSpawnPosition = new Vector3(0, -13f, 0);
        private const string AvailablePlayerCharacterPrefabsKey = "AvailablePlayerCharacterPrefabs";
        private const string PlayerCharacterPrefabIndexKey = "PlayerPrefabIndex";
        
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

            StartCoroutine(WaitForRoomThenSpawn());
        }

        private IEnumerator WaitForRoomThenSpawn()
        {
            yield return new WaitUntil(() => PhotonNetwork.InRoom);
    
            SpawnPlayerController();
            photonView.RPC(nameof(RpcRequestPlayerCharacter), RpcTarget.MasterClient,
                PhotonNetwork.LocalPlayer.ActorNumber);
        }


        private void SpawnPlayerController()
        {
            var playerController = Instantiate(playerControllerPrefab, Vector3.zero, Quaternion.identity);

            playerController.name = "PlayerController for Player: " + PhotonNetwork.NickName;
        }
        
        [PunRPC]
        private void RpcRequestPlayerCharacter(int requestingActorNumber)
        {
            if(!PhotonNetwork.IsMasterClient) return;

            // Get the available player prefabs from the room custom properties
            var availablePlayerCharacterPrefabs = GetAvailablePlayerCharacterPrefabs();
            
            // Find the first available player prefab index
            int prefabToUseIndex = Array.IndexOf(availablePlayerCharacterPrefabs, true);
            
            if(prefabToUseIndex == -1)
            {
                Debug.LogError("No available player prefabs to spawn.");
                return;
            }
            
            availablePlayerCharacterPrefabs[prefabToUseIndex] = false; // Mark this prefab as unavailable for future players
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { AvailablePlayerCharacterPrefabsKey, availablePlayerCharacterPrefabs } });
            
            var requestingPlayer = PhotonNetwork.CurrentRoom.GetPlayer(requestingActorNumber);
            requestingPlayer.SetCustomProperties(new Hashtable { { PlayerCharacterPrefabIndexKey, prefabToUseIndex } });
            
            photonView.RPC(nameof(RpcSpawnPlayerCharacter), requestingPlayer, prefabToUseIndex);
        }

        private bool[] GetAvailablePlayerCharacterPrefabs()
        {
            // Check if the room custom properties contain the availablePlayerCharacterPrefabs key and get its value
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(AvailablePlayerCharacterPrefabsKey, out var value))
                return (bool[])value;
            
            // If the availablePlayerCharacterPrefabs is not set, initialize it with all prefabs available
            var fresh = new bool[playerCharacterPrefabs.Length];
            for (int i = 0; i < fresh.Length; i++) fresh[i] = true;
            return fresh;
        }

        [PunRPC]
        private void RpcSpawnPlayerCharacter(int prefabToUseIndex)
        {
            if(bDebugMode)
                Debug.Log("GameMode.RpcSpawnPlayerCharacter: received index " + prefabToUseIndex);
            
            var playerCharacter = PhotonNetwork.Instantiate(playerCharacterPrefabs[prefabToUseIndex].name, _playerSpawnPosition, Quaternion.identity).GetComponent<PlayerCharacter>();
            PlayerController.Local.Possess(playerCharacter);
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            if(!PhotonNetwork.IsMasterClient) return;

            if (!otherPlayer.CustomProperties.TryGetValue(PlayerCharacterPrefabIndexKey, out var index)) return;
            
            var availablePlayerCharacterPrefabs = GetAvailablePlayerCharacterPrefabs();
            
            availablePlayerCharacterPrefabs[(int)index] = true;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { AvailablePlayerCharacterPrefabsKey, availablePlayerCharacterPrefabs } });
        }

        public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            DebugAvailablePlayerCharacterPrefabs();
        }

        private void DebugAvailablePlayerCharacterPrefabs()
        {
            if(!bDebugMode || !PhotonNetwork.IsMasterClient) return;

            var availablePlayerCharacterPrefabs = GetAvailablePlayerCharacterPrefabs();
            
            if(availablePlayerCharacterPrefabs == null) return;
            
            string availablePrefabsString = "Available Player Prefabs: ";
            
            for (int i = 0; i < availablePlayerCharacterPrefabs.Length; i++)
            {
                if (availablePlayerCharacterPrefabs[i])
                {
                    availablePrefabsString += $"\n Player Character {i} Prefab is available. ";
                }
                else
                {
                    availablePrefabsString += $"\n Player Character {i} Prefab is NOT available. ";
                }
            }
            
            Debug.Log(availablePrefabsString);
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