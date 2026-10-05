using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using _Game.Scripts;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class MosquitosHorde : MonoBehaviourPunCallbacks
{
    private enum BoundariesSideHit
    {
        Left,
        Right
    }
    
    [Header("Settings")]
    public Mosquito[] prefabs = new Mosquito[3];
    public int rows = 5;
    public int columns = 11;
    public AnimationCurve speed;
    public float missileAttackRate = 1.0f;
    public Projectile missilePrefab;
    public AudioCue killedSfx;
    [SerializeField] private LayerMask laserLayerMask;
    public bool stopMovement = false;
    
    [Header("ZigZag Movement Settings when the horde is at 10%, 7%, 5%, 3% and 1% of mosquitos alive")]
    public MosquitoZigZagMovement Last10ZigZag;
    public MosquitoZigZagMovement Last7ZigZag;
    public MosquitoZigZagMovement Last5ZigZag;
    public MosquitoZigZagMovement Last3ZigZag;
    public MosquitoZigZagMovement LastZigZag;
    
    private Vector3 _direction = Vector3.right;    
    private Mosquito[] _mosquitos;
    private PhotonView _photonView;
    private bool _bRequestedRowAdvance = false;
    private int _shotCounter;
    private BoundariesSideHit _lastBoundariesSideHit;
    public const int MosquitosInstigatorId = 451;
    private Queue<MosquitoType> _mosquitoTypes;
    
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
        
        if(!PhotonNetwork.IsMasterClient) return;
        
        _mosquitoTypes = new Queue<MosquitoType>();
        
        SpawnMosquitos();
        SubscribeToMosquitosEvents();
        
        bool[] mosquitosActive = _mosquitos.Select(mosquito => mosquito.gameObject.activeInHierarchy).ToArray();
        
        int[] mosquitoTypes = _mosquitoTypes.Select(mosquitoType => (int)mosquitoType).ToArray();
        
        photonView.RPC(nameof(RpcSyncMosquitosState), RpcTarget.Others, mosquitosActive, mosquitoTypes);
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

    private void SpawnMosquitos()
    {
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
                Mosquito mosquitoPrefab;
                
                if (PhotonNetwork.IsMasterClient)
                {
                    MosquitoType mosquitoType = RandomizeMosquitoType(row);
                    mosquitoPrefab = prefabs[(int)mosquitoType];
                    _mosquitoTypes.Enqueue(mosquitoType);
                }
                else
                {
                    mosquitoPrefab = prefabs[(int)mosquitoTypes.Dequeue()];
                }
                
                SpawnMosquito(mosquitoPrefab, row, rowPosition, col);
            }
        }
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

    private void SpawnMosquito(Mosquito mosquitoPrefab,int row, Vector3 rowPosition, int col)
    {
        Mosquito mosquito = Instantiate(mosquitoPrefab, this.transform);
        Vector3 position = rowPosition;
        position.x += col * 4.0f;
        mosquito.transform.localPosition = position;
        _mosquitos[row * this.columns + col] = mosquito;
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

        photonView.RPC(nameof(RpcSyncMosquitosState), newPlayer, activeMosquitos, mosquitoTypes);
    }

    [PunRPC]
    private void RpcSyncMosquitosState(bool[] activeMosquitos, int[] mosquitoTypes)
    {
        _mosquitoTypes = new Queue<MosquitoType>(mosquitoTypes.Select(t => (MosquitoType)t).ToArray());
        
        SpawnMosquitos();
        SubscribeToMosquitosEvents();
        
        for (int i = 0; i < activeMosquitos.Length; i++)
        {
            _mosquitos[i].gameObject.SetActive(activeMosquitos[i]);
        }
    }

    private void RequestMissileAttack()
    {
        if(!PhotonNetwork.IsMasterClient) return;

        if(_mosquitos.All(mosquito => mosquito.type != MosquitoType.Infected)) return;

        var infectedMosquitos = _mosquitos.Where(mosquito => mosquito.type == MosquitoType.Infected && mosquito.gameObject.activeInHierarchy).ToArray();
        
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
        if (!_mosquitos[mosquitoIndex].gameObject.activeInHierarchy) return;
        
        var mosquitoKilled = _mosquitos[mosquitoIndex];

        OnMosquitoKilled?.Invoke(instigatorPlayerId, mosquitoKilled.points);
        
        mosquitoKilled.gameObject.SetActive(false);
        
        mosquitoKilled.OnMosquitoKilled -= MosquitoKilled;

        AmountKilled++;
        
        if(AmountKilled >= TotalMosquitos)
        {
            AllMosquitosDestroyedEvent?.Invoke();

            if(PhotonNetwork.IsMasterClient)
                PhotonNetwork.Destroy(gameObject);
        }
        
        GameManager.Instance.AudioManager.PlaySfx(killedSfx);

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
                        mosquito.SetZigZagMovementSettings(Last10ZigZag);
                    }
                }
                break;
            case 7:
                foreach (var mosquito in _mosquitos)
                {
                    if (mosquito != null && mosquito.gameObject.activeInHierarchy)
                    {
                        mosquito.SetZigZagMovementSettings(Last7ZigZag);
                    }
                }
                break;
            case 5:
                foreach (var mosquito in _mosquitos)
                {
                    if (mosquito != null && mosquito.gameObject.activeInHierarchy)
                    {
                        mosquito.SetZigZagMovementSettings(Last5ZigZag);
                    }
                }
                break;
            case 3:
                foreach (var mosquito in _mosquitos)
                {
                    if (mosquito != null && mosquito.gameObject.activeInHierarchy)
                    {
                        mosquito.SetZigZagMovementSettings(Last3ZigZag);
                    }
                }
                break;
            case 1:
                foreach (var mosquito in _mosquitos)
                {
                    if (mosquito != null && mosquito.gameObject.activeInHierarchy)
                    {
                        mosquito.SetZigZagMovementSettings(LastZigZag);
                    }
                }
                break;
        }
    }
    
    public void MosquitoHitBoundaries()
    {
        if(!PhotonNetwork.IsMasterClient || _bRequestedRowAdvance) return;
        
        BoundariesSideHit sideHit = transform.position.x > 0 ? BoundariesSideHit.Right : BoundariesSideHit.Left;
        
        if(sideHit == _lastBoundariesSideHit) return;
        
        _lastBoundariesSideHit = sideHit;
        
        _bRequestedRowAdvance = true;
        _photonView.RPC(nameof(AdvanceRow), RpcTarget.All);
    }
    
    [PunRPC]
    private void AdvanceRow()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // Reverse the direction of the mosquitos and move them down by 2 units
        _direction.x *= -1.0f;
        
        Vector3 newPosition = transform.position;
        
        StartCoroutine(InterpolateToNewYPosition(newPosition.y - 2.0f, 0.5f));
        
        transform.position = newPosition;
        StartCoroutine(FinishRowAdvance());
    }
    
    private IEnumerator InterpolateToNewYPosition(float newYPosition, float duration)
    {
        float elapsedTime = 0f;
        Vector3 startingPosition = transform.position;
        Vector3 targetPosition = new Vector3(startingPosition.x, newYPosition, startingPosition.z);

        while (elapsedTime < duration)
        {
            transform.position = Vector3.Lerp(startingPosition, targetPosition, elapsedTime / duration);
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
}
