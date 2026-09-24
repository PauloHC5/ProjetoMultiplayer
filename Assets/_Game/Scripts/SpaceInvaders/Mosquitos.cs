using System;
using System.Collections;
using System.Linq;
using _Game.Scripts;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class Mosquitos : MonoBehaviourPunCallbacks
{
    private enum BoundariesSideHit
    {
        Left,
        Right
    }
    
    public Mosquito[] prefabs;
    public int rows = 5;
    public int columns = 11;
    public AnimationCurve speed;
    public float missileAttackRate = 1.0f;
    public Projectile missilePrefab;
    public AudioCue killedSfx;
    [SerializeField] private LayerMask laserLayerMask;
    public bool stopMovement = false;
    
    private Vector3 _direction = Vector3.right;    
    private Mosquito[] _mosquitos;
    private PhotonView _photonView;
    private bool _bRequestedRowAdvance = false;
    private int _shotCounter;
    private BoundariesSideHit _lastBoundariesSideHit;
    public const int MosquitosInstigatorId = 451;
    
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
        
        SpawnMosquitos();

        for(int i = 0; i < _mosquitos.Length; i++)
        {
            _mosquitos[i].mosquitoIndex = i;
            _mosquitos[i].OnMosquitoHitBoundaries += MosquitoHitBoundaries;
            _mosquitos[i].OnMosquitoKilled += MosquitoKilled;
        }
    }

    private void SpawnMosquitos()
    {
        for (int row = 0; row < this.rows; row++)
        {
            float width = 4.0f * (this.columns - 1);
            float height = 4.0f * (this.rows - 1);
            Vector2 centering = new Vector2(-width / 2, -height / 2);
            Vector3 rowPosition = new Vector3(centering.x, centering.y + (row * 4.0f), 0.0f);
            
            for(int col = 0; col < this.columns; col++)
            {
                Mosquito mosquito = Instantiate(this.prefabs[row], this.transform);
                Vector3 position = rowPosition;
                position.x += col * 4.0f;
                mosquito.transform.localPosition = position;
                _mosquitos[row * this.columns + col] = mosquito;
            }
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

        photonView.RPC(nameof(RpcSyncMosquitosState), newPlayer, activeMosquitos);
    }

    [PunRPC]
    private void RpcSyncMosquitosState(bool[] activeMosquitos)
    {                
        for (int i = 0; i < activeMosquitos.Length; i++)
        {
            _mosquitos[i].gameObject.SetActive(activeMosquitos[i]);
        }
    }

    private void RequestMissileAttack()
    {
        if(!PhotonNetwork.IsMasterClient) return;

        if(!_mosquitos.Any(mosquito => mosquito.CompareTag("Infected"))) return;

        var infectedMosquitos = _mosquitos.Where(mosquito => mosquito.CompareTag("Infected") && mosquito.gameObject.activeInHierarchy).ToArray();
        
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
        
        if(PhotonNetwork.IsMasterClient && AmountKilled >= TotalMosquitos)
        {
            AllMosquitosDestroyedEvent?.Invoke();

            PhotonNetwork.Destroy(gameObject);
        }
        
        GameManager.Instance.AudioManager.PlaySfx(killedSfx);
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

        // Reverse the direction of the mosquitos and move them down by 1 unit
        _direction.x *= -1.0f;
        
        Vector3 newPosition = transform.position;
        
        newPosition.y -= 1.0f;
        
        transform.position = newPosition;
        StartCoroutine(FinishRowAdvance());
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
