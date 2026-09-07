using System;
using _Game.Scripts;
using UnityEngine;

public class Invader : MonoBehaviour
{
    public int points;
    public Sprite[] animationSprites;
    public float animationTime;
    public AudioCue killedSfx;
    
    private SpriteRenderer _spriteRenderer;
    private int _animationFrame;
    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        InvokeRepeating(nameof(AnimateSprite), this.animationTime, this.animationTime);
    }

    private void AnimateSprite()
    {
        _animationFrame++;
        if (_animationFrame >= this.animationSprites.Length)
        {
            _animationFrame = 0;
        }
        _spriteRenderer.sprite = this.animationSprites[_animationFrame];
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(other.gameObject.layer == LayerMask.NameToLayer("Laser"))
        {
            GameMode.OnInvaderKilled?.Invoke(this.points);
            GameManager.Instance.AudioManager.PlaySfx(killedSfx);
            gameObject.SetActive(false);
        }
    }
}
