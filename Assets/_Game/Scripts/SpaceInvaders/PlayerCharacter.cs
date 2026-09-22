using System;
using System.Collections;
using _Game.Scripts;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerType
{
    Player1,
    Player2,
    Player3
}

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PhotonRigidbody2DView))]
public class PlayerCharacter : MonoBehaviourPun
{
    [Header("Player Settings")]
    public Projectile projectilePrefab;
    [SerializeField] private float speed = 10.0f;
    [SerializeField] private Transform projectileSpawnPoint;

    [Header("Audio")]
    public AudioCue shootSfx;

    [Header("Bot Settings")]
    public bool bBotMode = false;

    public AudioCue explosionSfx;
    
    public SpriteRenderer SpriteRenderer { get; private set; }
    private Rigidbody2D _rigidbody2D;
    private Animator _characterAnimator;

    private bool _projectileActive;
    private bool _invincible;
    private TextMeshProUGUI _playerNicknameText;
    private int _shotCounter;
    private Vector2 _botMoveDirection = Vector2.zero;
    
    public Action<PlayerCharacter> OnPlayerDestroyed;
    
    private readonly int MoveXHash = Animator.StringToHash("MoveX");
    private readonly int MoveYHash = Animator.StringToHash("MoveY");
    private readonly int IsMovingHash = Animator.StringToHash("IsMoving");

