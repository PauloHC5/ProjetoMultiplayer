using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using _Game.Scripts;
using AYellowpaper.SerializedCollections;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum PlayerType
{
    Player1,
    Player2,
    Player3,

    None
}

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PhotonRigidbody2DView))]
public class PlayerCharacter : MonoBehaviourPun
{
    [Header("Player Settings")]
    [SerializeField] private float speed = 10.0f;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private Collider2D playerCollider;
    [SerializeField] private Collider2D otherPlayersCollider;

    [Header("UI")]
    [SerializeField] private RectTransform pickupsStatusPanel;
    public SerializedDictionary<Pickup.PickupType, Sprite> pickupTypeSprites;
    public RectTransform playerMedkitWarningPanel;    
    public float medkitWarningPanelSinMultiplier = 1.0f;
    public float medkitWarningPanelAmplitude = 5.0f;
    public float maxMedkitWarningPanelAlpha = 0.5f;    

    [Header("Prefabs")]
    public Projectile projectilePrefab;

    [Header("Bot Settings")]
    public bool bBotMode = false;

    [Header("SFX")]
    public AudioCue shootSfx;
    public AudioCue deathSfx;
    public AudioCue ressurectSfx;

    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D _rigidbody2D;
    private Animator _characterAnimator;

    private int _projectilesActive;
    private TextMeshProUGUI _playerNicknameText;
    private int _shotCounter;
    private Vector2 _botMoveDirection = Vector2.zero;
    private int _projectilesLayer;
    private int _enemiesLayer;
    private int _pickupsLayer;
    private int _playersLayer;
    private Coroutine _invincibleCoroutine;
    private TrailRenderer _trailRenderer;
    private Shield_VFX _shieldVFX;
    private Image _medkitIconImage;

    public Action<PlayerCharacter> OnPlayerDeath;
    public Action<Pickup.PickupType, float> OnPickupCollected;
    public Action<PlayerCharacter> OnPlayerRessurected;
    
    private delegate void DisableMedkitWarningDelegate();
    
    private readonly Dictionary<int, DisableMedkitWarningDelegate> _medkitWarningPanelDisableCallback = new Dictionary<int, DisableMedkitWarningDelegate>();
    
    public Player Owner => photonView.Owner;

    private readonly int MoveXHash = Animator.StringToHash("MoveX");
    private readonly int MoveYHash = Animator.StringToHash("MoveY");
    private readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private readonly int AnimMultiplierHash = Animator.StringToHash("AnimMultiplier");

    // Buffs & Status
    private bool _invincible;
    private bool _tripleShootActive = false;
    private bool _piercingProjectilesActive = false;
    private bool _fasterFireActive = false;
    private bool _speedBuffActive = false;
    private bool _hasMedkit = false;

    public int GetPlayerId() => photonView.OwnerActorNr;

    private void Awake()
    {
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _playerNicknameText = GetComponentInChildren<TextMeshProUGUI>();
        _characterAnimator = GetComponentInChildren<Animator>();
        _trailRenderer = GetComponentInChildren<TrailRenderer>();
        _shieldVFX = GetComponentInChildren<Shield_VFX>();

        _projectilesLayer = LayerMask.NameToLayer("EnemiesProjectiles");
        _enemiesLayer = LayerMask.NameToLayer("Enemies");
        _pickupsLayer = LayerMask.NameToLayer("Pickups");
        _playersLayer = LayerMask.NameToLayer("Player");

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
        if (PhotonHandlers.CanUseNetwork)
            StartCoroutine(WaitForRoomBeReady());
        else
        {
            RpcStartInvincible();
            RpcSetNickname("LocalPlayer");
        }
    }

    private IEnumerator WaitForRoomBeReady()
    {
        yield return new WaitUntil(() => PhotonNetwork.InRoom);

        photonView.RPC(nameof(RpcStartInvincible), RpcTarget.AllBuffered);

        photonView.RPC(nameof(RpcSetNickname), RpcTarget.AllBuffered, PhotonNetwork.NickName);

        if(!photonView.IsMine)
            playerCollider.enabled = false;

    }

