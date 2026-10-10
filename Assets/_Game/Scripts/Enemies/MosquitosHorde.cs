using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using _Game.Scripts;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

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
        public int col;

        public HordePositionKey(int row, int col)
        {
            this.row = row;
            this.col = col;
        }

        public bool Equals(HordePositionKey other)
        {
            return row == other.row && col == other.col;
        }

        public override bool Equals(object obj)
        {
            return obj is HordePositionKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(row, col);
        }
    }
    
    [Header("Horde Settings")]
    [SerializeField] public int rows = 5;
    [SerializeField] int columns = 11;
    [SerializeField] private float missileAttackRate = 1.0f;
    public Vector2 mosquitoStartPosition = new Vector2(0.0f, 15.0f);
    [SerializeField] private LayerMask laserLayerMask;
    
    [Header("Movement Settings")]
    [FormerlySerializedAs("speed")] [SerializeField] private AnimationCurve hordeSpeedCurve;
    public bool stopMovement = false;
    public float hordeVerticalInterpolationDuration = 0.5f;
    
    [Header("Audio")]
    public AudioCue killedSfx;
    
    [Header("ZigZag Movement Settings")]
    [Space]
    [Header("Movement Settings for the last 10 mosquitos")]
    public MosquitoZigZagMovementSettings last10;
    [Header("Movement Settings for the last 7 mosquitos")]
    public MosquitoZigZagMovementSettings last7;
    [Header("Movement Settings for the last 5 mosquitos")]
    public MosquitoZigZagMovementSettings last5;
    [Header("Movement Settings for the last 3 mosquitos")]
    public MosquitoZigZagMovementSettings last3;
    [Header("Movement Settings for the last mosquito")]
    public MosquitoZigZagMovementSettings last;
    
    [Header("Prefabs")]
    public Mosquito[] prefabs = new Mosquito[3];
    public Projectile missilePrefab;
    
    [Header("Debug")]
    public bool bDrawDebugGizmos = false;
    public HordePositionKey debugHordePositionKey;
    
    private Vector3 _direction = Vector3.right;    
    private Mosquito[] _mosquitos;
    private Mosquito[] _last10Mosquitos;
    private Mosquito _lastMosquito;
    private PhotonView _photonView;
    private bool _bRequestedRowAdvance = false;
    private bool _bAlreadySetHordePositions = false;
    private bool _bAlreadySpawnedMosquitos = false;
    private int _shotCounter;
    private BoundariesSideHit _lastBoundariesSideHit;
    public const int MosquitosInstigatorId = 451;
    private Dictionary<HordePositionKey, Vector2> _hordePositions;
    
    //public int AmountKilled { get; private set; }
    public int TotalMosquitos => this.rows * this.columns;
    public float PercentKilled => (float)AmountAlive / (float)TotalMosquitos;

    public int AmountAlive
    {
        get 
        {
            if(_mosquitos == null || _mosquitos.Length == 0) return 0;
            return _mosquitos.Count(mosquito => mosquito != null && mosquito.gameObject.activeInHierarchy);
        }
    }
    
    
    public Action<int, int> OnMosquitoKilled;
    public event Action AllMosquitosDestroyedEvent;

    private void Awake()
    {        
        _mosquitos = new Mosquito[this.rows * this.columns];
        _photonView = GetComponent<PhotonView>();

        if (PhotonNetwork.InRoom)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            
            _photonView.RPC(nameof(SetHordePositions), RpcTarget.AllBuffered);
            
            SpawnMosquitoForEachHordePosition();
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
        
        if (!PhotonNetwork.IsMasterClient) yield break;
        
        _photonView.RPC(nameof(SetHordePositions), RpcTarget.AllBuffered);
        
        SpawnMosquitoForEachHordePosition();
    }

    [PunRPC]
    private void SetHordePositions()
    {
        if(_bAlreadySetHordePositions) return;
        
        Debug.Log($"Spawning horde positions");
        
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
        
        _bAlreadySetHordePositions = true;
    }
    
    private void SpawnMosquitoForEachHordePosition()
    {
        if(!PhotonNetwork.IsMasterClient || _bAlreadySpawnedMosquitos) return;
        
        for (int i = 0; i < _hordePositions.Count; i++)
        {
            if(!_hordePositions.TryGetValue(new HordePositionKey(i / this.columns, i % this.columns), out Vector2 rowPosition))
            {
                Debug.LogError($"Horde position not found for index {i}");
                continue;
            }
            
            MosquitoType mosquitoType = RandomizeMosquitoType(_hordePositions.ElementAt(i).Key.row);
            
            HordePositionKey hordePositionKey = new HordePositionKey(i / this.columns, i % this.columns);
            
            _photonView.RPC(nameof(SpawnMosquitoAtHorde), RpcTarget.AllBuffered, mosquitoType,
                hordePositionKey.row, hordePositionKey.col);
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
    
    [PunRPC]
    private void SpawnMosquitoAtHorde(MosquitoType mosquitoType, int row, int col)
    {
        HordePositionKey hordePositionKey = new HordePositionKey(row, col);

        if(!_hordePositions.TryGetValue(hordePositionKey, out Vector2 rowPosition))
        {
            Debug.LogError($"Horde position not found for key {hordePositionKey.row}, {hordePositionKey.col}");
            return;
        }

        Mosquito mosquitoPrefab = prefabs[(int)mosquitoType];
        
        Mosquito mosquito = Instantiate(mosquitoPrefab, transform);
        
        int index = hordePositionKey.row * this.columns + hordePositionKey.col;
        
        if(_mosquitos[index] != null)
        {
            Debug.LogWarning($"Mosquito already exists at index {index}. Replacing it.");
            Destroy(_mosquitos[index].gameObject);
        }
        _mosquitos[index] = mosquito;
        
        _mosquitos[index].mosquitoId = index;
        _mosquitos[index].OnMosquitoHitBoundaries += MosquitoHitBoundaries;
        _mosquitos[index].OnMosquitoKilled += MosquitoKilled;
        
        MoveMosquitoToNewHordePosition(mosquito, hordePositionKey, PhotonNetwork.Time + 0.1f);
    }
    
    public void RequestSpawnMosquito(DengueSource dengueSource)
    {
        if(!PhotonNetwork.IsMasterClient || AmountAlive >= TotalMosquitos) return;
        
        var availablePosition = GetAvailableHordePositionKey();
        if(availablePosition.row == -1 && availablePosition.col == -1)
        {
            Debug.LogError("No available horde position to spawn a mosquito.");
            return;
        }
        
        MosquitoType mosquitoType = RandomizeMosquitoType(availablePosition.row);

        _photonView.RPC(nameof(SpawnMosquitoAtHorde), RpcTarget.AllBuffered, mosquitoType,
            availablePosition.row, availablePosition.col);
    }
    
    private HordePositionKey GetAvailableHordePositionKey()
    {
        for (int i = 0; i < _hordePositions.Count; i++)
        {
            var key = _hordePositions.ElementAt(i).Key;
            var mosquito = _mosquitos[i];

            if (mosquito == null || !mosquito.gameObject.activeInHierarchy)
            {
                return key;
            }
        }

        return new HordePositionKey(-1, -1); // No available position
    }

    private void Start()
    {
        InvokeRepeating(nameof(RequestMissileAttack), missileAttackRate, missileAttackRate);
    }

#if UNITY_EDITOR
    private void Update()
    {
        if(Keyboard.current.kKey.wasPressedThisFrame)
        {
            
            var mosquitosAlive = _mosquitos.Where(mosquito => mosquito != null && mosquito.gameObject.activeInHierarchy).ToArray();
            var randomMosquitoIndex = UnityEngine.Random.Range(0, mosquitosAlive.Length);
            var randomMosquito = mosquitosAlive[randomMosquitoIndex];

            MosquitoKilled(randomMosquito.mosquitoId, -1);
        }
        
        if(Keyboard.current.lKey.wasPressedThisFrame)
        {
            // Debug log important information about the horde
            Debug.Log("Amount of mosquitos alive: " + AmountAlive + 
                      "\nLast boundaries side hit: " + _lastBoundariesSideHit +
                      "\nPercent of mosquitos killed: " + PercentKilled +
                      "\nTotal mosquitos: " + TotalMosquitos +
                      "\nDirection: " + _direction);
        }
    }
#endif

    private void FixedUpdate()  
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (stopMovement) return;

        transform.position += _direction * (hordeSpeedCurve.Evaluate(PercentKilled) * Time.fixedDeltaTime);
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if(!PhotonNetwork.IsMasterClient) return;
        
        photonView.RPC(nameof(RpcHordeState), RpcTarget.Others, _direction.x, (int)_lastBoundariesSideHit);
    }

    [PunRPC]
    private void RpcHordeState(int amountKilled, float directionX, int lastBoundariesSideHit)
    {
        UpdateMosquitosMovement();
        
        _direction.x = directionX;
        _lastBoundariesSideHit = (BoundariesSideHit)lastBoundariesSideHit;
    }

    private void RequestMissileAttack()
    {
        if(!PhotonNetwork.IsMasterClient) return;
        
        Mosquito mosquitoToAttack = null;
        
        if (_lastMosquito == null || !_lastMosquito.gameObject.activeInHierarchy)
        {
            // Check if we are in the last 10 mosquitos
            bool areWeInTheLast10Mosquitos =
                AmountAlive <= 10 && _last10Mosquitos != null && _last10Mosquitos.Length > 0;

            if (areWeInTheLast10Mosquitos)
            {
                var infectedMosquitos = _last10Mosquitos.Where(mosquito => mosquito != null &&
                    mosquito.Type == MosquitoType.Infected && mosquito.gameObject.activeInHierarchy).ToArray();

                if (infectedMosquitos.Length == 0) return;

                mosquitoToAttack = infectedMosquitos[UnityEngine.Random.Range(0, infectedMosquitos.Length - 1)];
            }
            else
            {
                var infectedMosquitos = _mosquitos.Where(mosquito => mosquito != null &&
                    mosquito.Type == MosquitoType.Infected && mosquito.gameObject.activeInHierarchy).ToArray();

                if (infectedMosquitos.Length == 0) return;

                mosquitoToAttack = infectedMosquitos[UnityEngine.Random.Range(0, infectedMosquitos.Length - 1)];
            }
        }
        else
        {
            mosquitoToAttack = _lastMosquito;
        }

        _shotCounter++;
        
        _photonView.RPC(nameof(MissileAttack), RpcTarget.All, mosquitoToAttack.mosquitoId, _shotCounter);
    }
    
    [PunRPC]
    private void MissileAttack(int mosquitosIndex, int shotId)
    {
        if(stopMovement) return;
        
        // Get the selected mosquito from the array using the index
        var selectedMosquito = _mosquitos[mosquitosIndex];
        
        var missile = Instantiate(missilePrefab, selectedMosquito.transform.position, Quaternion.identity);
        missile.Init(MosquitosInstigatorId, shotId);
    }
    
    private void MosquitoKilled(int mosquitoIndex, int instigatorPlayerId)
    {
        photonView.RPC(nameof(RpcReplicateKill), RpcTarget.AllBuffered, mosquitoIndex, instigatorPlayerId);
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
        
        if (AmountAlive == 0)
        {
            AllMosquitosDestroyedEvent?.Invoke();

            if (PhotonNetwork.IsMasterClient)
                PhotonNetwork.Destroy(gameObject);
        }
        
        if(AmountAlive == 1)
        {
            _lastMosquito = _mosquitos.FirstOrDefault(mosquito => mosquito != null && mosquito.gameObject.activeInHierarchy);
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
        
        if(_last10Mosquitos == null && AmountAlive <= 10)
        {
            _last10Mosquitos = _mosquitos.Where(mosquito => mosquito != null && mosquito.gameObject.activeInHierarchy).ToArray();
        }
        
        if(_last10Mosquitos == null || _last10Mosquitos.Length == 0) return;

        switch (AmountAlive)
        {
            case 10:
                for (var i = 0; i < _last10Mosquitos.Length; i++)
                {
                    if (_last10Mosquitos[i] != null && _last10Mosquitos[i].gameObject.activeInHierarchy)
                    {
                        _last10Mosquitos[i].MosquitoZigZagMovement.SwitchMovementSettings(last10);
                    }
                }

                break;
            case 7:
                for (var i = 0; i < _last10Mosquitos.Length; i++)
                {
                    if (_last10Mosquitos[i] != null && _last10Mosquitos[i].gameObject.activeInHierarchy)
                    {
                        _last10Mosquitos[i].MosquitoZigZagMovement.SwitchMovementSettings(last7);
                    }
                }

                break;
            case 5:
                for (var i = 0; i < _last10Mosquitos.Length; i++)
                {
                    if (_last10Mosquitos[i] != null && _last10Mosquitos[i].gameObject.activeInHierarchy)
                    {
                        _last10Mosquitos[i].MosquitoZigZagMovement.SwitchMovementSettings(last5);
                    }
                }

                break;
            case 3:
                for (var i = 0; i < _last10Mosquitos.Length; i++)
                {
                    if (_last10Mosquitos[i] != null && _last10Mosquitos[i].gameObject.activeInHierarchy)
                    {
                        _last10Mosquitos[i].MosquitoZigZagMovement.SwitchMovementSettings(last3);
                    }
                }

                break;
            case 1:
                for (var i = 0; i < _last10Mosquitos.Length; i++)
                {
                    if (_last10Mosquitos[i] != null && _last10Mosquitos[i].gameObject.activeInHierarchy)
                    {
                        _last10Mosquitos[i].MosquitoZigZagMovement.SwitchMovementSettings(last);
                    }
                }

                break;
        }
    }
    
    public void MoveMosquitoToNewHordePosition(Mosquito mosquito, HordePositionKey newHordePositionKey, double startTime)
    {
        if(!_hordePositions.TryGetValue(newHordePositionKey, out Vector2 newHordePosition))
        {
            Debug.LogError($"Horde position not found for key {newHordePositionKey.row}, {newHordePositionKey.col}");
            return;
        }

        /*if (!_mosquitos.Contains(mosquito))
        {
            Debug.LogWarning($"Mosquito {mosquito.name} is not part of the horde.");
            return;
        }*/
        
        var hordePosition = _hordePositions[newHordePositionKey];
        
        mosquito.GoToNewPosition(hordePosition, startTime);
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
    
    private void OnGameOver(GameMode.GameOverReason reason)
    {
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
        
        if(debugHordePositionKey.row >= 0 && debugHordePositionKey.col >= 0)
        {
            if(_hordePositions.TryGetValue(debugHordePositionKey, out Vector2 position))
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(transform.position + (Vector3)position, 0.5f);
            }
        }
    }
}
