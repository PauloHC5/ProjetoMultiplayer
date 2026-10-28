using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Random = UnityEngine.Random;

namespace _Game.Scripts.Enemies
{
    /// <summary>
    /// Arquitetura de rede:
    ///  - DECIDE (só a autoridade: MasterClient, ou qualquer um se estiver offline): DirectorRoutine
    ///  - ESPALHA (RPC RpcTarget.All para efeito ao vivo + Room Property para late-joiners): Broadcast
    ///  - APLICA (todos os clients, inclusive a autoridade): ApplyState
    /// Nenhum client além da autoridade sorteia duração/índice, então ninguém diverge.
    /// </summary>
    public class MosquitoQueen : MosquitoBase, IInRoomCallbacks
    {
        [Header("Queen Movement Settings")] 
        [SerializeField] private MosquitoZigZagMovementSettings queenStandardZigZagSettings;
        [SerializeField] private MosquitoZigZagMovementSettings queenPrepareToAttackZigZagSettings;
        [SerializeField] private QueenBehaviour[] queenBehaviours;
        [SerializeField] private float minStandardBehaviourDuration = 5f;
        [SerializeField] private float maxStandardBehaviourDuration = 10f;
        [SerializeField] private float preparationTime = 3f;
        public bool bEnableBehaviour = true;
        [SerializeField] private Vector2 retreatPosition = new Vector2(0f, 10f);
        [SerializeField] private Vector2 hordeSpawnPosition = new Vector2(0f, 0f);
        
        [Header("Queen Mosquitos Settings")]
        [SerializeField] private LissajouCurveSettings[] mosquitosPatrolMovementSettings;
        [SerializeField] private int mosquitosPatrolAmount = 5;

        [Header("Queen Health Settings")] 
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int damagePerHit = 10;
        [SerializeField] private Slider healthBarSlider;
        [SerializeField] private TextMeshProUGUI healthPercentageText;

        [Header("Prefabs")] 
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Mosquito mosquitoPrefab;

        [Header("Projectile Settings")]
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField] private float projectileSpawnRate = 1f;
        [SerializeField] private int projectilesPerShot = 3;
        [SerializeField] private float projectileSpreadAngle = 30f;

        // ---------------------------------------------------------------------------------
        // Estado replicado
        // ---------------------------------------------------------------------------------

        public enum QueenPhase : byte
        {
            MovingToCenter = 0, // indo até o centro da cena
            Standard = 1, // comportamento base (queenBehaviours[0])
            Preparing = 2, // avisando que vai atacar (zigzag de preparação)
            Attack = 3, // comportamento de ataque (queenBehaviours[1..])
            Retreat = 4 // indo embora
        }

        public readonly struct QueenState
        {
            public readonly QueenPhase Phase;
            public readonly int BehaviourIndex;
            public readonly int MovementIndex;
            public readonly int MosquitoPatrolMovementIndex;

            public QueenState(QueenPhase phase, int behaviourIndex, int movementIndex, int mosquitoPatrolMovementIndex = -1)
            {
                Phase = phase;
                BehaviourIndex = behaviourIndex;
                MovementIndex = movementIndex;
                MosquitoPatrolMovementIndex = mosquitoPatrolMovementIndex;
            }

            public int[] ToArray() => new[] { (int)Phase, BehaviourIndex, MovementIndex, MosquitoPatrolMovementIndex };

            public static bool TryFromArray(object raw, out QueenState state)
            {
                if (raw is int[] data && data.Length == 5)
                {
                    state = new QueenState((QueenPhase)data[0], data[1], data[2], data[3]);
                    return true;
                }

                state = default;
                return false;
            }

            public override string ToString() => $"{Phase} (behaviour {BehaviourIndex}, movement {MovementIndex})";
        }

        private PhotonView _photonView;
        private QueenState _state; // espelho local do último estado aplicado
        private QueenPhase _appliedPhase = QueenPhase.MovingToCenter;
        private int _lastAttackIndex; // último comportamento de ataque usado (para o rodízio)
        private Coroutine _directorCoroutine;
        private Coroutine _retreatCoroutine;
        private int _currentHealth;
        private bool _bHealthBelow75Triggered = false;
        private bool _bHealthBelow50Triggered = false;
        private TrailRenderer _queenTrailRenderer;
        private Mosquito[] _mosquitos;
        private int _lastMosquitoPatrolMovementIndex = -1;

        public float StandardBehaviourDuration =>
            Random.Range(minStandardBehaviourDuration, maxStandardBehaviourDuration);

