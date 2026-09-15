using System;
using System.Collections;
using System.Linq;
using _Game.Scripts;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class Invaders : MonoBehaviourPunCallbacks
{
    public Invader[] prefabs;
    public int rows = 5;
    public int columns = 11;
    public AnimationCurve speed;
    public float missileAttackRate = 1.0f;
    public Projectile missilePrefab;
    public AudioCue killedSfx;
    [SerializeField] private LayerMask laserLayerMask;
    public bool stopMovement = false;
    
    private Vector3 _direction = Vector3.right;    
    private Invader[] _invaders;
    private PhotonView _photonView;
    private bool _bRequestedRowAdvance = false;
    
    public const string DeadInvadersKey = "DeadInvaders";
    
    public int AmountKilled { get; private set; }
    public int TotalInvaders => this.rows * this.columns;
    public float PercertKilled => (float)this.AmountKilled / (float)this.TotalInvaders;
    public int AmountAlive => this.TotalInvaders - this.AmountKilled;
    
    public Action<int, int> OnInvaderKilled;
    public event Action AllInvadersDestroyedEvent;

    private void Awake()
    {        
        _invaders = new Invader[this.rows * this.columns];
        _photonView = GetComponent<PhotonView>();
        
        SpawnInvaders();

        for(int i = 0; i < _invaders.Length; i++)
        {
            _invaders[i].invaderIndex = i;
            _invaders[i].OnInvaderHitBoundaries += InvaderHitBoundaries;
            _invaders[i].OnInvaderKilled += InvaderKilled;
        }
    }

    private void SpawnInvaders()
    {
        for (int row = 0; row < this.rows; row++)
        {
            float width = 2.0f * (this.columns - 1);
            float height = 2.0f * (this.rows - 1);
            Vector2 centering = new Vector2(-width / 2, -height / 2);
            Vector3 rowPosition = new Vector3(centering.x, centering.y + (row * 2.0f), 0.0f);
            
            for(int col = 0; col < this.columns; col++)
            {
                Invader invader = Instantiate(this.prefabs[row], this.transform);
                Vector3 position = rowPosition;
                position.x += col * 2.0f;
                invader.transform.localPosition = position;
                _invaders[row * this.columns + col] = invader;
            }
        }
    }

    private void Start()
    {
        InvokeRepeating(nameof(RequestMissileAttack), missileAttackRate, missileAttackRate);
    }
    
    private void FixedUpdate()  
    {
        if(stopMovement) return;
        
        transform.position += _direction * (speed.Evaluate(PercertKilled) * Time.fixedDeltaTime);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if(!PhotonNetwork.IsMasterClient) return;

        bool[] activeInvaders = _invaders.Select(invader => invader.gameObject.activeInHierarchy).ToArray();

        photonView.RPC(nameof(RpcSyncInvadersState), newPlayer, activeInvaders);
    }

    [PunRPC]
    private void RpcSyncInvadersState(bool[] activeInvaders)
    {                
        for (int i = 0; i < activeInvaders.Length; i++)
        {
            _invaders[i].gameObject.SetActive(activeInvaders[i]);
        }
    }

    private void RequestMissileAttack()
    {
        if(!PhotonNetwork.IsMasterClient) return;
        
        int invaderIndex = UnityEngine.Random.Range(0, _invaders.Length);
        
        _photonView.RPC(nameof(MissileAttack), RpcTarget.All, invaderIndex);
    }
    
    private void InvaderKilled(int invaderIndex, int instigatorPlayerId)
    {
        RpcReplicateKill(invaderIndex, instigatorPlayerId);
    }    

    private void RpcReplicateKill(int invaderIndex, int instigatorPlayerId)
    {
        if (!_invaders[invaderIndex].gameObject.activeInHierarchy) return;
        
        var invaderKilled = _invaders[invaderIndex];

        OnInvaderKilled?.Invoke(instigatorPlayerId, invaderKilled.points);
        
        invaderKilled.gameObject.SetActive(false);
        
        invaderKilled.OnInvaderKilled -= InvaderKilled;

        AmountKilled++;
        
        if(PhotonNetwork.IsMasterClient && AmountKilled >= TotalInvaders)
        {
            AllInvadersDestroyedEvent?.Invoke();

            PhotonNetwork.Destroy(gameObject);
        }
        
        GameManager.Instance.AudioManager.PlaySfx(killedSfx);
    }
    
    public void InvaderHitBoundaries()
    {
        if(!PhotonNetwork.IsMasterClient || _bRequestedRowAdvance) return;
        
        _bRequestedRowAdvance = true;
        _photonView.RPC(nameof(AdvanceRow), RpcTarget.All);
    }
    
    [PunRPC]
    private void AdvanceRow()
    {
        // Reverse the direction of the invaders and move them down by 1 unit
        _direction.x *= -1.0f;
        
        Vector3 newPosition = transform.position;
        
        // Move the invaders down by 1 unit
        newPosition.y -= 1.0f;
        
        transform.position = newPosition;
        StartCoroutine(FinishRowAdvance());
    }
    
    private IEnumerator FinishRowAdvance()
    {
        yield return new WaitForEndOfFrame();
        _bRequestedRowAdvance = false;
    }
    
    [PunRPC]
    private void MissileAttack(int invaderIndex)
    {
        if(stopMovement) return;
        
        // Find all alive invaders
        var selectedInvader = _invaders[invaderIndex];
        if (selectedInvader != null && selectedInvader.gameObject.activeInHierarchy)
        {
            Instantiate(missilePrefab, selectedInvader.transform.position, Quaternion.identity);
        }
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
