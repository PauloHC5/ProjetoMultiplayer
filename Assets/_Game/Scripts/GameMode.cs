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
        private int _currentScore;
        private bool _bGameStarted = false;        
        private Dictionary<PlayerType, PlayerCharacter> _playerCharacters = new Dictionary<PlayerType, PlayerCharacter>();

        private readonly Vector3 _playerSpawnPosition = new Vector3(0, -13f, 0);
        private const string PlayerTypeKey = "AvailablePlayerTypes";
        
        public int StartingLives => startingLives;
        public int StartGameDelay => startGameDelay;
        
        public static Action<int> OnGameCooldownChanged;
        public static Action<int> OnLivesChanged;
        public static Action<int> OnInvaderKilled;
        public static Action<int> OnScoreChanged;
        public static Action OnGameStarted;
        public static Action OnGameEnded;
        public static Action OnWaveCompleted;
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
            
            OnInvaderKilled += AddPoints;

            StartCoroutine(WaitForRoomBeReady());
        }

        private IEnumerator WaitForRoomBeReady()
        {
            yield return new WaitUntil(() => PhotonNetwork.InRoom);
            
            RoomReady?.Invoke();
    
            SpawnPlayerController();
            _photonView.RPC(nameof(RpcRegisterPlayer), RpcTarget.MasterClient,
                PhotonNetwork.LocalPlayer.ActorNumber);
            
            if(!PhotonNetwork.IsMasterClient) yield break;
            
            StartCoroutine(StartGameContdown());
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
            
            requestingPlayer.SetCustomProperties(new Hashtable { { PlayerTypeKey, playerType } });
            
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

        [PunRPC]
        private void RpcSpawnPlayerCharacter(PlayerType playerType)
        {
            if(bDebugMode)
                Debug.Log("GameMode.RpcSpawnPlayerCharacter: received player type " + playerType + " for player " + PhotonNetwork.LocalPlayer.NickName);
            
            var playerCharacter = PhotonNetwork.Instantiate(_playerCharacters[playerType].name, _playerSpawnPosition, Quaternion.identity).GetComponent<PlayerCharacter>();
            PlayerController.Local.Possess(playerCharacter);
            
            OnPlayerCharacterSpawned?.Invoke(PhotonNetwork.LocalPlayer, playerCharacter);

            playerCharacter.OnPlayerDestroyed += OnPlayerDestroyed;
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            photonView.RPC(nameof(RpcReplicateGameCooldown), newPlayer, _gameCountdown);
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            OnPlayerLeft?.Invoke(otherPlayer);
        }

        private IEnumerator StartGameContdown()
        {            
            _gameCountdown = startGameDelay;
            OnGameCooldownChanged?.Invoke(_gameCountdown);
            while (_gameCountdown > -1)
            {                
                photonView.RPC(nameof(RpcReplicateGameCooldown), RpcTarget.All, _gameCountdown);
                yield return new WaitForSeconds(1.0f);
            }

            _invaders = PhotonNetwork.Instantiate(invadersPrefab.name, invadersPrefab.transform.position, Quaternion.identity).GetComponent<Invaders>();
            // _invaders.OnInvadersDestroyed += WaveCompleted;
        }

        [PunRPC]
        private void RpcReplicateGameCooldown(int countdown)
        {
            if(countdown > -1)
            {                
                OnGameCooldownChanged?.Invoke(--_gameCountdown);

                if (bDebugMode)
                    Debug.Log($"GameMode.StartGameContdown: {_gameCountdown}");

                return;
            }

            if (bDebugMode)
                Debug.Log("GameMode.StartGameContdown: Game Started");

            _bGameStarted = true;

            OnGameStarted?.Invoke();            

            
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
            
            StartCoroutine(StartGameContdown());
        }
        
        private void OnPlayerDestroyed()
        {            
            if(bDebugMode)
                Debug.Log("GameMode.OnPlayerDestroyed");
            
            _currentLives--;
            OnLivesChanged?.Invoke(_currentLives);

            if(PhotonNetwork.CurrentRoom.PlayerCount == 1 && _invaders)
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

            if (PhotonNetwork.CurrentRoom.PlayerCount == 1 && _invaders)
                _invaders.enabled = true;

            PlayerType playerType = PlayerType.Player1;
            if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PlayerTypeKey, out var index))
                playerType = (PlayerType)index;

            RpcSpawnPlayerCharacter(playerType);

        }
    }
}