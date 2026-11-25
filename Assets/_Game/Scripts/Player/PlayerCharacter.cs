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
    [SerializeField] private float fireCooldownDuration = 0.5f;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private Collider2D playerCollider;
    [SerializeField] private Collider2D otherPlayersCollider;
    [SerializeField] private Animator[] characterAnimators;
    [SerializeField] private SpriteRenderer[] playerSprites;

    [Header("UI")]
    [SerializeField] private RectTransform pickupsStatusPanel;
    public SerializedDictionary<Pickup.PickupType, Sprite> pickupTypeSprites;
    public RectTransform playerMedkitWarningPanel;    
    public float medkitWarningPanelSinMultiplier = 1.0f;
    public float medkitWarningPanelAmplitude = 5.0f;
    public float maxMedkitWarningPanelAlpha = 0.5f;

    [Header("Score UI")] 
    [SerializeField] private RectTransform playerCanvas;
    [SerializeField] private TextMeshProUGUI playerNicknameText;
    [SerializeField] private RectTransform plusScoreContainer;
    [SerializeField] private int plusScoreTextPoolSize = 5;
    public float plusScoreTextAnimationDuration = 1.0f;
    public float plusScoreTextDespawnDelay = 3.0f;

    [Header("Prefabs")]
    public Projectile projectilePrefab;
    public TextMeshProUGUI plusScoreTextPrefab;

    [Header("Bot Settings")]
    public bool bBotMode = false;

    [Header("SFX")]
    public AudioCue shootSfx;
    public AudioCue deathSfx;
    public AudioCue ressurectSfx;

    private Rigidbody2D _rigidbody2D;

    private bool _bFireCooldownFlag = false;
    private int _projectilesActive;
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
    private Stack<TextMeshProUGUI> _plusScoreTextPool;
    private Queue<TextMeshProUGUI> _usedPlusScoreTextQueue = new Queue<TextMeshProUGUI>();
    private Dictionary<TextMeshProUGUI, Coroutine> _plusScoreTextAnimations = new Dictionary<TextMeshProUGUI, Coroutine>();
    private Dictionary<TextMeshProUGUI, Vector3> _plusScoreTextOriginalScales = new Dictionary<TextMeshProUGUI, Vector3>();

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
        _rigidbody2D = GetComponent<Rigidbody2D>();
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
        
        _plusScoreTextPool = new Stack<TextMeshProUGUI>(plusScoreTextPoolSize);
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
        
        InitializePlusScoreTextPool();
    }
    
    private void InitializePlusScoreTextPool()
    {
        for (int i = 0; i < plusScoreTextPoolSize; i++)
        {
            CreatePlusScoreText();
        }
    }
    
    private void CreatePlusScoreText()
    {
        TextMeshProUGUI plusScoreText = Instantiate(plusScoreTextPrefab, playerCanvas);
        plusScoreText.color = GameMode.GetPlayerColorBasedOnPlayerType(photonView.Owner);
        plusScoreText.text = string.Empty;
        plusScoreText.gameObject.SetActive(false);
        _plusScoreTextPool.Push(plusScoreText);
    }
    
    private void AddScoreText(int points)
    {
        if (_plusScoreTextPool.Count == 0)
        {
            // If the pool is empty, need to create a new TextMeshProUGUI instance
            CreatePlusScoreText();
        }

        for (int i = 0; i < _usedPlusScoreTextQueue.Count; i++)
        {
            TextMeshProUGUI scoreText = _usedPlusScoreTextQueue.ElementAt(i);

            if (TryGetPlusScoreTextMultiplier(scoreText.text, points, out int multiplier))
            {
                scoreText.text = $"{multiplier + 1}x+{points}";
                RestartPlusScoreAnimation(scoreText);
                return;
            }
        }
        
        TextMeshProUGUI plusScoreTextToUse = _plusScoreTextPool.Pop();
        plusScoreTextToUse.text = $"+{points}";
        plusScoreTextToUse.gameObject.SetActive(true);
        plusScoreTextToUse.rectTransform.SetParent(plusScoreContainer);
        _usedPlusScoreTextQueue.Enqueue(plusScoreTextToUse);

        RestartPlusScoreAnimation(plusScoreTextToUse);
    }

    private void RestartPlusScoreAnimation(TextMeshProUGUI plusScoreText)
    {
        if (_plusScoreTextAnimations.TryGetValue(plusScoreText, out Coroutine animationCoroutine))
        {
            StopCoroutine(animationCoroutine);
        }
        else
        {
            _plusScoreTextOriginalScales[plusScoreText] = plusScoreText.rectTransform.localScale;
        }

        Coroutine newAnimationCoroutine = StartCoroutine(
            AnimatePlusScore(plusScoreText, _plusScoreTextOriginalScales[plusScoreText])
        );
        _plusScoreTextAnimations[plusScoreText] = newAnimationCoroutine;
    }

    private bool TryGetPlusScoreTextMultiplier(string text, int points, out int multiplier)
    {
        string scoreText = $"+{points}";

        if (text.Equals(scoreText))
        {
            multiplier = 1;
            return true;
        }

        int multiplierSeparatorIndex = text.IndexOf('x');
        if (multiplierSeparatorIndex > 0 &&
            text.Substring(multiplierSeparatorIndex + 1).Equals(scoreText) &&
            int.TryParse(text.Substring(0, multiplierSeparatorIndex), out multiplier))
        {
            return true;
        }

        multiplier = 0;
        return false;
    }
    
    private IEnumerator AnimatePlusScore(TextMeshProUGUI plusScoreText, Vector3 originalScale, bool bDestroyAfterAnimation = false)
    {
        float duration = plusScoreTextAnimationDuration;
        float elapsedTime = 0.0f;
        plusScoreText.rectTransform.localScale = Vector3.zero;
        
        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            plusScoreText.rectTransform.localScale = Vector3.Lerp(Vector3.zero, originalScale, Mathf.SmoothStep(0f, 1f, t));
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        yield return new WaitForSeconds(plusScoreText.text == "+300" ? 3f : plusScoreTextDespawnDelay);

        if(bDestroyAfterAnimation)
        {
            Destroy(plusScoreText.gameObject);
            yield break;
        }
        
        ReturnScoreTextToPoolAndRemoveFromQueue(plusScoreText);
    }

    private void ReturnScoreTextToPoolAndRemoveFromQueue(TextMeshProUGUI plusScoreText)
    {
        plusScoreText.text = string.Empty;
        plusScoreText.gameObject.SetActive(false);
        plusScoreText.gameObject.transform.SetParent(playerCanvas);
        _usedPlusScoreTextQueue = new Queue<TextMeshProUGUI>(
            _usedPlusScoreTextQueue.Where(text => text != plusScoreText)
        );
        _plusScoreTextAnimations.Remove(plusScoreText);
        _plusScoreTextOriginalScales.Remove(plusScoreText);
        _plusScoreTextPool.Push(plusScoreText);
    }

    private IEnumerator WaitForRoomBeReady()
    {
        yield return new WaitUntil(() => PhotonNetwork.InRoom);

        photonView.RPC(nameof(RpcStartInvincible), RpcTarget.AllBuffered);

        photonView.RPC(nameof(RpcSetNickname), RpcTarget.All, photonView.Owner.NickName);

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

        if (Keyboard.current.leftShiftKey.isPressed && Keyboard.current.pKey.wasPressedThisFrame)
        {
            // Choose 10 or 40
            int pointsToAdd = UnityEngine.Random.value < 0.5f ? 10 : 40;
            
            AddScoreText(pointsToAdd);
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
        playerNicknameText.text = nickname;
        playerNicknameText.color = GameMode.GetPlayerColorBasedOnPlayerType(photonView.Owner);
    }

    public void Move(Vector2 direction)
    {
        foreach (var animator in characterAnimators)
        {
            animator.SetBool(IsMovingHash, direction.sqrMagnitude > 0.01f);
            animator.SetFloat(MoveXHash, direction.x);
            animator.SetFloat(MoveYHash, direction.y);
        }

        Vector2 movement = new Vector2(direction.x, direction.y) * speed;
        _rigidbody2D.linearVelocity = movement;
    }

    public void TryShoot()
    {
        if(_projectilesActive > 0 || _bFireCooldownFlag) return;

        if (PhotonNetwork.IsConnected)
        {
            if (!photonView.IsMine) return;

            Vector2 projectileSpawnPosition = projectileSpawnPoint.position;
            Vector2 characterVelocity = _rigidbody2D.linearVelocity;

            int shotId = _shotCounter++;
            
            _bFireCooldownFlag = true;
            Invoke(nameof(ResetFireCooldown), fireCooldownDuration);

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
    
    private void ResetFireCooldown() => _bFireCooldownFlag = false;
    
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
        foreach (var animator in characterAnimators)
        {
            animator.SetBool(IsDeadHash, false);
            animator.Rebind();
        }
        _rigidbody2D.linearVelocity = Vector2.zero;
        GameManager.Instance.AudioManager.PlaySfx(ressurectSfx);
        
        OnPlayerRessurected?.Invoke(this);
    }

    [PunRPC]
    private void RpcDeath()
    {
        bBotMode = false;

        _rigidbody2D.linearVelocity = Vector2.zero;

        foreach (var animator in characterAnimators)
        {
            animator.SetBool(IsDeadHash, true);
        }

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
        for(int i = 0; i < playerSprites.Length; i++)
        {
            playerSprites[i].enabled = true;
        }

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

        for(int i = 0; i < playerSprites.Length; i++)
        {
            playerSprites[i].enabled = true;
        }

        if (photonView.IsMine && GameMode.Instance.CurrentLives > 0)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }

    private IEnumerator DeathBlinkEffect(float interval)
    {
        while (true)
        {
            for(int i = 0; i < playerSprites.Length; i++)
            {
                playerSprites[i].enabled = !playerSprites[i].enabled;
            }
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
        
        for(int i = 0; i < playerSprites.Length; i++)
        {
            playerSprites[i].enabled = true;
        }
    }

    private IEnumerator Invincible(bool isSpawnInvencible, float duration = 3.0f)
    {
        _invincible = true;
        
        for(int i = 0; i < playerSprites.Length; i++)
        {
            playerSprites[i].enabled = true;
        }

        if (isSpawnInvencible)
        {
            float elapsedTime = 0.0f;
            while (elapsedTime < duration)
            {
                
                for(int i = 0; i < playerSprites.Length; i++)
                {
                    playerSprites[i].enabled = !playerSprites[i].enabled;
                }

                yield return new WaitForSeconds(0.2f);
                elapsedTime += 0.2f;
            }
        }
        else
        {
            _shieldVFX.RiseShield(duration);
            yield return new WaitForSeconds(duration);
        }

        for(int i = 0; i < playerSprites.Length; i++)
        {
            playerSprites[i].enabled = true;
        }
        
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

        if(pickupType == Pickup.PickupType.Medkit || pickupType == Pickup.PickupType.Toolbox || pickupType == Pickup.PickupType._1UP) yield break;

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
                
                TextMeshProUGUI oneUPText = Instantiate(plusScoreTextPrefab, plusScoreContainer);
                oneUPText.text = "+1UP";
                oneUPText.color = GameMode.GetPlayerColorBasedOnPlayerType(photonView.Owner);
                StartCoroutine(AnimatePlusScore(oneUPText, oneUPText.rectTransform.localScale, true));
                
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
                foreach (var animator in characterAnimators)
                {
                    animator.SetFloat(AnimMultiplierHash, state ? 2f : 1f);
                }
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

    private void OnAddScoreToPlayer(int playerID, int points)
    {
        if(!photonView.IsMine || playerID != photonView.OwnerActorNr) return;

        AddScoreText(points);
    }

    private void OnEnable()
    {
        GameMode.GameOverEvent += OnGameOver;
        GameMode.OnPlayerOutOfLives += EnableMedkitWarning;
        GameMode.OnAddScoreToPlayer += OnAddScoreToPlayer;
    }

    private void OnDisable()
    {
        GameMode.GameOverEvent -= OnGameOver;
        GameMode.OnPlayerOutOfLives -= EnableMedkitWarning;
        GameMode.OnAddScoreToPlayer -= OnAddScoreToPlayer;
    }
}