        public int CurrentHealthPercentage => Mathf.RoundToInt((float)_currentHealth / maxHealth * 100f);

        // ---------------------------------------------------------------------------------
        // Helpers de rede (funcionam online, offline mode e sem Photon nenhum)
        // ---------------------------------------------------------------------------------

        private string RoomStateKey => $"Queen_{_photonView.ViewID}";

        public Action MosquitoQueenDeathEvent;

        protected override void Awake()
        {
            base.Awake();

            _photonView = GetComponent<PhotonView>();
            _queenTrailRenderer = GetComponent<TrailRenderer>();

            _currentHealth = maxHealth;

            PhotonNetwork.AddCallbackTarget(this);
        }

        protected override void Start()
        {
            base.Start();

            StartCoroutine(BootstrapRoutine());
        }

        protected override void Update()
        {
            base.Update();

#if UNITY_EDITOR

            if (Keyboard.current.kKey.wasPressedThisFrame && Keyboard.current.leftShiftKey.isPressed)
            {
                if (PhotonHandlers.CanUseNetwork && PhotonNetwork.IsMasterClient)
                    _photonView.RPC(nameof(TakeDamage), RpcTarget.All);
                else
                    TakeDamage();
            }

            if(Keyboard.current.lKey.wasPressedThisFrame && Keyboard.current.leftShiftKey.isPressed)
            {
                
            }
#endif
        }

