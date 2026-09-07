using System;
using System.Collections;
using System.Collections.Generic;
using _Game.Scripts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class Player : MonoBehaviour
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
    
    public Action OnPlayerDestroyed;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        StartCoroutine(Invincible());
    }

    private void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame || Mouse.current.leftButton.wasPressedThisFrame)
        {
            Shoot();
        }
    }

    private void FixedUpdate()
    {
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
        {
            transform.position += Vector3.left * (speed * Time.fixedDeltaTime);
        }
        else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
        {
            transform.position += Vector3.right * (speed * Time.fixedDeltaTime);
        }
    }
    
    private void Shoot()
    {
        if(!_laserActive){
            Projectile projectile = Instantiate(laserPrefab, transform.position, Quaternion.identity);
            projectile.destroyed += () => _laserActive = false;
            _laserActive = true;
            GameManager.Instance.AudioManager.PlaySfx(shootSfx);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(other.gameObject.layer == LayerMask.NameToLayer("Missile") || other.gameObject.layer == LayerMask.NameToLayer("Invader"))
        {
            if (_invincible) return;
            
            OnPlayerDestroyed?.Invoke();
            GameManager.Instance.AudioManager.PlaySfx(explosionSfx);
            Destroy(gameObject);
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
