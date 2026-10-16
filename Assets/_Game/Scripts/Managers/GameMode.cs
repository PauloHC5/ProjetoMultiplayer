using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        [Serializable]
        private struct DengueSourceSpawnSettings
        {
            public float MinSpawnInterval;
            public float MaxSpawnInterval;
            public int MaxDengueSourcesInScene;
        }

        public enum GameState
        {
            WaitingForPlayers,
            Countdown,
            InProgress,
            GameOver
        }

        public enum GameOverReason
        {
            AllPlayersDied,
            MosquitosReachedLimit,
        }

        private struct DebugPlayerCheck
        {
            public Vector2 pos;
            public float checkRadius;
            public bool isPlayerAround;
            public float timeLimit;
        }

        [Header("Game Mode")] [SerializeField] private int startGameDelay = 3;
        [SerializeField] private int startingLives = 3;
        [SerializeField] private int hordesPerWave = 3;
        [SerializeField] private float intervalBetweenHordes = 2.0f;
        public float playerDeathCooldown = 3.0f;
        [SerializeField] private LayerMask playerLayerMask;

        [Header("Prefabs")] [SerializeField] private PlayerController playerControllerPrefab;
        [SerializeField] private MosquitosHorde mosquitosHordePrefab;
        [SerializeField] private PlayerCharacter[] playerCharacterPrefabs = new PlayerCharacter[3];
        [SerializeField] private Pickup[] pickupPrefabs = new Pickup[6];
        [SerializeField] private DengueSource[] dengueSourcePrefabs;

        [Header("Pickups")] [SerializeField] private BoxCollider2D pickupsSpawnArea;
        [SerializeField] private PickupSpawnSettings pickupSpawnSettingsWith1CharacterAlive;
        [SerializeField] private PickupSpawnSettings pickupSpawnSettingsWith2CharactersAlive;
        [SerializeField] private PickupSpawnSettings pickupSpawnSettingsWith3CharactersAlive;
        [SerializeField] private float radiusCheckPlayerPickup = 5.0f;

        [Header("Dengue Source")] [SerializeField]
        private BoxCollider2D dengueSourceSpawnArea;

        [SerializeField] private DengueSourceSpawnSettings dengueSourceSpawnSettingsWith1CharacterAlive;
        [SerializeField] private DengueSourceSpawnSettings dengueSourceSpawnSettingsWith2CharactersAlive;
        [SerializeField] private DengueSourceSpawnSettings dengueSourceSpawnSettingsWith3CharactersAlive;
        [SerializeField] private float radiusCheckPlayerDengueSource = 5.0f;

        [Header("Debug")] public bool bDebugMode = false;
        public float playerAroundCheckDebugTime = 3.0f;
        public Color colorWhenPlayerAround = Color.green;
        public Color colorWhenPlayerNotAround = Color.red;


        private GameState _currentGameState;
        private PhotonView _photonView;
        private int _gameCountdown;
        private int _currentLives;
        private int _playerCharactersAlive;
        private MosquitosHorde _mosquitosHordeInTheScene;
        private int _currentWave = 1;
        private int _dengueSourcesInScene = 0;
        private int _hordesSpawnedInCurrentWave = 0;
        private Dictionary<PlayerType, PlayerCharacter> _playerCharacters;
        
        public int HordesSpawnedInCurrentWave => _hordesSpawnedInCurrentWave;
        public GameState CurrentGameState => _currentGameState;
        public MosquitosHorde MosquitosHordeInTheScene => _mosquitosHordeInTheScene;

        private List<DebugPlayerCheck> _debugCheckPlayerAround;

        private readonly Vector3 _playerSpawnPosition = new Vector3(0, -18.2f, 0);
        private Pickup.PickupType _lastPickupTypeSpawned = Pickup.PickupType.Default;

        public static readonly string PlayerTypeKey = "AvailablePlayerTypes";
        public static readonly string PlayerScoreKey = "PlayerScore";

        public int StartingLives => startingLives;
        public int StartGameDelay => startGameDelay;
        public float HordeProgressPercentage => (_hordesSpawnedInCurrentWave / (float)hordesPerWave) * 100f;

        public static Action<int> OnGameCooldownChanged;
        public static Action<int> OnLivesChanged;
        public static Action OnScoreChanged;
        public static Action<int> WaveStartedEvent;
        public static Action<int> WaveCompletedEvent;
        public static Action<GameOverReason> GameOverEvent;
        public static Action<Player, PlayerCharacter> OnPlayerCharacterSpawned;
        public static Action RoomReady;
        public static Action<float> OnHordeProgressChanged;
        public static Action<bool> OnMasterClientChanged;

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

            if (bDebugMode)
                _debugCheckPlayerAround = new List<DebugPlayerCheck>();
        }

        private void Start()
        {
            if (bDebugMode)
                Debug.Log("GameMode.Start");

            _currentLives = startingLives;
            _currentGameState = GameState.WaitingForPlayers;

            MosquitosLimit.OnMosquitosReachedLimit += OnMosquitosReachedLimit;

            StartCoroutine(WaitForRoomBeReady());
        }

        private IEnumerator WaitForRoomBeReady()
        {
            yield return new WaitUntil(() => PhotonNetwork.InRoom);

            RoomReady?.Invoke();

            SpawnPlayerController();
            _photonView.RPC(nameof(RpcRegisterPlayer), RpcTarget.MasterClient,
                PhotonNetwork.LocalPlayer.ActorNumber);
        }

        public override void OnLeftRoom()
        {
            // Remove Player Type and Player Score custom properties when leaving the room
            if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(PlayerTypeKey))
            {
                PhotonNetwork.LocalPlayer.CustomProperties.Remove(PlayerTypeKey);
            }

            // Remove Player Score custom property when leaving the room
            if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(PlayerScoreKey))
            {
                PhotonNetwork.LocalPlayer.CustomProperties.Remove(PlayerScoreKey);
            }

            GameManager.LoadMainMenu();
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            bool isLocalPlayerMasterClient = PhotonNetwork.LocalPlayer.ActorNumber == newMasterClient.ActorNumber;

            OnMasterClientChanged?.Invoke(isLocalPlayerMasterClient);
        }

        private void SpawnPlayerController()
        {
            var playerController = Instantiate(playerControllerPrefab, Vector3.zero, Quaternion.identity);

            playerController.name = "PlayerController for Player: " + PhotonNetwork.NickName;
        }

        [PunRPC]
        private void RpcRegisterPlayer(int requestingActorNumber)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            var requestingPlayer = PhotonNetwork.CurrentRoom.GetPlayer(requestingActorNumber);

            var playerType = GetAvailablePlayerType(requestingPlayer);

            SetPlayerType(requestingPlayer, playerType);
            SetPlayerScore(requestingPlayer, 0);

            if (bDebugMode)
                Debug.Log("GameMode.RpcRegisterPlayer: assigned 'PlayerType' " + playerType + " to player " +
                          requestingPlayer.NickName);

            _photonView.RPC(nameof(RpcSpawnPlayerCharacter), requestingPlayer, playerType);
        }

        private PlayerType GetAvailablePlayerType(Player requestingPlayer)
        {
            var playersInTheRoom = PhotonNetwork.CurrentRoom.PlayerCount;

            if (playersInTheRoom != 1)
            {
                List<PlayerType> availablePlayerTypes = new List<PlayerType>();

                foreach (var player in PhotonNetwork.CurrentRoom.Players)
                {
                    if (player.Value.ActorNumber == requestingPlayer.ActorNumber) continue;

                    if (player.Value.CustomProperties.TryGetValue(PlayerTypeKey, out var index))
                    {
                        availablePlayerTypes.Add((PlayerType)index);
                    }
                    else
                    {
                        Debug.LogError(
                            "There is a player in the room without a PlayerType custom property. This should not happen." +
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
                return PlayerType.Player1;
            }
        }

        public static Color GetPlayerColorBasedOnPlayerType(Player player)
        {
            if (player == null)
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
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsConnectedAndReady) return;

            player.SetCustomProperties(new Hashtable { { PlayerTypeKey, playerType } });
        }

        [PunRPC]
        private void RpcSpawnPlayerCharacter(PlayerType playerType)
        {
            if (bDebugMode)
                Debug.Log("GameMode.RpcSpawnPlayerCharacter: received player type " + playerType + " for player " +
                          PhotonNetwork.LocalPlayer.NickName);

            var playerCharacter = PhotonNetwork
                .Instantiate(_playerCharacters[playerType].name, _playerSpawnPosition, Quaternion.identity)
                .GetComponent<PlayerCharacter>();
            PlayerController.Local.Possess(playerCharacter);

            OnPlayerCharacterSpawned?.Invoke(PhotonNetwork.LocalPlayer, playerCharacter);

            SetPlayerCharactersAlive(_playerCharactersAlive + 1);

            playerCharacter.OnPlayerDeath += OnPlayerDestroyed;
        }

        private void SetPlayerCharactersAlive(int count)
        {
            _playerCharactersAlive = count;
            _photonView.RPC(nameof(RpcReplicatePlayerCharactersAlive), RpcTarget.All, _playerCharactersAlive);
        }

        [PunRPC]
        private void RpcReplicatePlayerCharactersAlive(int count)
        {
            _playerCharactersAlive = count;
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            photonView.RPC(nameof(RpcReplicateWaveStatus), newPlayer, _currentWave, _hordesSpawnedInCurrentWave);

            photonView.RPC(nameof(RpcReplicatePlayerCharactersAlive), newPlayer, _playerCharactersAlive);
        }

        [PunRPC]
        private void RpcReplicateWaveStatus(int waveNumber, int hordesSpawned)
        {
            _currentWave = waveNumber;
            _hordesSpawnedInCurrentWave = hordesSpawned;
        }

        public void StartWave()
        {
            if (!PhotonNetwork.IsMasterClient || _currentGameState != GameState.WaitingForPlayers) return;

            PhotonNetwork.CurrentRoom.IsOpen = false;

            StartCoroutine(StartWaveCountdown());
        }

        private IEnumerator StartWaveCountdown()
        {
            _gameCountdown = startGameDelay + 1;

            OnGameCooldownChanged?.Invoke(_gameCountdown);
            while (_gameCountdown > -1)
            {
                photonView.RPC(nameof(RpcReplicateGameCooldown), RpcTarget.All, --_gameCountdown);
                yield return new WaitForSeconds(1.0f);
            }

            SpawnHorde();
        }

        [PunRPC]
        private void RpcReplicateGameCooldown(int countdown)
        {
            _gameCountdown = countdown;

            if (_gameCountdown > -1)
            {
                _currentGameState = GameState.Countdown;
                OnGameCooldownChanged?.Invoke(_gameCountdown);

                if (bDebugMode)
                    Debug.Log($"GameMode.StartGameContdown: {_gameCountdown}");

                return;
            }

            if (bDebugMode)
                Debug.Log("GameMode.StartGameContdown: Game Started");

            _currentGameState = GameState.InProgress;

            WaveStartedEvent?.Invoke(_currentWave);

            InvokePickupSpawn();
            InvokeDengueSourceSpawn();
        }

        private void SpawnHorde()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            _mosquitosHordeInTheScene = PhotonNetwork
                .InstantiateRoomObject(mosquitosHordePrefab.name, mosquitosHordePrefab.transform.position,
                    Quaternion.identity).GetComponent<MosquitosHorde>();
            _photonView.RPC(nameof(RpcSubscribeToMosquitosHordeEvents), RpcTarget.All);
        }

        [PunRPC]
        private void RpcSubscribeToMosquitosHordeEvents()
        {
            if (!_mosquitosHordeInTheScene)
                _mosquitosHordeInTheScene = FindAnyObjectByType<MosquitosHorde>();

            _mosquitosHordeInTheScene.AllMosquitosDestroyedEvent += HordeKilled;
            _mosquitosHordeInTheScene.OnMosquitoKilled += MosquitoKilled;
        }

        private void MosquitoKilled(int playerID, int points)
        {
            AddScoreToPlayer(playerID, points);
        }

        private void OnDengueSourceDestroyed(DengueSource dengueSource, int playerID, int points)
        {
            dengueSource.OnDengueSourceDestroyed -= OnDengueSourceDestroyed;
            _dengueSourcesInScene--;

            AddScoreToPlayer(playerID, points);

            //restart dengue source spawn timer
            CancelInvoke(nameof(SpawnDengueSource));
            InvokeDengueSourceSpawn();
        }

        private void AddScoreToPlayer(int playerID, int points)
        {
            if (!PhotonNetwork.IsMasterClient || playerID == -1) return;

            if (bDebugMode)
                Debug.Log($"GameMode.MosquitoKilled: Player {playerID} killed an invader for {points} points");

            Player player = PhotonNetwork.CurrentRoom.GetPlayer(playerID);
            if (player == null) return;

            var playerScore = GetPlayerScore(player) + points;
            SetPlayerScore(player, playerScore);
        }

        public static int GetPlayerScore(Player player)
        {
            if (player.CustomProperties.TryGetValue(PlayerScoreKey, out var index))
            {
                return (int)index;
            }
            Debug.LogError("GameMode.GetPlayerScore: Player " + player.NickName +
                           " does not have a PlayerScore custom property. This should not happen.");
            return 0;
        }

        private void SetPlayerScore(Player player, int score)
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsConnectedAndReady) return;

            player.SetCustomProperties(new Hashtable { { PlayerScoreKey, score } });
        }

        private void HordeKilled()
        {
            if (bDebugMode)
                Debug.Log("GameMode.HordeKilled");

            _hordesSpawnedInCurrentWave++;
            OnHordeProgressChanged?.Invoke(HordeProgressPercentage);

            if (_hordesSpawnedInCurrentWave >= hordesPerWave)
            {
                _hordesSpawnedInCurrentWave = 0;
                WaveCompleted();
            }
            else
            {
                Invoke(nameof(SpawnHorde), intervalBetweenHordes);
            }
        }

        private void WaveCompleted()
        {
            if (bDebugMode)
                Debug.Log("GameMode.WaveCompleted");

            if (_currentLives <= 0)
            {
                _currentLives = startingLives;

                PlayerType playerType = GetPlayerType(PhotonNetwork.LocalPlayer);

                RpcSpawnPlayerCharacter(playerType);
            }


            WaveCompletedEvent?.Invoke(_currentWave);

            _currentWave++;

            PhotonNetwork.CurrentRoom.IsOpen = true;

            _currentGameState = GameState.WaitingForPlayers;

            CancelInvoke(nameof(SpawnPickup));
            CancelInvoke(nameof(SpawnDengueSource));
            photonView.RPC(nameof(RpcReplicateUnsubscribeFromMosquitosHordeEvents), RpcTarget.All);
        }

        [PunRPC]
        private void RpcReplicateUnsubscribeFromMosquitosHordeEvents()
        {
            _mosquitosHordeInTheScene.AllMosquitosDestroyedEvent -= HordeKilled;
            _mosquitosHordeInTheScene.OnMosquitoKilled -= MosquitoKilled;
        }

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            // if the changed property is the PlayerScoreKey, invoke the OnScoreChanged event
            if (changedProps.ContainsKey(PlayerScoreKey))
            {
                OnScoreChanged?.Invoke();
            }
        }

        private void OnMosquitosReachedLimit()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            if (bDebugMode)
                Debug.Log("GameMode.OnInvadersReachedLimit");

            _photonView.RPC(nameof(RpcReplicateMosquitosReachedLimit), RpcTarget.All);
        }

        [PunRPC]
        private void RpcReplicateMosquitosReachedLimit()
        {
            _mosquitosHordeInTheScene.OnMosquitoKilled -= MosquitoKilled;
            _mosquitosHordeInTheScene.AllMosquitosDestroyedEvent -= WaveCompleted;
            MosquitosLimit.OnMosquitosReachedLimit -= OnMosquitosReachedLimit;

            OnPlayerCharacterSpawned = null;

            _currentLives = 0;
            _currentGameState = GameState.GameOver;

            if (!PhotonNetwork.IsMasterClient) return;

            _photonView.RPC(nameof(RpcGameOver), RpcTarget.All, GameOverReason.MosquitosReachedLimit);
        }

        private void OnPlayerDestroyed(PlayerCharacter playerCharacterDestroyed)
        {
            if (!bIsRunning) return;

            if (bDebugMode)
                Debug.Log("GameMode.OnPlayerDestroyed");

            playerCharacterDestroyed.OnPlayerDeath -= OnPlayerDestroyed;

            PlayerController.Local.UnPossess();

            _currentLives--;

            SetPlayerCharactersAlive(_playerCharactersAlive - 1);

            OnLivesChanged?.Invoke(_currentLives);

            if (PhotonNetwork.CurrentRoom.PlayerCount == 1 && _mosquitosHordeInTheScene)
                _mosquitosHordeInTheScene.stopMovement = true;

            if (_playerCharactersAlive <= 0 && _currentLives <= 0 && _currentGameState != GameState.GameOver)
            {
                _photonView.RPC(nameof(RpcGameOver), RpcTarget.All, GameOverReason.AllPlayersDied);
                return;
            }

            StartCoroutine(PlayerDeathCooldown());
        }

        [PunRPC]
        private void RpcGameOver(GameOverReason reason)
        {
            _currentGameState = GameState.GameOver;

            if (bDebugMode)
                Debug.Log("General GameOver. Closing room.");

            if (PhotonNetwork.CurrentRoom.IsOpen)
                PhotonNetwork.CurrentRoom.IsOpen = false;

            _currentLives = 0;

            StopAllCoroutines();
            CancelInvoke(nameof(SpawnPickup));

            GameOverEvent?.Invoke(reason);
        }

        private IEnumerator PlayerDeathCooldown()
        {
            if (_currentLives <= 0)
                yield break;

            yield return new WaitForSeconds(playerDeathCooldown);

            if (PhotonNetwork.CurrentRoom.PlayerCount == 1 && _mosquitosHordeInTheScene)
                _mosquitosHordeInTheScene.stopMovement = false;

            PlayerType playerType = PlayerType.Player1;
            if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PlayerTypeKey, out var index))
                playerType = (PlayerType)index;

            RpcSpawnPlayerCharacter(playerType);
        }

        public void AddExtraLife()
        {
            _currentLives++;
            OnLivesChanged?.Invoke(_currentLives);
        }

        private void InvokePickupSpawn()
        {
            PickupSpawnSettings spawnSettings = GetCurrentPickupSpawnSettings();

            Invoke(nameof(SpawnPickup),
                UnityEngine.Random.Range(spawnSettings.MinSpawnInterval, spawnSettings.MaxSpawnInterval));
        }

        private PickupSpawnSettings GetCurrentPickupSpawnSettings()
        {
            switch (_playerCharactersAlive)
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
                var randomPickupPrefab = ChooseARandomPickup();

                Vector3 randomPosition;
                do
                {
                    var randomX =
                        UnityEngine.Random.Range(pickupsSpawnArea.bounds.min.x, pickupsSpawnArea.bounds.max.x);
                    var randomY =
                        UnityEngine.Random.Range(pickupsSpawnArea.bounds.min.y, pickupsSpawnArea.bounds.max.y);
                    randomPosition = new Vector3(randomX, randomY, 0f);
                } while (CheckIfPlayerIsAround(randomPosition, radiusCheckPlayerPickup));

                PhotonNetwork.InstantiateRoomObject(randomPickupPrefab.name, randomPosition, Quaternion.identity);

                _lastPickupTypeSpawned = randomPickupPrefab.Type;
            }

            InvokePickupSpawn();
        }

        private Pickup ChooseARandomPickup(Pickup.PickupType[] excludedPickupTypes = null)
        {
            Pickup chosenPickupPrefab;
            bool isOfExcludedType;

            do
            {
                var randomPickupIndex = UnityEngine.Random.Range(0, pickupPrefabs.Length);
                chosenPickupPrefab = pickupPrefabs[randomPickupIndex];
                isOfExcludedType = excludedPickupTypes != null && excludedPickupTypes.Contains(chosenPickupPrefab.Type);
            } while (chosenPickupPrefab.Type == _lastPickupTypeSpawned || isOfExcludedType ||
                     !CanSpawnPickup(chosenPickupPrefab.Type));

            return chosenPickupPrefab;
        }

        private bool CanSpawnPickup(Pickup.PickupType pickupType)
        {
            if (pickupType == Pickup.PickupType._1UP)
            {
                var spawnSettings = GetCurrentPickupSpawnSettings();
                var chance = UnityEngine.Random.Range(0, 100);
                return chance < spawnSettings.OneUpSpawnChance;
            }

            if (pickupType == Pickup.PickupType.Toolbox)
            {
                if (BarriersManager.IsAnyBarrierDestroyed())
                {
                    var spawnSettings = GetCurrentPickupSpawnSettings();
                    var chance = UnityEngine.Random.Range(0, 100);
                    return chance < spawnSettings.ToolboxSpawnChance;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        private void InvokeDengueSourceSpawn()
        {
            DengueSourceSpawnSettings spawnSettings = GetCurrentDengueSourceSpawnSettings();

            Invoke(nameof(SpawnDengueSource),
                UnityEngine.Random.Range(spawnSettings.MinSpawnInterval, spawnSettings.MaxSpawnInterval));
        }

        private DengueSourceSpawnSettings GetCurrentDengueSourceSpawnSettings()
        {
            switch (_playerCharactersAlive)
            {
                case 1:
                    return dengueSourceSpawnSettingsWith1CharacterAlive;
                case 2:
                    return dengueSourceSpawnSettingsWith2CharactersAlive;
                case 3:
                    return dengueSourceSpawnSettingsWith3CharactersAlive;
                default:
                    return dengueSourceSpawnSettingsWith1CharacterAlive;
            }
        }

        private void SpawnDengueSource()
        {
            if (_dengueSourcesInScene >= GetCurrentDengueSourceSpawnSettings().MaxDengueSourcesInScene)
            {
                InvokeDengueSourceSpawn();
                return;
            }

            DengueSourceSpawnSettings spawnSettings = GetCurrentDengueSourceSpawnSettings();
            if (PhotonNetwork.IsMasterClient)
            {
                Vector3 randomPosition;

                if (bDebugMode)
                    Debug.Log("GameMode.SpawnDengueSource: Finding random position for dengue source spawn.");
                do
                {
                    var randomX = UnityEngine.Random.Range(dengueSourceSpawnArea.bounds.min.x,
                        dengueSourceSpawnArea.bounds.max.x);
                    var randomY = UnityEngine.Random.Range(dengueSourceSpawnArea.bounds.min.y,
                        dengueSourceSpawnArea.bounds.max.y);
                    randomPosition = new Vector3(randomX, randomY, 0f);
                } while (CheckIfPlayerIsAround(randomPosition, radiusCheckPlayerDengueSource));

                if (bDebugMode)
                    Debug.Log(
                        $"GameMode.SpawnDengueSource: Found valid position for dengue source spawn: {randomPosition}");

                int randomIndex = UnityEngine.Random.Range(0, dengueSourcePrefabs.Length);

                DengueSource dengueSource = PhotonNetwork
                    .InstantiateRoomObject(dengueSourcePrefabs[randomIndex].name, randomPosition, Quaternion.identity)
                    .GetComponent<DengueSource>();
                _dengueSourcesInScene++;

                dengueSource.OnDengueSourceDestroyed += OnDengueSourceDestroyed;
            }

            InvokeDengueSourceSpawn();
        }

        private bool CheckIfPlayerIsAround(Vector3 position, float checkRadius)
        {
            Collider2D[] colliders = Physics2D.OverlapCircleAll(position, checkRadius, playerLayerMask);

            bool isPlayerAround = colliders.Length > 0;

            if (bDebugMode)
            {
                _debugCheckPlayerAround.Add(new DebugPlayerCheck
                {
                    pos = position,
                    checkRadius = checkRadius,
                    isPlayerAround = colliders.Length > 0,
                    timeLimit = Time.time + playerAroundCheckDebugTime
                });
            }

            return isPlayerAround;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            MosquitosLimit.OnMosquitosReachedLimit -= OnMosquitosReachedLimit;

            if (_mosquitosHordeInTheScene == null) return;

            _mosquitosHordeInTheScene.AllMosquitosDestroyedEvent -= WaveCompleted;
            _mosquitosHordeInTheScene.OnMosquitoKilled -= MosquitoKilled;
        }

        private void OnDrawGizmos()
        {
            if (!bDebugMode || _debugCheckPlayerAround == null || _debugCheckPlayerAround.Count == 0) return;

            _debugCheckPlayerAround.RemoveAll(check => Time.time > check.timeLimit);

            for (var i = 0; i < _debugCheckPlayerAround.Count; i++)
            {
                Gizmos.color = _debugCheckPlayerAround[i].isPlayerAround
                    ? colorWhenPlayerAround
                    : colorWhenPlayerNotAround;
                Gizmos.DrawWireSphere(_debugCheckPlayerAround[i].pos, _debugCheckPlayerAround[i].checkRadius);
            }
        }
    }
}