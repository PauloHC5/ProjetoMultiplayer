using System;
using System.Collections;
using System.Linq;
using _Game.Scripts;
using Photon.Pun;
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
    private Camera _mainCamera;
    private Invader[] _invaders;
    private PhotonView _photonView;
    private bool _bRequestedRowAdvance = false;
    
    public const string DeadInvadersKey = "DeadInvaders";
    
    public int AmountKilled { get; private set; }
    public int TotalInvaders => this.rows * this.columns;
    public float PercertKilled => (float)this.AmountKilled / (float)this.TotalInvaders;
    public int AmountAlive => this.TotalInvaders - this.AmountKilled;
    
    public Action OnInvadersDestroyed;

    private void Awake()
    {
        _mainCamera = Camera.main;
        _invaders = new Invader[this.rows * this.columns];
        _photonView = GetComponent<PhotonView>();
        
        SpawnInvaders();
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
                invader.invaderIndex = row * this.columns + col;
            }
        }
    }

    private void Start()
    {
        InvokeRepeating(nameof(RequestMissileAttack), missileAttackRate, missileAttackRate);

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(DeadInvadersKey))
        {
            SetDeadInvaders(new bool[TotalInvaders]);
        }

        SyncInvadersState();
    }
    
    private void FixedUpdate()  
    {
        if(stopMovement) return;
        
        transform.position += _direction * (speed.Evaluate(PercertKilled) * Time.fixedDeltaTime);
    }
    
    private bool[] GetDeadInvaders()
    {
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(DeadInvadersKey, out var deadMaskObj) && deadMaskObj is bool[] deadMask)
        {
            return deadMask;
        }
        
        // If the dead mask is not found, return a new array with all invaders alive
        return new bool[TotalInvaders];
    }
    
    private void SetDeadInvaders(bool[] deadMask)
    {
        if(! PhotonNetwork.IsMasterClient ||!PhotonNetwork.InRoom || !PhotonNetwork.IsConnectedAndReady) return;
        
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { DeadInvadersKey, deadMask } });
    }

    private void SyncInvadersState()
    {
        var deadMask = GetDeadInvaders();
        int killedCount = 0;

        for (int i = 0; i < _invaders.Length; i++)
        {
            if (!deadMask[i]) continue;
            
            killedCount++;
            DeactivateInvader(i, false);
        }
        
        AmountKilled = killedCount;
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if(!propertiesThatChanged.ContainsKey(DeadInvadersKey)) return;
        SyncInvadersState();
    }

    private void RequestMissileAttack()
    {
        if(!PhotonNetwork.IsMasterClient) return;
        
        int invaderIndex = UnityEngine.Random.Range(0, _invaders.Length);
        
        _photonView.RPC(nameof(MissileAttack), RpcTarget.All, invaderIndex);
    }
    
    public void RequestKill(int invaderIndex, int instigatorPlayerId)
    {
        _photonView.RPC(nameof(RpcRequestKill), RpcTarget.MasterClient, invaderIndex, instigatorPlayerId);
    }

    [PunRPC]
    private void RpcRequestKill(int invaderIndex, int instigatorPlayerId)
    {
        if(!PhotonNetwork.IsMasterClient) return;
        
        if(invaderIndex < 0 || invaderIndex >= _invaders.Length) return;
        if(!_invaders[invaderIndex].gameObject.activeInHierarchy) return;
        
        var invadersDeadMask = GetDeadInvaders();
        if(invadersDeadMask[invaderIndex]) return; // Already dead
        
        invadersDeadMask[invaderIndex] = true;
        SetDeadInvaders(invadersDeadMask);
        
        _photonView.RPC(nameof(RpcConfirmKill), RpcTarget.All, invaderIndex, instigatorPlayerId);
    }

    [PunRPC]
    private void RpcConfirmKill(int invaderIndex, int instigatorPlayerId, PhotonMessageInfo info)
    {
        GameMode.OnInvaderKilled?.Invoke(instigatorPlayerId, _invaders[invaderIndex].points);
        
        DeactivateInvader(invaderIndex, info.Sender.IsLocal);

        AmountKilled++;
        
        if(PhotonNetwork.IsMasterClient && AmountKilled >= TotalInvaders)
        {
            OnInvadersDestroyed?.Invoke();
            
            // Reset the dead invaders mask for the next game
            var deadMask = GetDeadInvaders();
            Array.Clear(deadMask, 0, deadMask.Length);
            SetDeadInvaders(deadMask);

            PhotonNetwork.Destroy(gameObject);
        }
        
        GameManager.Instance.AudioManager.PlaySfx(killedSfx);
    }
    
    private void DeactivateInvader(int invaderIndex, bool isLocal)
    {
        if(invaderIndex < 0 || invaderIndex >= _invaders.Length) return;
        if(!_invaders[invaderIndex].gameObject.activeInHierarchy) return;
        
        if(isLocal)
        {
            _invaders[invaderIndex].DeactivateDelay();
        }
        else
        {
            _invaders[invaderIndex].gameObject.SetActive(false);
        }
    }
    
    public void HitBoundaries()
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
