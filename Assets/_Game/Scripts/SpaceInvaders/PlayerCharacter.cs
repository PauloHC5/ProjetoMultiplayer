using System;
using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;

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
        
    private bool _laserActive;
    private bool _invincible;
    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D _rigidbody2D;
    private TextMeshProUGUI _playerNicknameText;
    
    public Action OnPlayerDestroyed;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
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
    
    [PunRPC]
    private void RpcSetNickname(string nickname)
    {
        _playerNicknameText.text = nickname;
        _playerNicknameText.color = _spriteRenderer.color;
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
 
        // Called only on the owner. PhotonNetwork.Instantiate replicates
        // the projectile to every client and assigns ownership to us.
        GameObject go = PhotonNetwork.Instantiate(laserPrefab.name, transform.position, Quaternion.identity);
        Projectile projectile = go.GetComponent<Projectile>();
        projectile.SpriteRenderer.color = _spriteRenderer.color;
        projectile.destroyed += () => _laserActive = false;
 
        GameManager.Instance.AudioManager.PlaySfx(shootSfx);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(!photonView.IsMine) return;
        
        if (_invincible) return;
        
        // Every client's physics evaluates this trigger for every Player
        // instance in the scene. Without this guard, all 3 clients would
        // each try to resolve this player's death independently.
        if(other.gameObject.layer == LayerMask.NameToLayer("Missile") || other.gameObject.layer == LayerMask.NameToLayer("Invader"))
        {
            photonView.RPC(nameof(RpcExplode), RpcTarget.All);
        }
    }

    [PunRPC]
    private void RpcExplode()
    {
        // Runs on every client: everyone hears/sees the explosion.
        GameManager.Instance.AudioManager.PlaySfx(explosionSfx);
 
        if (photonView.IsMine)
        {
            OnPlayerDestroyed?.Invoke();
            PhotonNetwork.Destroy(gameObject);
        }
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
            _spriteRenderer.enabled = isVisible;

            yield return new WaitForSeconds(0.2f);
            elapsedTime += 0.2f;
        }

        _spriteRenderer.enabled = true;
        _invincible = false;
    }
    
}
