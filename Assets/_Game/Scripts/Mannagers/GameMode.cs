using System;
using System.Collections;
using System.Collections.Generic;
using _Game.Scripts.SpaceInvaders;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public enum PlayerTypes
{
    Player1,
    Player2,
    Player3
}

namespace _Game.Scripts
{
    [RequireComponent(typeof(PhotonView))]
    public class GameMode : SceneSingleton<GameMode>
    {
        [Serializable]
        private struct PickupSpawnSettings
        {
            public float MinSpawnInterval;
            public float MaxSpawnInterval;
            public int OneUpSpawnChance;
            public int ToolboxSpawnChance;
        }
        
        [Header("Game Mode")]
        [SerializeField] private int startGameDelay = 3;
        [SerializeField] private int startingLives = 3;
        [SerializeField] private int hordesPerWave = 3;
        [SerializeField] private float intervalBetweenHordes = 2.0f;
        public float playerDeathCooldown = 3.0f;
        
        [Header("Prefabs")]
        [SerializeField] private PlayerController playerControllerPrefab;
        [SerializeField] private Mosquitos mosquitosPrefab;
        [SerializeField] private PlayerCharacter[] playerCharacterPrefabs = new PlayerCharacter[3];
        [SerializeField] private Pickup[] pickupPrefabs = new Pickup[6];
        
        [Header("Pickups")]
        [SerializeField] private BoxCollider2D pickupsSpawnArea;
        [SerializeField] private PickupSpawnSettings pickupSpawnSettingsWith1CharacterAlive;
        [SerializeField] private PickupSpawnSettings pickupSpawnSettingsWith2CharactersAlive;
        [SerializeField] private PickupSpawnSettings pickupSpawnSettingsWith3CharactersAlive;
        
        [Header("Debug")]
        public bool bDebugMode = false;
        
        private PhotonView _photonView;
        private int _gameCountdown;
        private int _currentLives;
        private Mosquitos _mosquitosInTheScene;
        private int _hordesSpawnedInCurrentWave = 0;
        private int _currentWave = 1;
        private Dictionary<PlayerType, PlayerCharacter> _playerCharacters = new Dictionary<PlayerType, PlayerCharacter>();
        private readonly Vector3 _playerSpawnPosition = new Vector3(0, -18.2f, 0);
        private bool _bGameCountdownRunning = false;
        private Pickup.PickupType _lastPickupTypeSpawned = Pickup.PickupType.Default;
        
        public static readonly string PlayerTypeKey = "AvailablePlayerTypes";
        public static readonly string PlayerScoreKey = "PlayerScore";
        public static readonly string PlayerCharactersAliveKey = "PlayerCharactersAlive";
        
        public int StartingLives => startingLives;
        public int StartGameDelay => startGameDelay;
        public float HordeProgressPercentage => (_hordesSpawnedInCurrentWave/ (float)hordesPerWave) * 100f;
        
        public static Action<int> OnGameCooldownChanged;
        public static Action<int> OnLivesChanged;
        public static Action OnScoreChanged;
        public static Action<int> GameStartedEvent;
        public static Action<int> GameOverEvent;
        public static Action<Player, PlayerCharacter> OnPlayerCharacterSpawned;
        public static Action<Player> OnPlayerLeft;
        public static Action RoomReady;
        public static Action<float> OnHordeProgressChanged;

        protected override void Awake()
        {
            base.Awake();
            
            _photonView = GetComponent<PhotonView>();
            
            _playerCharacters = new Dictionary<PlayerType, PlayerCharacter>
            {
                { PlayerType.Player1, playerCharacterPrefabs[0] },
                { PlayerType.Player2, playerCharacterPrefabs[1] },
                { PlayerType.Player3, playerCharacterPrefabs[2] }
            };
        }

        private void Start()
        {
            if(bDebugMode)
                Debug.Log("GameMode.Start");
            
            _currentLives = startingLives;            
            
            InvadersLimit.OnInvadersLimitReached += OnInvadersReachedLimit;

            StartCoroutine(WaitForRoomBeReady());
        }

        private IEnumerator WaitForRoomBeReady()
        {
            yield return new WaitUntil(() => PhotonNetwork.InRoom);
            
            RoomReady?.Invoke();
    
            SpawnPlayerController();
            _photonView.RPC(nameof(RpcRegisterPlayer), RpcTarget.MasterClient,
                PhotonNetwork.LocalPlayer.ActorNumber);
            
            
            StartCoroutine(StartGameCountdown());
        }