        private void OnDestroy()
        {
            // Limpa o estado da sala pra uma futura rainha não ler lixo.
            if (PhotonHandlers.CanUseNetwork && PhotonNetwork.IsMasterClient)
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { RoomStateKey, null } });

            PhotonNetwork.RemoveCallbackTarget(this);
        }

        public void OnMasterClientSwitched(Player newMasterClient)
        {
            RefreshDirector();
        }

        public void OnPlayerEnteredRoom(Player newPlayer)
        {
        }

        public void OnPlayerLeftRoom(Player otherPlayer)
        {
        }

        public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
        }

        public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
        }

        private IEnumerator BootstrapRoutine()
        {
            // Entrou no meio da luta: pula a intro e assume o estado que já está na sala.
            if (TryReadRoomState(out QueenState saved) && saved.Phase != QueenPhase.MovingToCenter)
            {
                ApplyState(saved);
            }

            if (bEnableBehaviour)
                RefreshDirector();

            yield return null;
        }

        /// <summary>
        /// Se o spawner passar PhotonNetwork.Time em InstantiationData[0], todos os clients começam a intro
        /// do mesmo instante. Sem isso, cai no comportamento antigo (hora local do Start).
        /// </summary>
        private float GetIntroStartTime()
        {
            object[] data = _photonView != null ? _photonView.InstantiationData : null;
            if (data != null && data.Length > 0 && data[0] is double spawnTime)
                return (float)spawnTime;

            return PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode ? (float)PhotonNetwork.Time : Time.time;
        }

        // ---------------------------------------------------------------------------------
        // Autoridade: liga/desliga o "diretor" conforme quem é o MasterClient
        // (cobre o início e a migração de master sem precisar de callbacks)
        // ---------------------------------------------------------------------------------

        private void RefreshDirector()
        {
            if (!bEnableBehaviour) return;

            if (PhotonHandlers.IsAuthority && _directorCoroutine == null)
                _directorCoroutine = StartCoroutine(DirectorRoutine());
            else if (!PhotonHandlers.IsAuthority && _directorCoroutine != null)
            {
                StopCoroutine(_directorCoroutine);
                _directorCoroutine = null;
            }
        }

        // ---------------------------------------------------------------------------------
        // Diretor (só roda na autoridade): Standard -> Preparing -> Attack -> Standard ...
        // Um novo master retoma da fase em que o anterior parou (_state é espelhado em todos).
        // ---------------------------------------------------------------------------------

        private IEnumerator DirectorRoutine()
        {
            if (queenBehaviours == null || queenBehaviours.Length == 0)
            {
                Debug.LogWarning("[MosquitoQueen] QueenBehaviours is not assigned.");
                _directorCoroutine = null;
                yield break;
            }

            bool hasAttackBehaviours = queenBehaviours.Length > 1;
            bool resumingAttack = _state.Phase == QueenPhase.Attack;
            QueenPhase phase = _state.Phase;

            while (true)
            {
                if (DoRetreat())
                    phase = QueenPhase.Retreat;

                switch (phase)
                {
                    case QueenPhase.MovingToCenter:
                        Broadcast(new QueenState(QueenPhase.MovingToCenter, 0, _state.MovementIndex));
                        yield return new WaitUntil(() => !IsMoving);
                        phase = QueenPhase.Standard;
                        break;

                    case QueenPhase.Standard:
                        yield return RunBehaviour(0, QueenPhase.Standard, StandardBehaviourDuration);
                        phase = hasAttackBehaviours ? QueenPhase.Preparing : QueenPhase.Standard;
                        break;

                    case QueenPhase.Preparing:
                        Broadcast(new QueenState(QueenPhase.Preparing, 0, _state.MovementIndex));
                        yield return new WaitForSeconds(preparationTime);
                        phase = QueenPhase.Attack;
                        break;

                    case QueenPhase.Attack:
                        int attackIndex = resumingAttack ? _state.BehaviourIndex : NextAttackIndex();
                        resumingAttack = false;

                        yield return RunBehaviour(attackIndex, QueenPhase.Attack,
                            queenBehaviours[attackIndex].BehaviourDuration);
                        phase = QueenPhase.Standard;
                        break;
                    case QueenPhase.Retreat:
                        Broadcast(new QueenState(QueenPhase.Retreat, 0, _state.MovementIndex));
                        yield return new WaitUntil(() => _retreatCoroutine == null);
                        phase = QueenPhase.MovingToCenter;
                        break;
                }
            }
        }

        /// <summary>Executa um comportamento por 'duration' segundos, trocando os movement settings quando configurado.</summary>
        private IEnumerator RunBehaviour(int behaviourIndex, QueenPhase phase, float duration)
        {
            QueenBehaviour behaviour = queenBehaviours[behaviourIndex];
            bool cycleMovements = behaviour.ChangeMovementSettings && behaviour.HasMoreThanOneMovementSettings;
            float remaining = duration;

            do
            {
                // TODO: o argumento aqui é o índice do comportamento (como no script original). Se a intenção
                // era "último movement index usado", troque por _state.MovementIndex.
                int movementIndex = cycleMovements
                    ? behaviour.GetNextMovementSettingsIndex(behaviourIndex)
                    : behaviour.GetRandomMovementSettingsIndex();

                int patrolMovementIndex = -1;

                if (phase == QueenPhase.Standard)
                {
                    patrolMovementIndex =
                        (_lastMosquitoPatrolMovementIndex + 1) % mosquitosPatrolMovementSettings.Length;
                    _lastMosquitoPatrolMovementIndex = patrolMovementIndex;
                }

                Broadcast(new QueenState(phase, behaviourIndex, movementIndex, patrolMovementIndex));

                float wait = cycleMovements
                    ? Mathf.Min(Mathf.Max(0.1f, behaviour.TimeToChangeBehaviour), remaining)
                    : remaining;

                yield return new WaitForSeconds(wait);
                remaining -= wait;
            } while (remaining > 0f);

            if (bDebugLogs)
                Debug.Log($"[MosquitoQueen] Behaviour {behaviourIndex} ended after {duration}s");
        }

        private int NextAttackIndex()
        {
            int next = (_lastAttackIndex + 1) % queenBehaviours.Length;
            return next == 0 ? 1 : next; // pula o comportamento base
        }

        // ---------------------------------------------------------------------------------
        // Replicação: 1 RPC ao vivo (sem buffer) + Room Property como estado autoritativo
        // ---------------------------------------------------------------------------------

        private void Broadcast(QueenState state)
        {
            if (!PhotonHandlers.IsAuthority) return;

            if (bDebugLogs)
                Debug.Log($"[MosquitoQueen] Broadcast -> {state}");

            if (PhotonHandlers.CanUseNetwork)
            {
                // Propriedade primeiro: quem entrar agora lê o estado novo mesmo que perca o RPC.
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { RoomStateKey, state.ToArray() } });
                _photonView.RPC(nameof(RpcApplyState), RpcTarget.All, state);
            }
            else
            {
                ApplyState(state);
            }
        }

        [PunRPC]
        private void RpcApplyState(QueenState newQueenState)
        {
            ApplyState(newQueenState);
        }

        private bool TryReadRoomState(out QueenState state)
        {
            state = default;

            return PhotonHandlers.CanUseNetwork
                   && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomStateKey, out object raw)
                   && QueenState.TryFromArray(raw, out state);
        }

        // ---------------------------------------------------------------------------------
        // Aplicação (todos os clients): só mexe em movimento/visual, nunca decide nada
        // ---------------------------------------------------------------------------------

        private void ApplyState(QueenState state)
        {
            _state = state;
            
            ApplyMosquitosState();

            if (state.Phase == QueenPhase.Attack)
                _lastAttackIndex = state.BehaviourIndex;

            // O zigzag só troca quando a FASE muda (os ciclos de movement dentro da mesma fase não o reiniciam).
            if (state.Phase != _appliedPhase)
            {
                _appliedPhase = state.Phase;

                switch (state.Phase)
                {
                    case QueenPhase.MovingToCenter:
                        zigZagMovement.SwitchMovementSettings(queenStandardZigZagSettings);
                        lissajousCurve.SwitchCurveSettings(null);
                        break;
                    case QueenPhase.Standard:
                        zigZagMovement.SwitchMovementSettings(queenStandardZigZagSettings);
                        InvokeRepeating(nameof(InvokeSpawnProjectile), projectileSpawnRate, projectileSpawnRate);
                        _queenTrailRenderer.emitting = false;
                        break;
                    case QueenPhase.Preparing:
                        zigZagMovement.SwitchMovementSettings(queenPrepareToAttackZigZagSettings);
                        _queenTrailRenderer.emitting = false;
                        break;
                    case QueenPhase.Attack:
                        zigZagMovement.SwitchMovementSettings(null);
                        CancelInvoke(nameof(InvokeSpawnProjectile));
                        _queenTrailRenderer.emitting = true;
                        break;
                    case QueenPhase.Retreat:
                        lissajousCurve.SwitchCurveSettings(null);
                        CancelInvoke(nameof(InvokeSpawnProjectile));
                        break;
                }
            }

            if (state.Phase == QueenPhase.MovingToCenter)
                GoToNewPosition(Vector2.zero);

            if (state.Phase == QueenPhase.Retreat)
                _retreatCoroutine = StartCoroutine(RetreatRoutine());

            // Na preparação o movimento (Lissajous) continua o mesmo, como no script original.
            if (state.Phase == QueenPhase.Preparing || state.Phase == QueenPhase.MovingToCenter ||
                state.Phase == QueenPhase.Retreat) return;

            LissajouCurveSettings settings =
                queenBehaviours[state.BehaviourIndex].GetMovementSettings(state.MovementIndex);
            if (settings != null)
                lissajousCurve.SwitchCurveSettings(settings);
        }
        
        private void ApplyMosquitosState()
        {
            SpawnMosquitosPatrol();

            for (var i = 0; i < _mosquitos.Length; i++)
            {
                if (_state.MosquitoPatrolMovementIndex == -1)
                {
                    _mosquitos[i].LissajousCurve.SwitchCurveSettings(null);
                    continue;
                }

                _mosquitos[i].LissajousCurve.SwitchCurveSettings(mosquitosPatrolMovementSettings[_state.MosquitoPatrolMovementIndex]);
            }
        }

        private void SpawnMosquitosPatrol()
        {
            bool AllMosquitosKilled = _mosquitos == null || _mosquitos.All(m => m == null || !m.gameObject.activeInHierarchy);
            
            if (_mosquitos == null || _mosquitos.Length == 0)
            {
                List<Mosquito> newMosquitos = _mosquitos != null ? new List<Mosquito>(_mosquitos) : new List<Mosquito>();

                int offsetCount = 0;
                
                while (newMosquitos.Count < mosquitosPatrolAmount)
                {
                    Mosquito newMosquito = Instantiate(mosquitoPrefab, transform);
                    
                    newMosquito.MosquitoZigZagMovement.SwitchMovementSettings(null);

                    var timeOffset = offsetCount;

                    newMosquito.LissajousCurve.timeOffset = timeOffset;
                    
                    offsetCount += 5;
                    
                    newMosquitos.Add(newMosquito);
                }
                
                _mosquitos = newMosquitos.ToArray();
            }
            else if(AllMosquitosKilled)
            {
                for (var i = 0; i < _mosquitos.Length; i++)
                {
                    _mosquitos[i].gameObject.SetActive(true);
                }
            }
            
        }

        private IEnumerator RetreatRoutine()
        {
            GoToNewPosition(hordeSpawnPosition);

            zigZagMovement.SwitchMovementSettings(queenStandardZigZagSettings);

            yield return new WaitForSeconds(preparationTime);

            GoToNewPosition(retreatPosition);

            _queenTrailRenderer.emitting = false;

            if (GameMode.Instance)
            {
                MosquitosHorde mosquitosHorde = GameMode.Instance.QueenSpawnHorde(this);

                if (mosquitosHorde != null)
                {
                    bool allDestroyed = false;

                    Action onAllDestroyed = () => { allDestroyed = true; };

                    mosquitosHorde.AllMosquitosDestroyedEvent += onAllDestroyed;

                    yield return new WaitUntil(() => allDestroyed);

                    mosquitosHorde.AllMosquitosDestroyedEvent -= onAllDestroyed;
                }
            }

            _retreatCoroutine = null;
            yield return null;
        }

        private void InvokeSpawnProjectile()
        {
            if (PhotonHandlers.CanUseNetwork)
            {
                if(!PhotonHandlers.IsAuthority) return;
                
                _photonView.RPC(nameof(RpcSpawnProjectile), RpcTarget.Others, (Vector2)projectileSpawnPoint.position, Rigidbody2D.linearVelocity);
                
                SpawnProjectile(projectileSpawnPoint.position);

                if (bDebugLogs)
                    Debug.Log("[MosquitoQueen] SpawnProjectile RPC sent to all");
            }
            else
            {
                SpawnProjectile(projectileSpawnPoint.position);
            }
        }

        [PunRPC]
        private void RpcSpawnProjectile(Vector2 projectileSpawnPosition, Vector2 queenVelocity, PhotonMessageInfo info)
        {
            float lag = Mathf.Max(0f, (float)(PhotonNetwork.Time - info.SentServerTime));
            
            Vector3 fixedPosition = projectileSpawnPosition + (Vector2)queenVelocity * lag;
            
            SpawnProjectile(fixedPosition);
        }

        private void SpawnProjectile(Vector3 spawnPosition)
        {
            if (projectilePrefab == null || projectileSpawnPoint == null) return;

            for (int i = 0; i < projectilesPerShot; i++)
            {
                float zRotation = i switch
                {
                    0 => 0f // Center
                    ,
                    1 => -projectileSpreadAngle // Left
                    ,
                    2 => projectileSpreadAngle // Right
                    ,
                    _ => 0f
                };

                Quaternion rotation = Quaternion.Euler(0f, 0f, zRotation);

                Projectile projectile = Instantiate(projectilePrefab, spawnPosition, rotation);
                projectile.Init(MosquitosInstigatorId, Random.Range(0, int.MaxValue), pierceThroughEnemies: false,
                    doubleSpeed: false);

                if (bDebugLogs)
                    Debug.Log($"[MosquitoQueen] Spawned projectile {i + 1}/{projectilesPerShot}");
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.layer == ProjectilesLayer)
            {
                if (PhotonHandlers.CanUseNetwork)
                    _photonView.RPC(nameof(TakeDamage), RpcTarget.All);
                else
                    TakeDamage();
            }

            if (other.gameObject.layer == BarrierLayer)
            {
                BarriersManager.Instance.DamageBarrier(other.gameObject);
            }
        }

        [PunRPC]
        private void TakeDamage()
        {
            _currentHealth -= damagePerHit;
            healthBarSlider.value = (float)_currentHealth / maxHealth;
            int currentHealthPercentage = CurrentHealthPercentage;
            healthPercentageText.text = $"{currentHealthPercentage}%";
            
            if (_currentHealth > 0) return;
            
            if (PhotonHandlers.CanUseNetwork)
            {
                if (PhotonHandlers.IsAuthority)
                {
                    PhotonNetwork.Destroy(gameObject);
                
                    if (bDebugLogs)
                        Debug.Log("[MosquitoQueen] Destroyed by MasterClient");
                }
            }
            else
            {
                Destroy(gameObject);
            }
            
            MosquitoQueenDeathEvent?.Invoke();
        }

        private bool DoRetreat()
        {
            if (_bHealthBelow75Triggered && _bHealthBelow50Triggered) return false;

            switch (_currentHealth)
            {
                case > 50 and <= 75:
                    // Trigger some event or change behavior
                    if (_bHealthBelow75Triggered) break;

                    if (bDebugLogs)
                        Debug.Log("[Mosquito Queen] Health is between 50% and 75%");

                    _bHealthBelow75Triggered = true;
                    return true;
                case > 25 and <= 50:
                    // Trigger some event or change behavior
                    if (_bHealthBelow50Triggered) break;

                    if (bDebugLogs)
                        Debug.Log("[Mosquito Queen] Health is below 50%");

                    _bHealthBelow50Triggered = true;
                    return true;
            }

            return false;
        }
    }
}