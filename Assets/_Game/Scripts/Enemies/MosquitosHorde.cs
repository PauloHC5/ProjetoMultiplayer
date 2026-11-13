using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using _Game.Scripts;
using _Game.Scripts.Enemies;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using AYellowpaper.SerializedCollections;

public class MosquitosHorde : MonoBehaviourPunCallbacks
{
    private enum BoundariesSideHit
    {
        Left,
        Right
    }
    
    private enum MosquitoBehaviourStateDifficulty
    {
        Easy,
        Medium,
        Hard
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
    [SerializeField] private LayerMask laserLayerMask;
    public bool enableBehaviour = true;

    [Header("Movement Settings")] [FormerlySerializedAs("speed")] [SerializeField]
    private AnimationCurve hordeSpeedCurve;

    public bool stopMovement = false;
    public float hordeVerticalInterpolationDuration = 0.5f;

    [Header("SFX")] 
    public AudioCue hordeSpawnSfx;
    public AudioCue hordeMovingSfx;
    public AudioCue hordeKilledSfx;
    public AudioCue mosquitoKilledSfx;
    public AudioCue mosquitoSpawnSfx;
    public AudioCue mosquitoFireSfx;
    public float pitchIncreasePerMosquitoKilled = 0.05f;

    [Header("ZigZag Movement Settings")]
    public MosquitoZigZagMovementSettings standardZigZag;
    
    [Space]
    
    public SerializedDictionary<int, MosquitoZigZagMovementSettings> zigZagMovementSettings;

    [Space] 
    
    [SerializeField] private SerializedDictionary<MosquitoBehaviourStateDifficulty, HordeBehaviour> mosquitosBehaviours;

    [Header("Prefabs")] public Mosquito[] prefabs = new Mosquito[3];
    public Projectile mosquitoProjectilePrefab;

    [Header("Debug")] public bool debugLogs = false;
    public bool bDrawDebugGizmos = false;
    public HordePositionKey debugHordePositionKey;

    private Vector3 _direction = Vector3.right;
    private Dictionary<HordePositionKey, Mosquito> _mosquitosDict;
    private PhotonView _photonView;
    private bool _bRequestedRowAdvance = false;
    private bool _bAlreadySetHordePositions = false;
    private bool _bAlreadySpawnedMosquitos = false;
    private int _shotCounter;
    private BoundariesSideHit _lastBoundariesSideHit;
    private Dictionary<HordePositionKey, Vector2> _hordePositions;
    private Coroutine _mosquitosBehaviourRoutine;
    private MosquitoBehaviourStateDifficulty _currentBehaviourDifficulty;
    private List<Mosquito> _infectedMosquitos = new List<Mosquito>();
    
    public float speedBuff = 1f;
    public bool rapidFireActive = false;

    public int TotalMosquitos => this.rows * this.columns;
    public float PercentAlive => ((float)AmountAlive / (float)TotalMosquitos) * 100f;

    public int AmountAlive
    {
        get
        {
            if (_mosquitosDict == null || _mosquitosDict.Count == 0) return 0;
            return _mosquitosDict.Count(kvp => kvp.Value != null && kvp.Value.gameObject.activeInHierarchy);
        }
    }


    public Action<int, int> OnMosquitoKilled;
    public event Action AllMosquitosDestroyedEvent;

    private void Awake()
    {
        _mosquitosDict = new Dictionary<HordePositionKey, Mosquito>(this.rows * this.columns);
        _photonView = GetComponent<PhotonView>();
        
        ValidateZigZagMovementSettings();

        if (PhotonNetwork.InRoom)
        {
            StartSettingUpHorde();
        }
        else
        {
            StartCoroutine(WaitForRoomBeReady());
        }
    }
    
    private void ValidateZigZagMovementSettings()
    {
        int[] requiredKeys = { 1, 3, 5, 7, 10 };

        foreach (int key in requiredKeys)
        {
            if (!zigZagMovementSettings.ContainsKey(key) ||
                zigZagMovementSettings[key] == null)
            {
                Debug.LogError(
                    $"Missing zig-zag movement settings for {key} mosquito(s). " +
                    $"\nIt is required to have settings for 1, 3, 5, 7, and 10 mosquitos alive. " +
                    $"\nPlease assign the appropriate settings in the inspector.",
                    this);
            }
        }
    }