        public override void OnLeftRoom()
        {
            // Remove Player Type and Player Score custom properties when leaving the room
            if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(PlayerTypeKey))
            {
                PhotonNetwork.LocalPlayer.CustomProperties.Remove(PlayerTypeKey);
            }

            if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(PlayerScoreKey))
            {
                PhotonNetwork.LocalPlayer.CustomProperties.Remove(PlayerScoreKey);
            }

            GameManager.LoadMainMenu();
        }


        private void SpawnPlayerController()
        {
            var playerController = Instantiate(playerControllerPrefab, Vector3.zero, Quaternion.identity);

            playerController.name = "PlayerController for Player: " + PhotonNetwork.NickName;
        }
        
        [PunRPC]
        private void RpcRegisterPlayer(int requestingActorNumber)
        {
            if(!PhotonNetwork.IsMasterClient) return;
            
            var requestingPlayer = PhotonNetwork.CurrentRoom.GetPlayer(requestingActorNumber);
            
            var playerType = GetAvailablePlayerType(requestingPlayer);
            
            SetPlayerType(requestingPlayer, playerType);
            SetPlayerScore(requestingPlayer, 0);
            
            if(bDebugMode)
                Debug.Log("GameMode.RpcRegisterPlayer: assigned 'PlayerType' " + playerType + " to player " + requestingPlayer.NickName);
            
            _photonView.RPC(nameof(RpcSpawnPlayerCharacter), requestingPlayer, playerType);
        }

        private PlayerType GetAvailablePlayerType(Player requestingPlayer)
        {
            var playersInTheRoom = PhotonNetwork.CurrentRoom.PlayerCount;
            
            // If there is only one player in the room, assign PlayerType.Player1 to the requesting player
            if(playersInTheRoom != 1)
            {
                List<PlayerType> availablePlayerTypes = new List<PlayerType>();
                
                foreach (var player in PhotonNetwork.CurrentRoom.Players)
                {
                    if(player.Value.ActorNumber == requestingPlayer.ActorNumber) continue;
                    
                    if(player.Value.CustomProperties.TryGetValue(PlayerTypeKey, out var index))
                    {
                        availablePlayerTypes.Add((PlayerType)index);
                    }
                    else
                    {
                        Debug.LogError("There is a player in the room without a PlayerType custom property. This should not happen." +
                                       "\nPlayer: " + player.Value.NickName + " ActorNumber: " + player.Value.ActorNumber);
                    }
                }
                
                for (int i = 0; i < Enum.GetValues(typeof(PlayerType)).Length; i++)
                {
                    if (!availablePlayerTypes.Contains((PlayerType)i))
                    {
                        return (PlayerType)i;
                    }
                }
            }

            return PlayerType.Player1;
        }
        
        public static PlayerType GetPlayerType(Player player)
        {
            if (player.CustomProperties.TryGetValue(PlayerTypeKey, out var index))
            {
                return (PlayerType)index;
            }
            else
            {
                Debug.LogWarning("GameMode.GetPlayerType: Player " + player.NickName + " does not have a PlayerType custom property.");
                return PlayerType.Player1;
            }
        }

        public static Color GetPlayerColorBasedOnPlayerType(Player player)
        {
            if(player == null)
            {
                Debug.LogWarning("GameMode.GetPlayerColorBasedOnPlayerType: Player is null.");
                return Color.white;
            }
            
            PlayerType playerType = GetPlayerType(player);
            return playerType switch
            {
                PlayerType.Player1 => Color.green,
                PlayerType.Player2 => Color.blue,
                PlayerType.Player3 => Color.red,
                _ => Color.white
            };
        }
        
        private void SetPlayerType(Player player, PlayerType playerType)
        {
            if(!PhotonNetwork.InRoom || !PhotonNetwork.IsConnectedAndReady) return;
            
            player.SetCustomProperties(new Hashtable { { PlayerTypeKey, playerType } });
        }

        [PunRPC]
        private void RpcSpawnPlayerCharacter(PlayerType playerType)
        {
            if(bDebugMode)
                Debug.Log("GameMode.RpcSpawnPlayerCharacter: received player type " + playerType + " for player " + PhotonNetwork.LocalPlayer.NickName);
            
            var playerCharacter = PhotonNetwork.Instantiate(_playerCharacters[playerType].name, _playerSpawnPosition, Quaternion.identity).GetComponent<PlayerCharacter>();
            PlayerController.Local.Possess(playerCharacter);
            
            OnPlayerCharacterSpawned?.Invoke(PhotonNetwork.LocalPlayer, playerCharacter);
            
            var playerCharactersAlive = GetOrInitPlayerCharactersAlive() + 1;
            
            SetPlayerCharactersAlive(playerCharactersAlive);

            playerCharacter.OnPlayerDeath += OnPlayerDestroyed;
        }

        public static int GetOrInitPlayerCharactersAlive()
        {
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PlayerCharactersAliveKey, out var index))
            {
                return (int)index;
            }
            else
            {
                return 0;
            }
        }
        
        private void SetPlayerCharactersAlive(int count)
        {
            if(!PhotonNetwork.InRoom || !PhotonNetwork.IsConnectedAndReady) return;
            
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { PlayerCharactersAliveKey, count } });
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            photonView.RPC(nameof(RpcReplicateWaveStatus), newPlayer, _currentWave, _hordesSpawnedInCurrentWave);
            
            photonView.RPC(nameof(RpcReplicateGameCooldown), newPlayer, _gameCountdown);

            if (_mosquitosInTheScene)
            {
                photonView.RPC(nameof(RpcSubscribeToMosquitosHordeEvents), newPlayer);
            }
            
        }
        
        [PunRPC]
        private void RpcReplicateWaveStatus(int waveNumber, int hordesSpawned)
        {
            _currentWave = waveNumber;
            _hordesSpawnedInCurrentWave = hordesSpawned;
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            OnPlayerLeft?.Invoke(otherPlayer);
            
            if(!PhotonNetwork.IsMasterClient) return;
            
            // If countdown is running and the master client leaves, the new master client should start the countdown again
            if (_bGameCountdownRunning )
                StartCoroutine(StartGameCountdown());
            
            SetPlayerCharactersAlive(GetOrInitPlayerCharactersAlive() - 1);
        }

        private IEnumerator StartGameCountdown()
        {     
            if(!PhotonNetwork.IsMasterClient) yield break;
            
            _gameCountdown = startGameDelay;
            
            OnGameCooldownChanged?.Invoke(_gameCountdown);
            while (_gameCountdown > -1)
            {                
                photonView.RPC(nameof(RpcReplicateGameCooldown), RpcTarget.All, --_gameCountdown);
                yield return new WaitForSeconds(1.0f);
            }

            SpawnHorde();
        }
        
        private void SpawnHorde()
        {
            if(!PhotonNetwork.IsMasterClient) return;
            
            _mosquitosInTheScene = PhotonNetwork.InstantiateRoomObject(mosquitosPrefab.name, mosquitosPrefab.transform.position, Quaternion.identity).GetComponent<Mosquitos>();
            _photonView.RPC(nameof(RpcSubscribeToMosquitosHordeEvents), RpcTarget.All);
        }

        [PunRPC]
        private void RpcReplicateGameCooldown(int countdown)
        {
            _gameCountdown = countdown;
            _bGameCountdownRunning = true;
            
            if(_gameCountdown > -1)
            {                
                OnGameCooldownChanged?.Invoke(_gameCountdown);

                if (bDebugMode)
                    Debug.Log($"GameMode.StartGameContdown: {_gameCountdown}");

                return;
            }

            if (bDebugMode)
                Debug.Log("GameMode.StartGameContdown: Game Started");
            
            _bGameCountdownRunning = false;
            
            GameStartedEvent?.Invoke(_currentWave);     
            
            InvokePickupSpawn();
        }
        
        [PunRPC]
        private void RpcSubscribeToMosquitosHordeEvents()
        {
            if(!_mosquitosInTheScene)
                _mosquitosInTheScene = FindAnyObjectByType<Mosquitos>();
            
            _mosquitosInTheScene.AllMosquitosDestroyedEvent += HordeKilled;
            _mosquitosInTheScene.OnMosquitoKilled += InvaderKilled;
        }
        
        private void InvaderKilled(int playerID, int points)
        {
            if(!PhotonNetwork.IsMasterClient || playerID == -1) return;
            
            if(bDebugMode)
                Debug.Log($"GameMode.InvaderKilled: Player {playerID} killed an invader for {points} points");
            
            Player player = PhotonNetwork.CurrentRoom.GetPlayer(playerID);
            if (player == null) return;
            
            var playerScore = GetPlayerScore(player) + points;
            SetPlayerScore(player, playerScore);
        }

        private void HordeKilled()
        {
            if(bDebugMode)
                Debug.Log("GameMode.HordeKilled");
            
            _hordesSpawnedInCurrentWave++;
            OnHordeProgressChanged?.Invoke(HordeProgressPercentage);
            
            if (_hordesSpawnedInCurrentWave >= hordesPerWave)
            {
                _hordesSpawnedInCurrentWave = 0;
                _currentWave++;
                WaveCompleted();
            }
            else
            {
                Invoke(nameof(SpawnHorde), intervalBetweenHordes);
            }
        }

        private void WaveCompleted()
        {
            if(bDebugMode)
                Debug.Log("GameMode.WaveCompleted");
            
            CancelInvoke(nameof(SpawnPickup));
            photonView.RPC(nameof(RpcReplicateUnsubscribeFromMosquitosHordeEvents), RpcTarget.All);
            
            StartCoroutine(StartGameCountdown());
        }
        
        [PunRPC]
        private void RpcReplicateUnsubscribeFromMosquitosHordeEvents()
        {
            _mosquitosInTheScene.AllMosquitosDestroyedEvent -= HordeKilled;
            _mosquitosInTheScene.OnMosquitoKilled -= InvaderKilled;
        }
        
        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            // if the changed property is the PlayerScoreKey, invoke the OnScoreChanged event
            if (changedProps.ContainsKey(PlayerScoreKey))
            {
                OnScoreChanged?.Invoke();
            }
        }
        
        private void OnInvadersReachedLimit()
        {
            if(bDebugMode)
                Debug.Log("GameMode.OnInvadersReachedLimit");
            
            _photonView.RPC(nameof(RpcReplicateInvadersReachedLimit), RpcTarget.All);
        }

        [PunRPC]
        private void RpcReplicateInvadersReachedLimit()
        {
            _mosquitosInTheScene.OnMosquitoKilled -= InvaderKilled;
            _mosquitosInTheScene.AllMosquitosDestroyedEvent -= WaveCompleted;
            InvadersLimit.OnInvadersLimitReached -= OnInvadersReachedLimit;
            
            OnPlayerCharacterSpawned = null;
         
            _currentLives = 0;
            
            GameOver(0);
        }
        private void OnPlayerDestroyed(PlayerCharacter playerCharacterDestroyed)
        {            
            if(!bIsRunning) return;
            
            if(bDebugMode)
                Debug.Log("GameMode.OnPlayerDestroyed");
            
            playerCharacterDestroyed.OnPlayerDeath -= OnPlayerDestroyed;
            
            PlayerController.Local.UnPossess();
            
            _currentLives--;

            var playerCharactersAlive = GetOrInitPlayerCharactersAlive() - 1;
            
            SetPlayerCharactersAlive(playerCharactersAlive);
            
            OnLivesChanged?.Invoke(_currentLives);

            if(PhotonNetwork.CurrentRoom.PlayerCount == 1 && _mosquitosInTheScene)
                _mosquitosInTheScene.stopMovement = true;

            if (_currentLives <= 0)
            {
                GameOver(playerCharactersAlive);
                return;
            }

            StartCoroutine(PlayerDeathCooldown());
        }

        private void GameOver(int playerCharactersAlive)
        {
            if(bDebugMode)
                Debug.Log("GameMode.OnPlayerDestroyed: Game Over");

            if (playerCharactersAlive <= 0)
            {
                if(bDebugMode)
                    Debug.Log("GameMode.OnPlayerDestroyed: No more player characters alive, closing room.");

                if (PhotonNetwork.CurrentRoom.IsOpen)
                    PhotonNetwork.CurrentRoom.IsOpen = false;
                
                StopAllCoroutines();
                CancelInvoke(nameof(SpawnPickup));
            }
                
            GameOverEvent?.Invoke(playerCharactersAlive);
        }
        
        private IEnumerator PlayerDeathCooldown()
        {
            yield return new WaitForSeconds(playerDeathCooldown);                       

            if (PhotonNetwork.CurrentRoom.PlayerCount == 1 && _mosquitosInTheScene)
                _mosquitosInTheScene.stopMovement = false;

            PlayerType playerType = PlayerType.Player1;
            if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PlayerTypeKey, out var index))
                playerType = (PlayerType)index;

            RpcSpawnPlayerCharacter(playerType);

        }
        
        public static int GetPlayerScore(Player player)
        {
            if (player.CustomProperties.TryGetValue(PlayerScoreKey, out var index))
            {
                return (int)index;
            }
            else
            {
                Debug.LogError("GameMode.GetPlayerScore: Player " + player.NickName + " does not have a PlayerScore custom property. This should not happen.");
                return 0;
            }
        }
        
        private void SetPlayerScore(Player player, int score)
        {
            if(!PhotonNetwork.InRoom || !PhotonNetwork.IsConnectedAndReady) return;
            
            player.SetCustomProperties(new Hashtable { { PlayerScoreKey, score } });
        }
        
        public void AddExtraLife()
        {
            _currentLives++;
            OnLivesChanged?.Invoke(_currentLives);
        }
        
        private void InvokePickupSpawn()
        {
            PickupSpawnSettings spawnSettings = GetCurrentPickupSpawnSettings();
            
            Invoke(nameof(SpawnPickup), UnityEngine.Random.Range(spawnSettings.MinSpawnInterval, spawnSettings.MaxSpawnInterval));
        }
        
        private PickupSpawnSettings GetCurrentPickupSpawnSettings()
        {
            switch (GetOrInitPlayerCharactersAlive())
            {
                case 1:
                    return pickupSpawnSettingsWith1CharacterAlive;
                case 2:
                    return pickupSpawnSettingsWith2CharactersAlive;
                case 3:
                    return pickupSpawnSettingsWith3CharactersAlive;
                default:
                    return pickupSpawnSettingsWith1CharacterAlive;
            }
        }
        
        private void SpawnPickup()
        {
            PickupSpawnSettings spawnSettings = GetCurrentPickupSpawnSettings();
            if (PhotonNetwork.IsMasterClient)
            {
                var randomPickupIndex = UnityEngine.Random.Range(0, pickupPrefabs.Length);
                var randomPickupPrefab = pickupPrefabs[randomPickupIndex];

                if (randomPickupPrefab.Type == _lastPickupTypeSpawned)
                {
                    randomPickupIndex = (randomPickupIndex + 1) % pickupPrefabs.Length;
                    randomPickupPrefab = pickupPrefabs[randomPickupIndex];
                }
                
                if(randomPickupPrefab.Type == Pickup.PickupType._1UP)
                {
                    var chance = UnityEngine.Random.Range(0, 100);
                    if (chance >= spawnSettings.OneUpSpawnChance)
                    {
                        SpawnPickup();
                        return;
                    }
                }
                
                if(randomPickupPrefab.Type == Pickup.PickupType.Toolbox)
                {
                    if (BarriersManager.IsAnyBarrierDestroyed())
                    {

                        var chance = UnityEngine.Random.Range(0, 100);
                        if (chance >= spawnSettings.ToolboxSpawnChance)
                        {
                            SpawnPickup();
                            return;
                        }
                        Debug.Log("GameMode.SpawnPickup: Spawning Toolbox pickup because at least one barrier has been destroyed.");
                    }
                    else
                    {
                        SpawnPickup();
                        return;
                    }
                }

                _lastPickupTypeSpawned = randomPickupPrefab.Type;

                var randomX = UnityEngine.Random.Range(pickupsSpawnArea.bounds.min.x, pickupsSpawnArea.bounds.max.x);
                var randomY = UnityEngine.Random.Range(pickupsSpawnArea.bounds.min.y, pickupsSpawnArea.bounds.max.y);
                var randomPosition = new Vector3(randomX, randomY, 0f);

                PhotonNetwork.InstantiateRoomObject(randomPickupPrefab.name, randomPosition, Quaternion.identity);
            }

            InvokePickupSpawn();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            
            InvadersLimit.OnInvadersLimitReached -= OnInvadersReachedLimit;

            if (_mosquitosInTheScene == null) return;
            
            _mosquitosInTheScene.AllMosquitosDestroyedEvent -= WaveCompleted;
            _mosquitosInTheScene.OnMosquitoKilled -= InvaderKilled;
        }
    }
}