    private void Update()
    {
        if (playerMedkitWarningPanel.gameObject.activeInHierarchy)
        {
            float sinValue = Mathf.Sin(Time.time * medkitWarningPanelSinMultiplier);
            float alpha = Mathf.InverseLerp(maxMedkitWarningPanelAlpha, 1.0f, sinValue);
            Color color = playerMedkitWarningPanel.GetComponent<Image>().color;
            color.a = alpha;
            playerMedkitWarningPanel.GetComponent<Image>().color = color;
            playerMedkitWarningPanel.localPosition += new Vector3(0, Mathf.Sin(Time.time * medkitWarningPanelSinMultiplier) * medkitWarningPanelAmplitude, 0);
        }            

        if (!photonView.IsMine) return;

#if UNITY_EDITOR
        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            if(PhotonHandlers.CanUseNetwork)
                photonView.RPC(nameof(RpcDeath), RpcTarget.All);
            else
                RpcDeath();
        }

        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            // Get other player in the room
            Player otherPlayer = null;
            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (player != PhotonNetwork.LocalPlayer)
                {
                    otherPlayer = player;
                    break;
                }
            }

            if (otherPlayer == null) return;

            bool isOtherPlayerOutOfLives = otherPlayer.CustomProperties.TryGetValue(GameMode.PlayerOutOfLivesKey, out var isOutOfLives) && (bool)isOutOfLives;

            Debug.Log("Is other player out of lives? " + isOtherPlayerOutOfLives);
        }
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
                photonView.RPC(nameof(RpcSetPickupState), RpcTarget.AllBuffered, pickup.Type, pickup.Duration);
                pickup.RequestDestroyPickup();
            }
            else
            {
                RpcSetPickupState(pickup.Type, pickup.Duration);
                Destroy(other.gameObject);
            }

            if(photonView.IsMine)
                GameManager.Instance.AudioManager.PlaySfx(pickup.pickupCollectedSfx);
        }

        if (other.gameObject.layer == _playersLayer)
        {
            if(!_hasMedkit) return;
            
            Player otherPlayer = other.GetComponent<PhotonView>().Owner;
            
            if(otherPlayer == null || otherPlayer == PhotonNetwork.LocalPlayer || !GameMode.IsPlayerOutOfLives(otherPlayer)) return;
            
            SetPickupState(Pickup.PickupType.Medkit, false);
            
            GameMode.Instance.RequestRespawnDeadPlayer(otherPlayer);
        }
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if(!bBotMode || !photonView.IsMine) return;

        _botMoveDirection = -_botMoveDirection; // Reverse direction on collision
    }
    
    [PunRPC]
    public void RessurectPlayer()
    {
        if (photonView.IsMine)
        {
            photonView.RPC(nameof(RessurectPlayer), RpcTarget.Others);            
            playerCollider.enabled = true;
        }
        
        photonView.RPC(nameof(RpcStartInvincible), RpcTarget.AllBuffered);
        _characterAnimator.SetBool(IsDeadHash, false);
        _characterAnimator.Rebind();
        _rigidbody2D.linearVelocity = Vector2.zero;
        GameManager.Instance.AudioManager.PlaySfx(ressurectSfx);
        
        OnPlayerRessurected?.Invoke(this);
    }

    [PunRPC]
    private void RpcDeath()
    {
        bBotMode = false;

        _rigidbody2D.linearVelocity = Vector2.zero;

        _characterAnimator.SetBool(IsDeadHash, true);

        _shieldVFX.BreakShield();

        GameManager.Instance.AudioManager.PlaySfx(deathSfx);

        OnPlayerDeath?.Invoke(this);

        StartCoroutine(DeathRoutine());

        if (photonView.IsMine)
        {
            playerCollider.enabled = false;
            
            if(_medkitWarningPanelDisableCallback.Count <= 0) return;
            
            foreach (var key in _medkitWarningPanelDisableCallback.Keys.ToList())
            {
                _medkitWarningPanelDisableCallback[key]?.Invoke();
            }
        }
    }

    private IEnumerator DeathRoutine()
    {        
        _spriteRenderer.enabled = true;
        float elapsedTime = 0f;

        float deathDuration = GameMode.Instance ? GameMode.Instance.playerDeathCooldown : 1.5f;

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

        _spriteRenderer.enabled = true;

        if (photonView.IsMine && GameMode.Instance.CurrentLives > 0)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }

    private IEnumerator DeathBlinkEffect(float interval)
    {
        while (true)
        {
            _spriteRenderer.enabled = !_spriteRenderer.enabled;
            yield return new WaitForSeconds(interval);
        }
    }

    [PunRPC]
    private void RpcStartInvincible()
    {
        if(_invincible) return;

        if(_invincibleCoroutine != null)
            StopCoroutine(_invincibleCoroutine);
        _invincibleCoroutine = StartCoroutine(Invincible(true));
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
        _spriteRenderer.enabled = true;
    }

    private IEnumerator Invincible(bool isSpawnInvencible, float duration = 3.0f)
    {
        _invincible = true;
        _spriteRenderer.enabled = true;

        if (isSpawnInvencible)
        {
            float elapsedTime = 0.0f;
            while (elapsedTime < duration)
            {
                _spriteRenderer.enabled = !_spriteRenderer.enabled;

                yield return new WaitForSeconds(0.2f);
                elapsedTime += 0.2f;
            }
        }
        else
        {
            _shieldVFX.RiseShield(duration);
            yield return new WaitForSeconds(duration);
        }

        _spriteRenderer.enabled = true;
        _invincible = false;

        _shieldVFX.BreakShield();

        if(PhotonHandlers.CanUseNetwork)
            photonView.RPC(nameof(RpcEndInvincible), RpcTarget.OthersBuffered);
        else
            RpcEndInvincible();
    }

    [PunRPC]
    private void RpcSetPickupState(Pickup.PickupType pickupType, float duration)
    {
        StartCoroutine(PickupDuration(pickupType, duration));

        OnPickupCollected?.Invoke(pickupType, duration);
    }

    private IEnumerator PickupDuration(Pickup.PickupType pickupType, float duration)
    {
        SetPickupState(pickupType, true, duration);

        if(pickupType == Pickup.PickupType.Medkit) yield break;

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
            Pickup.PickupType.Medkit => _hasMedkit,
            _ => false
        };
    }

    private void SetPickupState(Pickup.PickupType pickupType, bool state, float duration = 0f)
    {
        switch (pickupType)
        {
            case Pickup.PickupType._1UP:
                if(!photonView.IsMine) return;

                if(GameMode.Instance == null) return;
                GameMode.Instance.AddExtraLife();
                break;
            case Pickup.PickupType.FasterFire:
                _fasterFireActive = state;
                StartCoroutine(PickupIconRoutine(pickupType, duration));
                break;
            case Pickup.PickupType.PiercingProjectiles:
                _piercingProjectilesActive = state;
                StartCoroutine(PickupIconRoutine(pickupType, duration));
                break;
            case Pickup.PickupType.Repellent:
                if(state)
                {
                    StartCoroutine(Invincible(false, duration));
                    StartCoroutine(PickupIconRoutine(pickupType, duration));
                }
                break;
            case Pickup.PickupType.SpeedBuff:
                _speedBuffActive = state;
                _trailRenderer.emitting = state;
                speed = state ? speed * 2f : speed / 2f; // Double speed when active, revert when inactive
                _characterAnimator.SetFloat(AnimMultiplierHash, state ? 2f : 1f);
                StartCoroutine(PickupIconRoutine(pickupType, duration));
                break;
            case Pickup.PickupType.TripleFire:
                _tripleShootActive = state;
                StartCoroutine(PickupIconRoutine(pickupType, duration));
                break;
            case Pickup.PickupType.Toolbox:
                if(!photonView.IsMine || !BarriersManager.Instance) return;
                BarriersManager.Instance.RestoreBarrier();
                break;
            case Pickup.PickupType.Medkit:
                if(!photonView.IsMine || !GameMode.Instance) return;
                _hasMedkit = state;
                
                if(!state)
                    Destroy(_medkitIconImage.gameObject);                
                else
                    _medkitIconImage = CreatePickupIcon(pickupType);

                List<PlayerCharacter> deadPlayers = GameMode.Instance.CheckIfIsThereAnyPlayersOutOfLives();
                
                EnableMedkitWarning(deadPlayers);

                break;
            default:
                Debug.LogWarning("Unknown pickup type: " + pickupType);
                break;
        }
    }
    
    private void EnableMedkitWarning(List<PlayerCharacter> deadPlayers)
    {
        if(!photonView.IsMine || deadPlayers == null || deadPlayers.Count <= 0 || !_hasMedkit) return;

        foreach (var deadPlayer in deadPlayers)
        {
            if(deadPlayer == null || _medkitWarningPanelDisableCallback.ContainsKey(deadPlayer.Owner.ActorNumber)) continue;

            deadPlayer.playerMedkitWarningPanel.gameObject.SetActive(true);

            _medkitWarningPanelDisableCallback.Add(deadPlayer.Owner.ActorNumber, () =>
            {
                if (!photonView.IsMine) return;

                deadPlayer.playerMedkitWarningPanel.gameObject.SetActive(false);
                
                _medkitWarningPanelDisableCallback.Remove(deadPlayer.Owner.ActorNumber);
            });
            
            deadPlayer.OnPlayerRessurected += DisableMedkitWarning;
        }
    }
    
    private void DisableMedkitWarning(PlayerCharacter resurrectedPlayer)
    {
        if(!_hasMedkit)
        {
            if(_medkitWarningPanelDisableCallback.Count <= 0) return;
            
            foreach (var key in _medkitWarningPanelDisableCallback.Keys.ToList())
            {
                _medkitWarningPanelDisableCallback[key]?.Invoke();
            }
        }
        else
        {
            if (!_medkitWarningPanelDisableCallback.TryGetValue(resurrectedPlayer.Owner.ActorNumber,
                    out var disableCallback)) return;
        
            disableCallback?.Invoke();
        }
        
        resurrectedPlayer.OnPlayerRessurected -= DisableMedkitWarning;
    }

    private IEnumerator PickupIconRoutine(Pickup.PickupType pickupType, float duration)
    {
        if(!photonView.IsMine) yield break;

        // Create an empty GameObject to hold the pickup icon
        var pickIconImage = CreatePickupIcon(pickupType);

        float countdown = duration;
        Color originalColor = pickIconImage.color;
        float aThirdDuration = duration / 3f;

        while (countdown > 0)
        {
            if (countdown <= aThirdDuration)
            {
                float alpha = Mathf.PingPong(Time.time * 5, 1);
                pickIconImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            }
            countdown -= Time.deltaTime;
            yield return null;
        }

        Destroy(pickIconImage.gameObject);
    }

    private Image CreatePickupIcon(Pickup.PickupType pickupType)
    {
        GameObject pickupIconGO = new GameObject("PickupIcon");
        pickupIconGO.transform.SetParent(pickupsStatusPanel, false);
        pickupIconGO.AddComponent<Image>();

        var pickIconImage = pickupIconGO.GetComponent<Image>();
        pickIconImage.sprite = pickupTypeSprites[pickupType];
        return pickIconImage;
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
        GameMode.OnPlayerOutOfLives += EnableMedkitWarning;
    }

    private void OnDisable()
    {
        GameMode.GameOverEvent -= OnGameOver;
        GameMode.OnPlayerOutOfLives -= EnableMedkitWarning;
    }
}
