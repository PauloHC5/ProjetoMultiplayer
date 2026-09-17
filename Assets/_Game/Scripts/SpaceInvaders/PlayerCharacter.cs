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
    public Projectile laserPrefab;
    public float speed = 5.0f;
    
    [Header("Audio")]
    public AudioCue shootSfx;
    public AudioCue explosionSfx;
    
    public SpriteRenderer SpriteRenderer { get; private set; }
        
    private bool _laserActive;
    private bool _invincible;
    private Rigidbody2D _rigidbody2D;
    private TextMeshProUGUI _playerNicknameText;
    
    public Action<PlayerCharacter> OnPlayerDestroyed;

    private void Awake()
    {
        SpriteRenderer = GetComponent<SpriteRenderer>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _playerNicknameText = GetComponentInChildren<TextMeshProUGUI>();
        
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
        if (!photonView.IsMine) return;
        
        photonView.RPC(nameof(RpcInvincible), RpcTarget.All);
        
        photonView.RPC(nameof(RpcSetNickname), RpcTarget.AllBuffered, PhotonNetwork.NickName);
    }

    private void Update()
    {
        if (!photonView.IsMine) return;
        
        if(Keyboard.current.fKey.wasPressedThisFrame)
            photonView.RPC(nameof(RpcExplode), RpcTarget.All);
    }

    [PunRPC]
    private void RpcSetNickname(string nickname)
    {
        _playerNicknameText.text = nickname;
        _playerNicknameText.color = SpriteRenderer.color;
    }

    public void Move(Vector2 direction)
    {
        Vector2 movement = new Vector2(direction.x, direction.y) * speed;
        _rigidbody2D.linearVelocity = movement;
    }
    
    public void TryShoot()
    {
        if(!photonView.IsMine) return;
        
        if (_laserActive) return;
 
        _laserActive = true;

        Vector2 characterPosition = transform.position;
        Vector2 characterVelocity = _rigidbody2D.linearVelocity;

        photonView.RPC(nameof(RpcShoot), RpcTarget.All,
        characterPosition,
        characterVelocity);           
    }
    
    [PunRPC]
    private void RpcShoot(Vector2 shooterPosition, Vector2 shooterVelocity, PhotonMessageInfo info)
    {
        float lag = Mathf.Max(0f, (float)(PhotonNetwork.Time - info.SentServerTime));

        if (!photonView.IsMine)
            Debug.Log($"RPC Shoot received from {info.Sender.NickName} with lag of: " + lag);

        Vector3 fixedPositionToSpawn = shooterPosition + shooterVelocity * lag;

        Projectile laser = Instantiate(laserPrefab, fixedPositionToSpawn, Quaternion.identity);
        laser.Destroyed += () => _laserActive = false;
        laser.Init(photonView.OwnerActorNr, photonView.IsMine);

        GameManager.Instance.AudioManager.PlaySfx(shootSfx);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(!photonView.IsMine) return;
        
        if (_invincible) return;
                
        if(other.gameObject.layer == LayerMask.NameToLayer("Missile") || other.gameObject.layer == LayerMask.NameToLayer("Invader"))
        {
           photonView.RPC(nameof(RpcExplode), RpcTarget.All);
        }
    }

    [PunRPC]
    public void RpcExplode()
    {                    
        PhotonNetwork.Destroy(gameObject);

        GameManager.Instance.AudioManager.PlaySfx(explosionSfx);
    }

    private void OnDestroy()
    {
        OnPlayerDestroyed?.Invoke(this);
    }

    [PunRPC]
    private void RpcInvincible()
    {
        StartCoroutine(Invincible());
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