    private void Awake()
    {
        SpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _playerNicknameText = GetComponentInChildren<TextMeshProUGUI>();
        _characterAnimator = GetComponentInChildren<Animator>();
        
        // Only the owner needs a real physics simulation to get blocked by
        // walls/obstacles. Remote copies get their position overwritten by
        // PhotonTransformView every frame - if they stayed Dynamic, their
        // local physics would fight that sync and cause jitter.
        if (!photonView.IsMine)
        {
            _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    private void Start()
    {
        if (!photonView.IsMine || !PhotonNetwork.IsConnected) return;
        
        StartCoroutine(WaitForRoomBeReady());
    }

    private IEnumerator WaitForRoomBeReady()
    {
        yield return new WaitUntil(() => PhotonNetwork.InRoom);
        
        photonView.RPC(nameof(RpcStartInvincible), RpcTarget.AllBuffered);
        
        photonView.RPC(nameof(RpcSetNickname), RpcTarget.AllBuffered, PhotonNetwork.NickName);
    }

    private void Update()
    {
        if (!photonView.IsMine) return;
        
        if(Keyboard.current.fKey.wasPressedThisFrame)
            photonView.RPC(nameof(RpcExplodeSfx), RpcTarget.All);

        if (bBotMode)
            BotMovement();
    }

    private void BotMovement()
    {
        // Bot movement logic
        if(_botMoveDirection == Vector2.zero || UnityEngine.Random.value < 0.01f) // Randomly change direction
        {
            _botMoveDirection = new Vector2(UnityEngine.Random.Range(-1f, 1f), 0f).normalized;
        }
        Move(_botMoveDirection);

        // Bot shooting logic
        if (UnityEngine.Random.value < 0.01f) // Random chance to shoot
        {
            TryShoot();
        }
    }

    [PunRPC]
    private void RpcSetNickname(string nickname)
    {
        _playerNicknameText.text = nickname;
        _playerNicknameText.color = GameMode.GetPlayerColorBasedOnPlayerType(photonView.Owner);
    }

    public void Move(Vector2 direction) 
    {
        _characterAnimator.SetBool(IsMovingHash, direction.sqrMagnitude > 0.01f);
        _characterAnimator.SetFloat(MoveXHash, direction.x);
        _characterAnimator.SetFloat(MoveYHash, direction.y);

        Vector2 movement = new Vector2(direction.x, direction.y) * speed;
        _rigidbody2D.linearVelocity = movement;
    }
    
    public void TryShoot()
    {
        if(_projectileActive) return;
        
        if (PhotonNetwork.IsConnected)
        {
            if (!photonView.IsMine) return;
            
            Vector2 projectileSpawnPosition = projectileSpawnPoint.position;
            Vector2 characterVelocity = _rigidbody2D.linearVelocity;

            int shotId = _shotCounter++;

            photonView.RPC(nameof(RpcShoot), RpcTarget.All,
                projectileSpawnPosition,
                characterVelocity,
                shotId);
        }
        else
        {
            RpcShoot(projectileSpawnPoint.position, _rigidbody2D.linearVelocity, _shotCounter++, new PhotonMessageInfo());
        }
        
        _projectileActive = true;
    }
    
    [PunRPC]
    private void RpcShoot(Vector2 projectileSpawnPosition, Vector2 shooterVelocity, int shotId, PhotonMessageInfo info)
    {
        float lag = Mathf.Max(0f, (float)(PhotonNetwork.Time - info.SentServerTime));

        if (!photonView.IsMine)
            Debug.Log($"RPC Shoot received from {info.Sender.NickName} with lag of: " + lag);

        Vector3 fixedPositionToSpawn = projectileSpawnPosition + shooterVelocity * lag;

        Projectile projectile = Instantiate(projectilePrefab, fixedPositionToSpawn, Quaternion.identity);
        projectile.Destroyed += OnProjectileDestroyed;
        
        projectile.Init(photonView.OwnerActorNr, shotId);
        projectile.Collider2D.enabled = photonView.IsMine;

        GameManager.Instance.AudioManager.PlaySfx(shootSfx);
    }
    
    private void OnProjectileDestroyed(Projectile projectile)
    {
        _projectileActive = false;
        projectile.Destroyed -= OnProjectileDestroyed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(!PhotonNetwork.IsMasterClient) return;
        
        if (_invincible) return;
                
        if(other.gameObject.layer == LayerMask.NameToLayer("Missile") || other.gameObject.layer == LayerMask.NameToLayer("Invader"))
        {
            photonView.RPC(nameof(RpcExplodeSfx), RpcTarget.All);
            
            var player = photonView.Owner;
            
            if (Equals(PhotonNetwork.MasterClient, player))
            {
                PhotonNetwork.Destroy(gameObject);
            }
            else
            {
                photonView.RPC(nameof(RpcExplode), player);
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if(!bBotMode || !photonView.IsMine) return;
        
        _botMoveDirection = -_botMoveDirection; // Reverse direction on collision
    }

    [PunRPC]
    private void RpcExplode()
    {
        PhotonNetwork.Destroy(gameObject);
    }

    [PunRPC]
    public void RpcExplodeSfx()
    {                    
        GameManager.Instance.AudioManager.PlaySfx(explosionSfx);
    }

    private void OnDestroy()
    {
        OnPlayerDestroyed?.Invoke(this);
    }

    [PunRPC]
    private void RpcStartInvincible()
    {
        StartCoroutine(Invincible());
    }
    
    [PunRPC]
    private void RpcEndInvincible()
    {
        StopAllCoroutines();
        _invincible = false;
        SpriteRenderer.enabled = true;
    }
    
    private IEnumerator Invincible()
    {
        _invincible = true;
        
        float invincibleTime = 3.0f;
        float elapsedTime = 0.0f;
        bool isVisible = true;

        while (elapsedTime < invincibleTime)
        {
            isVisible = !isVisible;
            SpriteRenderer.enabled = isVisible;

            yield return new WaitForSeconds(0.2f);
            elapsedTime += 0.2f;
        }

        SpriteRenderer.enabled = true;
        _invincible = false;
        
        photonView.RPC(nameof(RpcEndInvincible), RpcTarget.OthersBuffered);
    }
    
    private void OnGameOver(int playerCharactersAlive)
    {
        if(photonView.IsMine && playerCharactersAlive <= 0)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        GameMode.GameOverEvent += OnGameOver;
    }

    private void OnDisable()
    {
        GameMode.GameOverEvent -= OnGameOver;
    }
}