    private IEnumerator WaitForRoomBeReady()
    {
        yield return new WaitUntil(() => PhotonNetwork.InRoom);

        yield return new WaitForSeconds(1f); // Wait for a short delay to ensure the room is fully ready;

        StartSettingUpHorde();
    }

    private void StartSettingUpHorde()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        _photonView.RPC(nameof(SetHordePositions), RpcTarget.AllBuffered);

        SpawnMosquitoForEachHordePosition();

        _photonView.RPC(nameof(GiveATimeOffsetToAllMosquitos), RpcTarget.AllBuffered);

        _photonView.RPC(nameof(RpcStarMosquitosBehaviourRoutine), RpcTarget.AllBuffered);
    }

    [PunRPC]
    private void SetHordePositions()
    {
        if (_bAlreadySetHordePositions) return;

        Debug.Log($"Spawning horde positions");

        for (int row = 0; row < this.rows; row++)
        {
            float width = 4.0f * (this.columns - 1);
            float height = 4.0f * (this.rows - 1);
            Vector2 centering = new Vector2(-width / 2, -height / 2);
            Vector3 rowPosition = new Vector3(centering.x, centering.y + (row * 4.0f), 0.0f);

            for (int col = 0; col < this.columns; col++)
            {
                _hordePositions ??= new Dictionary<HordePositionKey, Vector2>();
                _hordePositions.Add(new HordePositionKey(row, col),
                    new Vector2(rowPosition.x + (col * 4.0f), rowPosition.y));
            }
        }

        _bAlreadySetHordePositions = true;
    }

    private void SpawnMosquitoForEachHordePosition()
    {
        if (!PhotonNetwork.IsMasterClient || _bAlreadySpawnedMosquitos) return;

        for (int i = 0; i < _hordePositions.Count; i++)
        {
            if (!_hordePositions.TryGetValue(new HordePositionKey(i / this.columns, i % this.columns),
                    out Vector2 rowPosition))
            {
                Debug.LogError($"Horde position not found for index {i}");
                continue;
            }

            MosquitoType mosquitoType = RandomizeMosquitoType(_hordePositions.ElementAt(i).Key.row);

            HordePositionKey hordePositionKey = new HordePositionKey(i / this.columns, i % this.columns);

            _photonView.RPC(nameof(SpawnMosquitoAtHorde), RpcTarget.AllBuffered, mosquitoType,
                hordePositionKey, 0f, 0f, false);
        }

        _bAlreadySpawnedMosquitos = true;
    }

