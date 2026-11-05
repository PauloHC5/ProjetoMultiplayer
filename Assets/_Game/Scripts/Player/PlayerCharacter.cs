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
    [SerializeField] private float speed = 10.0f;
    [SerializeField] private Transform projectileSpawnPoint;
    public Projectile projectilePrefab;

    [Header("Audio")]
    public AudioCue shootSfx;

    [Header("Bot Settings")]
    public bool bBotMode = false;

    public AudioCue explosionSfx;
    
    public SpriteRenderer SpriteRenderer { get; private set; }
    private Rigidbody2D _rigidbody2D;
    private Animator _characterAnimator;

    private int _projectilesActive;
    private TextMeshProUGUI _playerNicknameText;
    private int _shotCounter;
    private Vector2 _botMoveDirection = Vector2.zero;
    private int _projectilesLayer;
    private int _enemiesLayer;
    private int _pickupsLayer;
    private Coroutine _invincibleCoroutine;
    
    public Action<PlayerCharacter> OnPlayerDeath;
    public Action<Pickup.PickupType, float> OnPickupCollected;
    
    private readonly int MoveXHash = Animator.StringToHash("MoveX");
    private readonly int MoveYHash = Animator.StringToHash("MoveY");
    private readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private readonly int IsDeadHash = Animator.StringToHash("IsDead");
    
    // Buffs & Status
    private bool _invincible;
    private bool _tripleShootActive = false;
    private bool _piercingProjectilesActive = false;
    private bool _fasterFireActive = false;
    private bool _speedBuffActive = false;
    
    public int GetPlayerId() => photonView.OwnerActorNr;

    private void Awake()
    {
        SpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _playerNicknameText = GetComponentInChildren<TextMeshProUGUI>();
        _characterAnimator = GetComponentInChildren<Animator>();
        _projectilesLayer = LayerMask.NameToLayer("EnemiesProjectiles");
        _enemiesLayer = LayerMask.NameToLayer("Enemies");
        _pickupsLayer = LayerMask.NameToLayer("Pickups");
        
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

#if UNITY_EDITOR
        if(Keyboard.current.fKey.wasPressedThisFrame)
            photonView.RPC(nameof(RpcDeath), RpcTarget.All);
#endif

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
        if(_projectilesActive > 0) return;
        
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
    }
    
    [PunRPC]
    private void RpcShoot(Vector2 projectileSpawnPosition, Vector2 shooterVelocity, int shotId, PhotonMessageInfo info)
    {
        float lag = Mathf.Max(0f, (float)(PhotonNetwork.Time - info.SentServerTime));

        Vector3 fixedPositionToSpawn = projectileSpawnPosition + shooterVelocity * lag;

        SpawnLaserProjectile(shotId, fixedPositionToSpawn, 0);

        if (_tripleShootActive)
        {
            SpawnLaserProjectile(shotId, fixedPositionToSpawn, 35);

            SpawnLaserProjectile(shotId, fixedPositionToSpawn, -35);
        }

        GameManager.Instance?.AudioManager.PlaySfx(shootSfx);
    }

    private void SpawnLaserProjectile(int shotId, Vector3 fixedPositionToSpawn, int zRotation)
    {
        Projectile leftProjectile = Instantiate(projectilePrefab, fixedPositionToSpawn, Quaternion.Euler(0, 0, zRotation));
        leftProjectile.Destroyed += OnProjectileDestroyed;
        leftProjectile.Init(photonView.OwnerActorNr, shotId, _piercingProjectilesActive, _fasterFireActive);
        leftProjectile.Collider2D.enabled = photonView.IsMine;
        _projectilesActive++;
    }

    private void OnProjectileDestroyed(Projectile projectile)
    {
        _projectilesActive--;
        projectile.Destroyed -= OnProjectileDestroyed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(!PhotonHandlers.IsAuthority) return;
                
        if(other.gameObject.layer == _projectilesLayer || other.gameObject.layer == _enemiesLayer)
        {
            if (_invincible || bBotMode) return;
            
            photonView.RPC(nameof(RpcDeath), RpcTarget.All);
        }
        
        if(other.gameObject.layer == _pickupsLayer)
        {
            var pickup = other.GetComponent<Pickup>();
            if (pickup == null) return;
            
            if(CheckIfThisPickupIsActive(pickup.Type)) return;

            if (PhotonHandlers.CanUseNetwork)
            {
                photonView.RPC(nameof(RpcReplicatePickupState), RpcTarget.AllBuffered, pickup.Type, pickup.Duration);
                PhotonNetwork.Destroy(other.gameObject);
            }
            else
            {
                SetPickupState(pickup.Type, true, pickup.Duration);
                Destroy(other.gameObject);
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if(!bBotMode || !photonView.IsMine) return;
        
        _botMoveDirection = -_botMoveDirection; // Reverse direction on collision
    }

    [PunRPC]
    private void RpcDeath()
    {
        bBotMode = false;
        
        _rigidbody2D.linearVelocity = Vector2.zero;
        
        _characterAnimator.SetBool(IsDeadHash, true);
        
        GameManager.Instance.AudioManager.PlaySfx(explosionSfx);
        
        OnPlayerDeath?.Invoke(this);
        
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        SpriteRenderer.enabled = true;
        float elapsedTime = 0f;

        float deathDuration = GameMode.Instance.playerDeathCooldown;

        while (elapsedTime < deathDuration)
        {
            if(elapsedTime >= deathDuration / 2f)
            {
                // Blink effect
                StartCoroutine(DeathBlinkEffect(0.05f));
            }

            yield return new WaitForSeconds(0.05f);
            elapsedTime += 0.1f;
        }
        
        if(photonView.IsMine)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }
    
    private IEnumerator DeathBlinkEffect(float interval)
    {
        while (true)
        {
            SpriteRenderer.enabled = !SpriteRenderer.enabled;
            yield return new WaitForSeconds(interval);
        }
    }

    [PunRPC]
    private void RpcStartInvincible()
    {
        if(_invincible) return;
        
        if(_invincibleCoroutine != null)
            StopCoroutine(_invincibleCoroutine);
        _invincibleCoroutine = StartCoroutine(Invincible());
    }
    
    [PunRPC]
    private void RpcEndInvincible()
    {
        if (_invincibleCoroutine != null)
        {
            StopCoroutine(_invincibleCoroutine);
            _invincibleCoroutine = null;
        }

        _invincible = false;
        SpriteRenderer.enabled = true;
    }
    
    private IEnumerator Invincible(float duration = 3.0f)
    {
        _invincible = true;
        SpriteRenderer.enabled = true;

        float elapsedTime = 0.0f;

        while (elapsedTime < duration)
        {
            SpriteRenderer.enabled = !SpriteRenderer.enabled;

            yield return new WaitForSeconds(0.2f);
            elapsedTime += 0.2f;
        }

        SpriteRenderer.enabled = true;
        _invincible = false;
        
        photonView.RPC(nameof(RpcEndInvincible), RpcTarget.OthersBuffered);
    }
    
    [PunRPC]
    private void RpcReplicatePickupState(Pickup.PickupType pickupType, float duration)
    {
        StartCoroutine(PickupDuration(pickupType, duration));
        
        OnPickupCollected?.Invoke(pickupType, duration);
    }
    
    private IEnumerator PickupDuration(Pickup.PickupType pickupType, float duration)
    {
        SetPickupState(pickupType, true, duration);
        
        yield return new WaitForSeconds(duration);
        
        SetPickupState(pickupType, false);
    }
    
    private bool CheckIfThisPickupIsActive(Pickup.PickupType pickupType)
    {
        return pickupType switch
        {
            Pickup.PickupType.FasterFire => _fasterFireActive,
            Pickup.PickupType.PiercingProjectiles => _piercingProjectilesActive,
            Pickup.PickupType.Repellent => _invincible,
            Pickup.PickupType.SpeedBuff => _speedBuffActive,
            Pickup.PickupType.TripleFire => _tripleShootActive,
            _ => false
        };
    }
    
    private void SetPickupState(Pickup.PickupType pickupType, bool state, float duration = 0f)
    {
        switch (pickupType)
        {
            case Pickup.PickupType._1UP:
                if(!photonView.IsMine) return;
                GameMode.Instance.AddExtraLife();
                break;
            case Pickup.PickupType.FasterFire:
                _fasterFireActive = state;
                break;
            case Pickup.PickupType.PiercingProjectiles:
                _piercingProjectilesActive = state;
                break;
            case Pickup.PickupType.Repellent:
                if(state)
                {
                    StartCoroutine(Invincible(duration));
                }
                break;
            case Pickup.PickupType.SpeedBuff:
                _speedBuffActive = state;
                speed = state ? speed * 2f : speed / 2f; // Double speed when active, revert when inactive
                break;
            case Pickup.PickupType.TripleFire:
                _tripleShootActive = state;
                break;
            case Pickup.PickupType.Toolbox:
                if(!photonView.IsMine) return;
                BarriersManager.Instance.RestoreBarrier();
                break;
            default:
                Debug.LogWarning("Unknown pickup type: " + pickupType);
                break;
        }
    }
    
    private void OnGameOver(GameMode.GameOverReason reason)
    {
        if(!photonView.IsMine) return;
        
        bBotMode = false;
        
        photonView.RPC(nameof(RpcDeath), RpcTarget.All);
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
