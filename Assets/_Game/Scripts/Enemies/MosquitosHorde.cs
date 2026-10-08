using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using _Game.Scripts;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class MosquitosHorde : MonoBehaviourPunCallbacks
{
    private enum BoundariesSideHit
    {
        Left,
        Right
    }

    [Serializable]
    public struct HordePositionKey : IEquatable<HordePositionKey>
    {
        public int row;
        public int column;

        public HordePositionKey(int row, int column)
        {
            this.row = row;
            this.column = column;
        }

        public bool Equals(HordePositionKey other)
        {
            return row == other.row && column == other.column;
        }

        public override bool Equals(object obj)
        {
            return obj is HordePositionKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(row, column);
        }
    }
    
    [Header("Settings")]
    public Mosquito[] prefabs = new Mosquito[3];
    [SerializeField] public int rows = 5;
    [SerializeField] int columns = 11;
    public float hordeVerticalInterpolationDuration = 0.5f;
    [SerializeField] private AnimationCurve speed;
    [SerializeField] private float missileAttackRate = 1.0f;
    public Projectile missilePrefab;
    public AudioCue killedSfx;
    [SerializeField] private LayerMask laserLayerMask;
    public bool stopMovement = false;
    public Vector2 mosquitoStartPosition = new Vector2(0.0f, 15.0f);
    
    [Header("ZigZag Movement Settings when the horde is at 10%, 7%, 5%, 3% and 1% of mosquitos alive")]
    public MosquitoMovementSettings last10;
    public MosquitoMovementSettings last7;
    public MosquitoMovementSettings last5;
    public MosquitoMovementSettings last3;
    public MosquitoMovementSettings last;
    
    [Header("Debug")]
    public bool bDrawDebugGizmos = false;
    public HordePositionKey debugHordePositionKey;
    
    private Vector3 _direction = Vector3.right;    
    private Mosquito[] _mosquitos;
    private PhotonView _photonView;
    private bool _bRequestedRowAdvance = false;
    private bool _bAlreadySpawnedHordePositions = false;
    private bool _bAlreadySpawnedMosquitos = false;
    private int _shotCounter;
    private BoundariesSideHit _lastBoundariesSideHit;
    public const int MosquitosInstigatorId = 451;
    private Queue<MosquitoType> _mosquitoTypes;
    private Dictionary<HordePositionKey, Vector2> _hordePositions;
    
    public int AmountKilled { get; private set; }
    public int TotalMosquitos => this.rows * this.columns;
    public float PercentKilled => (float)this.AmountKilled / (float)this.TotalMosquitos;
    public int AmountAlive => this.TotalMosquitos - this.AmountKilled;
    
    
    public Action<int, int> OnMosquitoKilled;
    public event Action AllMosquitosDestroyedEvent;

    private void Awake()
    {        
        _mosquitos = new Mosquito[this.rows * this.columns];
        _photonView = GetComponent<PhotonView>();

        if (PhotonNetwork.InRoom)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            _mosquitoTypes = new Queue<MosquitoType>();

            SpawnHordePositions();
            SpawnMosquitoForEachHordePosition(true);
            SubscribeToMosquitosEvents();

            bool[] mosquitosActive = _mosquitos.Select(mosquito => mosquito.gameObject.activeInHierarchy).ToArray();

            int[] mosquitoTypes = _mosquitoTypes.Select(mosquitoType => (int)mosquitoType).ToArray();

            photonView.RPC(nameof(RpcSyncMosquitosState), RpcTarget.Others, mosquitosActive, mosquitoTypes, AmountKilled, true, _direction.x, (int)_lastBoundariesSideHit);
        }
        else
        {
            StartCoroutine(WaitForRoomBeReady());
        }
        
    }

    private IEnumerator WaitForRoomBeReady()
    {
        yield return new WaitUntil(() => PhotonNetwork.InRoom);
        
        yield return new WaitForSeconds(1f); // Wait for a short delay to ensure the room is fully ready;

        if (!PhotonNetwork.IsMasterClient) yield return null;
        
        _mosquitoTypes = new Queue<MosquitoType>();
        
        SpawnHordePositions();
        SpawnMosquitoForEachHordePosition(true);
        SubscribeToMosquitosEvents();
        
        bool[] mosquitosActive = _mosquitos.Select(mosquito => mosquito.gameObject.activeInHierarchy).ToArray();
        
        int[] mosquitoTypes = _mosquitoTypes.Select(mosquitoType => (int)mosquitoType).ToArray();
        
        photonView.RPC(nameof(RpcSyncMosquitosState), RpcTarget.Others, mosquitosActive, mosquitoTypes, AmountKilled);
    }

    private void SubscribeToMosquitosEvents()
    {
        for(int i = 0; i < _mosquitos.Length; i++)
        {
            _mosquitos[i].mosquitoIndex = i;
            _mosquitos[i].OnMosquitoHitBoundaries += MosquitoHitBoundaries;
            _mosquitos[i].OnMosquitoKilled += MosquitoKilled;
        }
    }

    private void SpawnHordePositions()
    {
        if(_bAlreadySpawnedHordePositions) return;
        
        // Make a copy of _mosquitoTypes to avoid modifying the original queue while iterating
        Queue<MosquitoType> mosquitoTypes = new Queue<MosquitoType>(_mosquitoTypes);
        
        for (int row = 0; row < this.rows; row++)
        {
            float width = 4.0f * (this.columns - 1);
            float height = 4.0f * (this.rows - 1);
            Vector2 centering = new Vector2(-width / 2, -height / 2);
            Vector3 rowPosition = new Vector3(centering.x, centering.y + (row * 4.0f), 0.0f);
            
            for(int col = 0; col < this.columns; col++) 
            {                                                
                _hordePositions ??= new Dictionary<HordePositionKey, Vector2>();
                _hordePositions.Add(new HordePositionKey(row, col), new Vector2(rowPosition.x + (col * 4.0f), rowPosition.y));
            }
        }
        
        _bAlreadySpawnedHordePositions = true;
    }
    
    private void SpawnMosquitoForEachHordePosition(bool bInterToPosition)
    {
        if(_bAlreadySpawnedMosquitos) return;
        
        Queue<MosquitoType> mosquitoTypes = new Queue<MosquitoType>(_mosquitoTypes);
        
        for (int i = 0; i < _hordePositions.Count; i++)
        {
            if(!_hordePositions.TryGetValue(new HordePositionKey(i / this.columns, i % this.columns), out Vector2 rowPosition))
            {
                Debug.LogError($"Horde position not found for index {i}");
                continue;
            }

            Mosquito mosquitoPrefab = null;
            
            if (PhotonNetwork.IsMasterClient)
            {
                MosquitoType mosquitoType = RandomizeMosquitoType(_hordePositions.ElementAt(i).Key.row);
                mosquitoPrefab = prefabs[(int)mosquitoType];
                _mosquitoTypes.Enqueue(mosquitoType);
            }
            else
            {
                mosquitoPrefab = prefabs[(int)mosquitoTypes.Dequeue()];
            }
            
            Mosquito mosquito = Instantiate(mosquitoPrefab, transform);
            _mosquitos[i] = mosquito;

            if (bInterToPosition)
            {
                var startingPosition = transform.position + (Vector3)mosquitoStartPosition;

                var mosquitoHordePositionKey = new HordePositionKey(i / this.columns, i % this.columns);
                var mosquitoHordePosition = _hordePositions[mosquitoHordePositionKey];

                mosquito.ChangeMosquitoHordePosition(mosquitoHordePositionKey, mosquitoHordePosition, PhotonNetwork.Time + 0.1f);                
            }
            else
            {
                mosquito.transform.localPosition = _hordePositions.ElementAt(i).Value;
            }
        }
        
        _bAlreadySpawnedMosquitos = true;
    }
    private MosquitoType RandomizeMosquitoType(int row)
    {
        // if we are in the last row, we want to spawn the infected mosquito type
        if(row == this.rows - 1)
        {
            return MosquitoType.Infected;
        }
        else // choose a random mosquito type from the prefabs array, as higher is the row, the more likely to spawn the infected mosquito type
        {
            float infectedChance = (float)row / (float)this.rows;
            float randomValue = UnityEngine.Random.value;

            return randomValue < infectedChance ? MosquitoType.Infected : MosquitoType.Normal;
        }
        
    }

    private void Start()
    {
        InvokeRepeating(nameof(RequestMissileAttack), missileAttackRate, missileAttackRate);
    }
    
    private void FixedUpdate()  
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (stopMovement) return;

        transform.position += _direction * (speed.Evaluate(PercentKilled) * Time.fixedDeltaTime);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if(!PhotonNetwork.IsMasterClient) return;
        
        bool[] activeMosquitos = _mosquitos.Select(mosquito => mosquito.gameObject.activeInHierarchy).ToArray();
        
        int[] mosquitoTypes = _mosquitoTypes.Select(mosquitoType => (int)mosquitoType).ToArray();
        
        photonView.RPC(nameof(RpcSyncMosquitosState), newPlayer, activeMosquitos, mosquitoTypes, AmountKilled, false, _direction.x, (int)_lastBoundariesSideHit);
    }

    public override void OnLeftRoom()
    {
        if(!PhotonNetwork.IsMasterClient) return;
        
        bool[] activeMosquitos = _mosquitos.Select(mosquito => mosquito.gameObject.activeInHierarchy).ToArray();
        
        int[] mosquitoTypes = _mosquitoTypes.Select(mosquitoType => (int)mosquitoType).ToArray();
        
        photonView.RPC(nameof(RpcSyncMosquitosState), RpcTarget.Others, activeMosquitos, mosquitoTypes, AmountKilled, false, _direction.x, (int)_lastBoundariesSideHit);
    }

    [PunRPC]
    private void RpcSyncMosquitosState(bool[] activeMosquitos, int[] mosquitoTypes, int amountKilled, bool bInterpMosquitosWhenSpawn, float directionX, int lastBoundariesSideHit)
    {
        _mosquitoTypes = new Queue<MosquitoType>(mosquitoTypes.Select(t => (MosquitoType)t).ToArray());
        
        SpawnHordePositions();
        SpawnMosquitoForEachHordePosition(bInterpMosquitosWhenSpawn);
        SubscribeToMosquitosEvents();
        
        for (int i = 0; i < activeMosquitos.Length; i++)
        {
            _mosquitos[i].gameObject.SetActive(activeMosquitos[i]);
        }

        AmountKilled = amountKilled;
        UpdateMosquitosMovement();
        
        _direction.x = directionX;
        _lastBoundariesSideHit = (BoundariesSideHit)lastBoundariesSideHit;
    }

    private void RequestMissileAttack()
    {
        if(!PhotonNetwork.IsMasterClient) return;
        
        if(_mosquitos.All(mosquito => mosquito == null || mosquito.Type != MosquitoType.Infected)) return;

        var infectedMosquitos = _mosquitos.Where(mosquito => mosquito.Type == MosquitoType.Infected && mosquito.gameObject.activeInHierarchy).ToArray();
        
        if(infectedMosquitos.Length == 0) return;
        
        var mosquitoToAttack = infectedMosquitos[UnityEngine.Random.Range(0, infectedMosquitos.Length - 1)];
        _shotCounter++;
        
        _photonView.RPC(nameof(MissileAttack), RpcTarget.All, mosquitoToAttack.mosquitoIndex, _shotCounter);
    }
    
    [PunRPC]
    private void MissileAttack(int mosquitosIndex, int shotId)
    {
        if(stopMovement) return;
        
        // Find all alive mosquitos
        var selectedMosquito = _mosquitos[mosquitosIndex];
        if (selectedMosquito != null && selectedMosquito.gameObject.activeInHierarchy)
        {
            var missile = Instantiate(missilePrefab, selectedMosquito.transform.position, Quaternion.identity);
            missile.Init(MosquitosInstigatorId, shotId);
        }
    }
    
    private void MosquitoKilled(int mosquitoIndex, int instigatorPlayerId)
    {
        photonView.RPC(nameof(RpcReplicateKill), RpcTarget.All, mosquitoIndex, instigatorPlayerId);
    }

    [PunRPC]
    private void RpcReplicateKill(int mosquitoIndex, int instigatorPlayerId)
    {
        if(_mosquitos == null || _mosquitos.Length == 0) return;
        
        if (!_mosquitos[mosquitoIndex].gameObject.activeInHierarchy) return;

        var mosquitoKilled = _mosquitos[mosquitoIndex];

        OnMosquitoKilled?.Invoke(instigatorPlayerId, mosquitoKilled.points);

        mosquitoKilled.gameObject.SetActive(false);

        mosquitoKilled.OnMosquitoKilled -= MosquitoKilled;

        AmountKilled++;

        if (AmountKilled >= TotalMosquitos)
        {
            AllMosquitosDestroyedEvent?.Invoke();

            if (PhotonNetwork.IsMasterClient)
                PhotonNetwork.Destroy(gameObject);
        }

        GameManager.Instance.AudioManager.PlaySfx(killedSfx);
        UpdateMosquitosMovement();
    }

    private void UpdateMosquitosMovement()
    {
        if (AmountAlive == 1)
        {
            CancelInvoke(nameof(RequestMissileAttack));
            InvokeRepeating(nameof(RequestMissileAttack), missileAttackRate / 2, missileAttackRate / 2);
        }

        switch (AmountAlive)
        {
            case 10:
                foreach (var mosquito in _mosquitos)
                {
                    if (mosquito != null && mosquito.gameObject.activeInHierarchy)
                    {
                        mosquito.MosquitoMovement.SwitchMovementSettings(last10);
                    }
                }
                break;
            case 7:
                foreach (var mosquito in _mosquitos)
                {
                    if (mosquito != null && mosquito.gameObject.activeInHierarchy)
                    {
                        mosquito.MosquitoMovement.SwitchMovementSettings(last7);
                    }
                }
                break;
            case 5:
                foreach (var mosquito in _mosquitos)
                {
                    if (mosquito != null && mosquito.gameObject.activeInHierarchy)
                    {
                        mosquito.MosquitoMovement.SwitchMovementSettings(last5);
                    }
                }
                break;
            case 3:
                foreach (var mosquito in _mosquitos)
                {
                    if (mosquito != null && mosquito.gameObject.activeInHierarchy)
                    {
                        mosquito.MosquitoMovement.SwitchMovementSettings(last3);
                    }
                }
                break;
            case 1:
                foreach (var mosquito in _mosquitos)
                {
                    if (mosquito != null && mosquito.gameObject.activeInHierarchy)
                    {
                        mosquito.MosquitoMovement.SwitchMovementSettings(last);
                    }
                }
                break;
        }
    }

    public void MosquitoHitBoundaries()
    {
        if(_bRequestedRowAdvance) return;
        
        BoundariesSideHit sideHit = transform.position.x > 0 ? BoundariesSideHit.Right : BoundariesSideHit.Left;
        
        if(sideHit == _lastBoundariesSideHit) return;
        
        _lastBoundariesSideHit = sideHit;
        
        if(!PhotonNetwork.IsMasterClient) return;
        
        _bRequestedRowAdvance = true;

        _photonView.RPC(nameof(AdvanceRow), RpcTarget.All);
    }
    
    [PunRPC]
    private void AdvanceRow()
    {
        // Reverse the direction of the mosquitos
        _direction.x *= -1.0f;

        if (!PhotonNetwork.IsMasterClient) return;

        Vector3 newPosition = transform.position;

        StartCoroutine(InterpolateToNewYPosition(newPosition.y - 2.0f));

        transform.position = newPosition;
        StartCoroutine(FinishRowAdvance());
    }
    
    private IEnumerator InterpolateToNewYPosition(float newYPosition)
    {
        float elapsedTime = 0f;
        Vector3 startingPosition = transform.position;
        Vector3 targetPosition = new Vector3(startingPosition.x, newYPosition, startingPosition.z);

        while (elapsedTime < hordeVerticalInterpolationDuration)
        {
            transform.position = Vector3.Lerp(startingPosition, targetPosition, elapsedTime / hordeVerticalInterpolationDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPosition;
    }
    
    private IEnumerator FinishRowAdvance()
    {
        yield return new WaitForEndOfFrame();
        _bRequestedRowAdvance = false;
    }
    
    private void OnGameOver(int playerCharactersAlive)
    {
        if(playerCharactersAlive > 0) return;
        
        _photonView.RPC(nameof(RpcReplicateGameOver), RpcTarget.All);
    }
    
    [PunRPC]
    private void RpcReplicateGameOver()
    {
        CancelInvoke(nameof(RequestMissileAttack));
        stopMovement = true;
    }

    public override void OnEnable()
    {
        base.OnEnable();
        
        GameMode.GameOverEvent += OnGameOver;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        GameMode.GameOverEvent -= OnGameOver;
    }

    private void OnDrawGizmos()
    {
        if(!enabled || !bDrawDebugGizmos || _hordePositions == null) return;
        
        Gizmos.color = Color.white;
        for(int i = 0; i < _hordePositions.Count; i++)
        {
            var key = _hordePositions.ElementAt(i).Key;
            var position = _hordePositions.ElementAt(i).Value;
            Gizmos.DrawSphere(transform.position + (Vector3)position, 0.5f);
        }
        
        if(debugHordePositionKey.row >= 0 && debugHordePositionKey.column >= 0)
        {
            if(_hordePositions.TryGetValue(debugHordePositionKey, out Vector2 position))
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(transform.position + (Vector3)position, 0.5f);
            }
        }
    }
}