    private MosquitoType RandomizeMosquitoType(int row)
    {
        // if we are in the last row, we want to spawn the infected mosquito type
        if (row == this.rows - 1)
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
    private void SpawnMosquitoAtHorde(MosquitoType mosquitoType, HordePositionKey hordePositionKey, float starPosX, float starPosY,
        bool isWorldPosition)
    {
        if (!_hordePositions.TryGetValue(hordePositionKey, out Vector2 rowPosition))
        {
            Debug.LogError($"Horde position not found for key {hordePositionKey.row}, {hordePositionKey.col}");
            return;
        }

        // Check if a mosquito already exists at this horde position
        Mosquito mosquitoSpawned = _mosquitosDict.GetValueOrDefault(hordePositionKey);
        
        if (mosquitoSpawned == null) // If no mosquito exists at this horde position, spawn a new one
        {
            Mosquito mosquitoPrefab = prefabs[(int)mosquitoType];

            mosquitoSpawned = Instantiate(mosquitoPrefab, transform);
            
            _mosquitosDict[hordePositionKey] = mosquitoSpawned;

            mosquitoSpawned.hordePositionKey = hordePositionKey;
            
            if(mosquitoSpawned.Type == MosquitoType.Infected)
                _infectedMosquitos.Add(mosquitoSpawned);
        }
        else // If a mosquito already exists at this horde position, just activate it and set its position
        {
            mosquitoSpawned.gameObject.SetActive(true);
            mosquitoSpawned.transform.localPosition = new  Vector2(starPosX, starPosY);
        }

        Vector3 startPos = isWorldPosition
            ? transform.InverseTransformPoint(new Vector3(starPosX, starPosY, 0f))
            : new Vector3(starPosX, starPosY, 0f);

        _mosquitosDict[hordePositionKey].OnMosquitoHitBoundaries += MosquitoHitBoundaries;
        _mosquitosDict[hordePositionKey].OnMosquitoKilled += MosquitoKilled;

        MoveMosquitoToNewHordePosition(mosquitoSpawned, hordePositionKey, PhotonNetwork.Time + 0.1f, startPos);
        
        if(_bAlreadySpawnedMosquitos)
            GameManager.Instance.AudioManager.PlaySfx(mosquitoSpawnSfx);
        
        UpdateMosquitosMovement();
    }

    public void RequestSpawnMosquito(Vector2 dengueSourcePosition)
    {
        if (!PhotonNetwork.IsMasterClient || AmountAlive >= TotalMosquitos) return;

        var availablePosition = GetAvailableHordePositionKey();
        if (availablePosition.row == -1 && availablePosition.col == -1)
        {
            Debug.LogError("No available horde position to spawn a mosquito.");
            return;
        }

        MosquitoType mosquitoType = RandomizeMosquitoType(availablePosition.row);

        _photonView.RPC(nameof(SpawnMosquitoAtHorde), RpcTarget.AllBuffered, mosquitoType,
            availablePosition, dengueSourcePosition.x, dengueSourcePosition.y, true);
    }

    private HordePositionKey GetAvailableHordePositionKey()
    {
        for (int i = _hordePositions.Count - 1; i >= 0; i--)
        {
            var key = _hordePositions.ElementAt(i).Key;
            var mosquito = _mosquitosDict.GetValueOrDefault(key);

            if (mosquito == null || !mosquito.gameObject.activeInHierarchy)
            {
                return key;
            }
        }

        return new HordePositionKey(-1, -1); // No available position
    }

    [PunRPC]
    private void GiveATimeOffsetToAllMosquitos()
    {
        for (int i = 0; i < _mosquitosDict.Count; i++)
        {
            if (_mosquitosDict.Values.ElementAt(i) != null &&
                _mosquitosDict.Values.ElementAt(i).gameObject.activeInHierarchy)
            {
                var offset = (i * 137.5f) % (Mathf.PI * 2f);
                _mosquitosDict.Values.ElementAt(i).MosquitoZigZagMovement.phaseOffset = offset;
                _mosquitosDict.Values.ElementAt(i).LissajousCurve.timeOffset = offset;
            }
        }
    }

    [PunRPC]
    private void RpcStarMosquitosBehaviourRoutine()
    {
        GameManager.Instance.AudioManager.PlaySfx(hordeSpawnSfx);
        GameManager.Instance.AudioManager.PlaySfxInLoop(hordeMovingSfx);
        
        if(GameMode.Instance?.HordeProgressPercentage == 0)
            return;
        
        if(GameMode.Instance?.HordeProgressPercentage < 50f)
            _currentBehaviourDifficulty = MosquitoBehaviourStateDifficulty.Easy;
        else if(GameMode.Instance?.HordeProgressPercentage < 80f)
            _currentBehaviourDifficulty = MosquitoBehaviourStateDifficulty.Medium;
        else
            _currentBehaviourDifficulty = MosquitoBehaviourStateDifficulty.Hard;
        
        _mosquitosBehaviourRoutine = StartCoroutine(MosquitosBehaviourRoutine());
    }

    private void Start()
    {
        InvokeRepeating(nameof(RequestMosquitoFire), missileAttackRate, missileAttackRate);
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            var mosquitosAlive = _mosquitosDict.Values
                .Where(mosquito => mosquito != null && mosquito.gameObject.activeInHierarchy).ToArray();
            var randomMosquitoIndex = UnityEngine.Random.Range(0, mosquitosAlive.Length);
            var randomMosquito = mosquitosAlive[randomMosquitoIndex];

            MosquitoKilled(randomMosquito, -1);
        }

        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            // Debug log important information about the horde
            Debug.Log("Amount of mosquitos alive: " + AmountAlive +
                      "\nLast boundaries side hit: " + _lastBoundariesSideHit +
                      "\nPercent of mosquitos killed: " + PercentAlive +
                      "\nTotal mosquitos: " + TotalMosquitos +
                      "\nDirection: " + _direction +
                      "\nCurrent Speed: " + hordeSpeedCurve.Evaluate(PercentAlive));
        }
    }
