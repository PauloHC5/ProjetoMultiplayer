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
        [Header("Game Mode")]
        [SerializeField] private int startGameDelay = 3;
        [SerializeField] private int startingLives = 3;
        [SerializeField] private PlayerController playerControllerPrefab;
        [SerializeField] private PlayerCharacter[] playerCharacterPrefabs = new PlayerCharacter[3];
        [SerializeField] private Invaders invadersPrefab;
        
        [Header("Debug")]
        public bool bDebugMode = false;
        
        private PhotonView _photonView;
        private int _gameCountdown;
        private int _currentLives;
        private Invaders _invaders;
        private Dictionary<PlayerType, PlayerCharacter> _playerCharacters = new Dictionary<PlayerType, PlayerCharacter>();
        private readonly Vector3 _playerSpawnPosition = new Vector3(0, -13f, 0);
        private bool _bGameCountdownRunning = false;
        
        public static readonly string PlayerTypeKey = "AvailablePlayerTypes";
        public static readonly string PlayerScoreKey = "PlayerScore";
        public static readonly string PlayerCharactersAliveKey = "PlayerCharactersAlive";
        
        public int StartingLives => startingLives;
        public int StartGameDelay => startGameDelay;
        
        public static Action<int> OnGameCooldownChanged;
        public static Action<int> OnLivesChanged;
        public static Action<int, int> OnInvaderKilled;
        public static Action OnScoreChanged;
        public static Action GameStartedEvent;
        public static Action<int> GameOverEvent;
        public static Action<Player, PlayerCharacter> OnPlayerCharacterSpawned;
        public static Action<Player> OnPlayerLeft;
        public static Action RoomReady;

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
            
            OnInvaderKilled += InvaderKilled;
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

            playerCharacter.OnPlayerDestroyed += OnPlayerDestroyed;
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

            photonView.RPC(nameof(RpcReplicateGameCooldown), newPlayer, _gameCountdown);
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            OnPlayerLeft?.Invoke(otherPlayer);
            
            // If countdown is running and the master client leaves, the new master client should start the countdown again
            if (_bGameCountdownRunning && PhotonNetwork.IsMasterClient)
                StartCoroutine(StartGameCountdown());
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

            _invaders = PhotonNetwork.InstantiateRoomObject(invadersPrefab.name, invadersPrefab.transform.position, Quaternion.identity).GetComponent<Invaders>();
            _photonView.RPC(nameof(RpcSubscribeToNewInvadersEvents), RpcTarget.All);
            
        }
        
        [PunRPC]
        private void RpcSubscribeToNewInvadersEvents()
        {
            if(!_invaders)
                _invaders = FindAnyObjectByType<Invaders>();
            
            _invaders.OnInvadersDestroyed += WaveCompleted;
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
            
            GameStartedEvent?.Invoke();            
        }
        
        private void InvaderKilled(int playerID, int points)
        {
            if(!PhotonNetwork.IsMasterClient) return;
            
            Debug.Log($"GameMode.InvaderKilled: Player {playerID} killed an invader for {points} points");
            
            Player player = PhotonNetwork.CurrentRoom.GetPlayer(playerID);
            if (player == null) return;
            
            var playerScore = GetPlayerScore(player) + points;
            SetPlayerScore(player, playerScore);
        }

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            // if the changed property is the PlayerScoreKey, invoke the OnScoreChanged event
            if (changedProps.ContainsKey(PlayerScoreKey))
            {
                OnScoreChanged?.Invoke();
            }
        }

        private void WaveCompleted()
        {
            if(bDebugMode)
                Debug.Log("GameMode.WaveCompleted");
            
            _invaders.OnInvadersDestroyed -= WaveCompleted;
            
            StartCoroutine(StartGameCountdown());
        }
        
        private void OnInvadersReachedLimit()
        {
            if(bDebugMode)
                Debug.Log("GameMode.OnInvadersReachedLimit");
            
            _invaders.OnInvadersDestroyed -= WaveCompleted;
            
            OnPlayerCharacterSpawned = null;
         
            _currentLives = 0;
            
            GameOver(0);
        }
        
        private void OnPlayerDestroyed(PlayerCharacter playerCharacterDestroyed)
        {            
            if(bDebugMode)
                Debug.Log("GameMode.OnPlayerDestroyed");
            
            playerCharacterDestroyed.OnPlayerDestroyed -= OnPlayerDestroyed;
            
            _currentLives--;

            var playerCharactersAlive = GetOrInitPlayerCharactersAlive() - 1;
            
            SetPlayerCharactersAlive(playerCharactersAlive);
            
            OnLivesChanged?.Invoke(_currentLives);

            if(PhotonNetwork.CurrentRoom.PlayerCount == 1 && _invaders)
                _invaders.stopMovement = true;

            if (_currentLives <= 0)
            {
                GameOver(playerCharactersAlive);
                return;
            }

            StartCoroutine(PlayerDestroyedCooldown());
        }

        private void GameOver(int playerCharactersAlive)
        {
            if(bDebugMode)
                Debug.Log("GameMode.OnPlayerDestroyed: Game Over");

            if (playerCharactersAlive <= 0)
            {
                if(bDebugMode)
                    Debug.Log("GameMode.OnPlayerDestroyed: No more player characters alive, closing room.");
                   
                if(PhotonNetwork.CurrentRoom.IsOpen)
                    PhotonNetwork.CurrentRoom.IsOpen = false;
                    
                StopAllCoroutines();
            }
                
            GameOverEvent?.Invoke(playerCharactersAlive);
        }
        
        private IEnumerator PlayerDestroyedCooldown()
        {
            yield return new WaitForSeconds(2.0f);                       

            if (PhotonNetwork.CurrentRoom.PlayerCount == 1 && _invaders)
                _invaders.stopMovement = false;

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
    }
}