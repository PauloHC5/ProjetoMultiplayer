using System;
using System.Collections;
using System.Collections.Generic;
using _Game.Scripts;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Player), typeof(PhotonView), typeof(Rigidbody2D))]
public class Player : MonoBehaviourPun
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
    
    public Action OnPlayerDestroyed;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
        
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
        StartCoroutine(Invincible());
    }

    public void Move(Vector2 direction)
    {
        Vector3 movement = new Vector3(direction.x, direction.y, 0) * speed * Time.deltaTime;
        _rigidbody2D.linearVelocity = movement;
    }
    
    public void TryShoot()
    {
        if (_laserActive) return;
 
        _laserActive = true;
 
        // Called only on the owner. PhotonNetwork.Instantiate replicates
        // the projectile to every client and assigns ownership to us.
        GameObject go = PhotonNetwork.Instantiate(laserPrefab.name, transform.position, Quaternion.identity);
        Projectile projectile = go.GetComponent<Projectile>();
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