#endif

    private void FixedUpdate()
    {
        if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom) return;
        if (stopMovement || AmountAlive <= 0) return;

        transform.position += _direction * (hordeSpeedCurve.Evaluate(PercentAlive) * speedBuff * Time.fixedDeltaTime);
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        photonView.RPC(nameof(RpcHordeState), RpcTarget.Others, _direction.x, (int)_lastBoundariesSideHit);
        
        HordePositionKey[] mosquitosKeys = _mosquitosDict.Keys.ToArray();
        
        photonView.RPC(nameof(StopBehaviour), RpcTarget.All, mosquitosKeys);
        
        photonView.RPC(nameof(SendMosquitosBackToTheirPositions), RpcTarget.All);
        
        photonView.RPC(nameof(RpcStarMosquitosBehaviourRoutine), RpcTarget.MasterClient);
    }

    [PunRPC]
    private void RpcHordeState(float directionX, int lastBoundariesSideHit)
    {
        UpdateMosquitosMovement();

        _direction.x = directionX;
        _lastBoundariesSideHit = (BoundariesSideHit)lastBoundariesSideHit;
    }

    private void RequestMosquitoFire()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        Mosquito mosquitoToAttack = null;

        if (_infectedMosquitos != null && _infectedMosquitos.Count > 0)
        {
            mosquitoToAttack = _infectedMosquitos[UnityEngine.Random.Range(0, _infectedMosquitos.Count)];
        }

        if (mosquitoToAttack == null) return;

        _shotCounter++;
        
        _photonView.RPC(nameof(MosquitoFire), RpcTarget.All, mosquitoToAttack.hordePositionKey, _shotCounter);
    }

    [PunRPC]
    private void MosquitoFire(HordePositionKey hordePositionKey, int shotId)
    {
        if (stopMovement) return;

        // Get the selected mosquito from the dictionary using the position key
        var selectedMosquito = _mosquitosDict.GetValueOrDefault(hordePositionKey);

        if (selectedMosquito == null || !selectedMosquito.gameObject.activeInHierarchy) return;

        var missile = Instantiate(mosquitoProjectilePrefab, selectedMosquito.transform.position, Quaternion.identity);
        missile.Init(MosquitoBase.MosquitosInstigatorId, shotId);
        
        GameManager.Instance.AudioManager.PlaySfx(mosquitoFireSfx);

        if (debugLogs)
            Debug.Log("Spawning missile with speed: " + missile.Speed);
    }

    private void MosquitoKilled(Mosquito mosquito, int instigatorPlayerId)
    {
        HordePositionKey hordePositionKey = mosquito.hordePositionKey;

        photonView.RPC(nameof(RpcReplicateKill), RpcTarget.AllBuffered, hordePositionKey.row, hordePositionKey.col,
            instigatorPlayerId);
    }

    [PunRPC]
    private void RpcReplicateKill(int mosquitoRow, int mosquitoCol, int instigatorPlayerId)
    {
        HordePositionKey hordePositionKey = new HordePositionKey(mosquitoRow, mosquitoCol);

        if (_mosquitosDict == null || _mosquitosDict.Count == 0) return;

        var selectedMosquito = _mosquitosDict.GetValueOrDefault(hordePositionKey);
        if (selectedMosquito == null || !selectedMosquito.gameObject.activeInHierarchy) return;

        var mosquitoKilled = selectedMosquito;

        OnMosquitoKilled?.Invoke(instigatorPlayerId, mosquitoKilled.points);

        mosquitoKilled.gameObject.SetActive(false);
        
        if(mosquitoKilled.Type == MosquitoType.Infected)
            _infectedMosquitos.Remove(mosquitoKilled);

        mosquitoKilled.OnMosquitoKilled -= MosquitoKilled;
        mosquitoKilled.OnMosquitoHitBoundaries -= MosquitoHitBoundaries;
        
        bool allMosquitosDestroyed = AmountAlive == 0;

        if (allMosquitosDestroyed)
        {
            AllMosquitosDestroyedEvent?.Invoke();

            if (PhotonNetwork.IsMasterClient)
                PhotonNetwork.Destroy(gameObject);
        }
        
        GameManager.Instance.AudioManager.PlaySfx(
            allMosquitosDestroyed ? hordeKilledSfx : mosquitoKilledSfx
            );
        
        UpdateMosquitosMovement();
    }

    private void UpdateMosquitosMovement()
    {
        int amountAlive = AmountAlive;
        
        if (amountAlive == 1)
        {
            CancelInvoke(nameof(RequestMosquitoFire));
            InvokeRepeating(nameof(RequestMosquitoFire), missileAttackRate / 2, missileAttackRate / 2);
        }

        if (amountAlive <= 10 && _mosquitosBehaviourRoutine != null)
        {
            StopCoroutine(_mosquitosBehaviourRoutine);
            _mosquitosBehaviourRoutine = null;

            if(PhotonHandlers.CanUseNetwork && PhotonHandlers.IsAuthority)
                photonView.RPC(nameof(SendMosquitosBackToTheirPositions), RpcTarget.All);
            else
                SendMosquitosBackToTheirPositions();

            CancelInvoke(nameof(RapidFire));
            
            GameManager.Instance.AudioManager.ChangeSfxPitch(hordeMovingSfx, 1.0f);
        }

        var zigZagSettings = GetZigZagMovementSettingsForMosquito(amountAlive);
        
        foreach (Mosquito mosquito in _mosquitosDict.Values)
        {
            if (mosquito == null || !mosquito.gameObject.activeInHierarchy)
                continue;

            if (mosquito.LissajousCurve.HasMovementSettings && amountAlive <= 10)
                mosquito.LissajousCurve.SwitchCurveSettings(null);

            mosquito.MosquitoZigZagMovement.SwitchMovementSettings(zigZagSettings);
        }
    }

    private MosquitoZigZagMovementSettings GetZigZagMovementSettingsForMosquito(int amountAlive)
    {
        if (amountAlive > 10)
        {
            GameManager.Instance.AudioManager.ChangeSfxPitch(hordeMovingSfx, 1.0f);
            return standardZigZag;
        }
        
        float currentPitch = GameManager.Instance.AudioManager.GetSfxPitch(hordeMovingSfx);

        switch (amountAlive)
        {
            case > 7 and <= 10:
                GameManager.Instance.AudioManager.ChangeSfxPitch(hordeMovingSfx, currentPitch + ((10 - amountAlive) * pitchIncreasePerMosquitoKilled), true);
                return zigZagMovementSettings[10];
            case > 5 and <= 7:
                GameManager.Instance.AudioManager.ChangeSfxPitch(hordeMovingSfx, currentPitch + ((7 - amountAlive) * pitchIncreasePerMosquitoKilled), true);
                return zigZagMovementSettings[7];
            case > 3 and <= 5:
                GameManager.Instance.AudioManager.ChangeSfxPitch(hordeMovingSfx, currentPitch + ((5 - amountAlive) * pitchIncreasePerMosquitoKilled), true);
                return zigZagMovementSettings[5];
            case > 1 and <= 3:
                GameManager.Instance.AudioManager.ChangeSfxPitch(hordeMovingSfx, currentPitch + ((3 - amountAlive) * pitchIncreasePerMosquitoKilled), true);
                return zigZagMovementSettings[3];
            case 1:
                GameManager.Instance.AudioManager.ChangeSfxPitch(hordeMovingSfx, currentPitch + 1 * pitchIncreasePerMosquitoKilled, true);
                return zigZagMovementSettings[1];
            default:
                GameManager.Instance.AudioManager.ChangeSfxPitch(hordeMovingSfx, 1.0f);
                return standardZigZag;
        }
    }
    
    [PunRPC]
    public void SendMosquitosBackToTheirPositions()
    {
        if (debugLogs)
            Debug.Log("Sending mosquitos back to their positions.");

        for (int i = 0; i < _mosquitosDict.Values.Count; i++)
        {
            if (_mosquitosDict.Values.ElementAt(i) != null &&
                _mosquitosDict.Values.ElementAt(i).gameObject.activeInHierarchy)
            {
                var hordePositionKey = _mosquitosDict.Values.ElementAt(i).hordePositionKey;
                MoveMosquitoToNewHordePosition(_mosquitosDict.Values.ElementAt(i), hordePositionKey,
                    PhotonNetwork.Time + 0.1f);
            }
        }
    }

    public void MoveMosquitoToNewHordePosition(Mosquito mosquito, HordePositionKey newHordePositionKey,
        double startTime, Vector3 startPos = default)
    {
        if (!_hordePositions.TryGetValue(newHordePositionKey, out Vector2 newHordePosition))
        {
            Debug.LogError($"Horde position not found for key {newHordePositionKey.row}, {newHordePositionKey.col}");
            return;
        }

        var hordePosition = _hordePositions[newHordePositionKey];

        Vector2 startPosition = startPos == default ? mosquito.transform.localPosition : startPos;

        mosquito.GoToNewPosition(hordePosition, startPosition);
    }

    public void MosquitoHitBoundaries()
    {
        if (_bRequestedRowAdvance) return;

        BoundariesSideHit sideHit = transform.position.x > 0 ? BoundariesSideHit.Right : BoundariesSideHit.Left;

        if (sideHit == _lastBoundariesSideHit) return;

        _lastBoundariesSideHit = sideHit;

        if (!PhotonNetwork.IsMasterClient) return;

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
            transform.position = Vector3.Lerp(startingPosition, targetPosition,
                elapsedTime / hordeVerticalInterpolationDuration);
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

    private IEnumerator MosquitosBehaviourRoutine()
    {
        var hordeBehaviour = mosquitosBehaviours.GetValueOrDefault(_currentBehaviourDifficulty);
        
        if (hordeBehaviour == null || !enableBehaviour || !PhotonNetwork.IsMasterClient || AmountAlive <= 10) yield break;

        while (AmountAlive > 10)
        {
            yield return new WaitForSeconds(hordeBehaviour.BehaviourStartDelay);

            HordePositionKey[] mosquitosChosenKeys = Array.Empty<HordePositionKey>();
            int settingsIndexChosen = -1;
            
            hordeBehaviour.ChoseMosquitoBehaviour(columns, rows, _mosquitosDict, out settingsIndexChosen,
                out mosquitosChosenKeys);
            
            ClearBufferedFromBehaviourFunctions();

            photonView.RPC(nameof(StartBehaviour), RpcTarget.AllBuffered, mosquitosChosenKeys, settingsIndexChosen, hordeBehaviour.SendMosquitosToHordeCenter);

            if (hordeBehaviour.IsRapidFire)
            {
                InvokeRepeating(nameof(RapidFire), 0f, hordeBehaviour.RapidFireTime);
                rapidFireActive = true;
            }

            yield return new WaitForSeconds(hordeBehaviour.BehaviourDuration);
            
            ClearBufferedFromBehaviourFunctions();

            photonView.RPC(nameof(StopBehaviour), RpcTarget.AllBuffered, mosquitosChosenKeys);

            if (hordeBehaviour.IsRapidFire)
            {
                CancelInvoke(nameof(RapidFire));
                rapidFireActive = false;
            }

            yield return new WaitForSeconds(hordeBehaviour.TimeToReorder);
            
            ClearBufferedFromBehaviourFunctions();
            
            if(hordeBehaviour.SendMosquitosToHordeCenter)
            {
                photonView.RPC(nameof(SendMosquitosBackToTheirPositions), RpcTarget.AllBuffered);
            }
        }
    }

    private void ClearBufferedFromBehaviourFunctions()
    {
        PhotonNetwork.RemoveBufferedRPCs(photonView.ViewID, nameof(StartBehaviour));
        PhotonNetwork.RemoveBufferedRPCs(photonView.ViewID, nameof(StopBehaviour));
        PhotonNetwork.RemoveBufferedRPCs(photonView.ViewID, nameof(SendMosquitosBackToTheirPositions));
    }

    [PunRPC]
    private void StartBehaviour(HordePositionKey[] mosquitosKeys, int settingsIndexChosen, bool sendMosquitosToHordeCenter)
    {
        var hordeBehaviour = mosquitosBehaviours.GetValueOrDefault(_currentBehaviourDifficulty);
        
        if (hordeBehaviour == null || mosquitosKeys == null || mosquitosKeys.Length == 0 ||
            settingsIndexChosen < 0) return;

        if (debugLogs)
        {
            Debug.Log("MosquitosBehaviourBase: Behaviour started.");
            Debug.Log("MosquitosBehaviourBase: Mosquitos chosen length: " + mosquitosKeys.Length);
            Debug.Log("MosquitosBehaviourBase: Settings index chosen: " + settingsIndexChosen);
            Debug.Log("MosquitosBehaviourBase: Send mosquitos to horde center: " + sendMosquitosToHordeCenter);
        }

        Mosquito[] mosquitosChosen = mosquitosKeys.Select(key => _mosquitosDict.GetValueOrDefault(key))
            .Where(mosquito => mosquito != null && mosquito.gameObject.activeInHierarchy).ToArray();
        LissajouCurveSettings movementSettingsChosen = hordeBehaviour.GetMovementSettings(settingsIndexChosen);

        if (movementSettingsChosen == null)
        {
            Debug.LogWarning("Failed to get movement settings.");
            return;
        }

        for (int i = 0; i < mosquitosChosen.Length; i++)
        {
            if (mosquitosChosen[i] != null && mosquitosChosen[i].gameObject.activeInHierarchy)
            {
                if (sendMosquitosToHordeCenter)
                {
                    mosquitosChosen[i].GoToNewPosition(Vector2.zero);
                }
                mosquitosChosen[i].LissajousCurve.SwitchCurveSettings(movementSettingsChosen);
            }
        }
        
        GameManager.Instance.AudioManager.ChangeSfxPitch(hordeMovingSfx, hordeBehaviour.PitchToApplyOnHordeMovingSfx, true);
    }
    
    private void RapidFire()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        if(debugLogs)
            Debug.Log("Rapid fire requested.");

        RequestMosquitoFire();
    }

    [PunRPC]
    private void StopBehaviour(HordePositionKey[] mosquitosKeys)
    {
        var hordeBehaviour = mosquitosBehaviours.GetValueOrDefault(_currentBehaviourDifficulty);
        
        if (hordeBehaviour == null || mosquitosKeys == null || mosquitosKeys.Length == 0) return;

        if (debugLogs)
        {
            Debug.Log("MosquitosBehaviourBase: Behaviour started.");
            Debug.Log("MosquitosBehaviourBase: Mosquitos chosen length: " + mosquitosKeys.Length);
        }

        Mosquito[] mosquitosChosen = mosquitosKeys.Select(key => _mosquitosDict.GetValueOrDefault(key))
            .Where(mosquito => mosquito != null && mosquito.gameObject.activeInHierarchy).ToArray();

        for (int i = 0; i < mosquitosChosen.Length; i++)
        {
            if (mosquitosChosen[i] != null && mosquitosChosen[i].gameObject.activeInHierarchy)
            {
                mosquitosChosen[i].LissajousCurve.SwitchCurveSettings(null);
            }
        }
        
        GameManager.Instance.AudioManager.ChangeSfxPitch(hordeMovingSfx, 1.0f, true);
    }

    private void OnGameOver(GameMode.GameOverReason reason)
    {
        _photonView.RPC(nameof(RpcReplicateGameOver), RpcTarget.All);
    }

    [PunRPC]
    private void RpcReplicateGameOver()
    {
        CancelInvoke(nameof(RequestMosquitoFire));
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

    private void OnDestroy()
    {
        if(GameManager.Instance == null || GameManager.Instance.AudioManager == null) return;
        
        GameManager.Instance.AudioManager.StopSfxInLoop(hordeMovingSfx);
        GameManager.Instance.AudioManager.PlaySfx(hordeKilledSfx);
    }

    private void OnDrawGizmos()
    {
        if (!enabled || !bDrawDebugGizmos || _hordePositions == null) return;

        Gizmos.color = Color.white;
        for (int i = 0; i < _hordePositions.Count; i++)
        {
            var key = _hordePositions.ElementAt(i).Key;
            var position = _hordePositions.ElementAt(i).Value;
            Gizmos.DrawSphere(transform.position + (Vector3)position, 0.5f);
        }

        if (debugHordePositionKey.row >= 0 && debugHordePositionKey.col >= 0)
        {
            if (_hordePositions.TryGetValue(debugHordePositionKey, out Vector2 position))
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(transform.position + (Vector3)position, 0.5f);
            }
        }
    }
}