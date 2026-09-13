using System;
using System.Collections;
using System.Linq;
using _Game.Scripts;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Invaders : MonoBehaviour
{
    public Invader[] prefabs;
    public int rows = 5;
    public int columns = 11;
    public AnimationCurve speed;
    public float missileAttackRate = 1.0f;
    public Projectile missilePrefab;
    public AudioCue killedSfx;
    [SerializeField] private LayerMask laserLayerMask;
    public bool testMode = false;
    
    private Vector3 _direction = Vector3.right;
    private Camera _mainCamera;
    private Invader[] _invaders;
    private PhotonView _photonView;
    private bool _bRequestedRowAdvance = false;
    
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
    }
    
    private void RequestMissileAttack()
    {
        if(!PhotonNetwork.IsMasterClient) return;
        
        int invaderIndex = UnityEngine.Random.Range(0, _invaders.Length);
        
        _photonView.RPC(nameof(MissileAttack), RpcTarget.All, invaderIndex);
    }

    private void FixedUpdate()  
    {
        if(testMode) return;
        
        transform.position += _direction * (speed.Evaluate(PercertKilled) * Time.fixedDeltaTime);
    }
    
    public void ReplicateKill(int invaderIndex, int instigatorPlayerId)
    {
        _photonView.RPC(nameof(RpcConfirmKill), RpcTarget.AllBuffered, invaderIndex, instigatorPlayerId);
    }

    [PunRPC]
    private void RpcConfirmKill(int invaderIndex, int instigatorPlayerId, PhotonMessageInfo info)
    {
        GameMode.OnInvaderKilled?.Invoke(instigatorPlayerId, _invaders[invaderIndex].points);
        
        if (!info.Sender.IsLocal)
        {
            _invaders[invaderIndex].DeactivateDelay();
        }
        else
        {
            _invaders[invaderIndex].gameObject.SetActive(false);
        }

        AmountKilled++;
        
        if(AmountKilled >= TotalInvaders)
        {
            OnInvadersDestroyed?.Invoke();
            Destroy(gameObject);
        }
        
        GameManager.Instance.AudioManager.PlaySfx(killedSfx);
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
        if(testMode) return;
        
        // Find all alive invaders
        var selectedInvader = _invaders[invaderIndex];
        if (selectedInvader != null && selectedInvader.gameObject.activeInHierarchy)
        {
            Instantiate(missilePrefab, selectedInvader.transform.position, Quaternion.identity);
        }
    }
}
