using System.Collections;
using UnityEngine;

public class Invader : MonoBehaviour
{
    public int invaderIndex;
    public int points;
    public Sprite[] animationSprites;
    public float animationTime;
    
    private SpriteRenderer _spriteRenderer;
    private int _animationFrame;
    private Invaders _invaders;
    private bool _hitPredicted;
    
    private int _laserLayer;
    private int _defaultLayer;
    
    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _invaders = GetComponentInParent<Invaders>();
        
        _laserLayer = LayerMask.NameToLayer("Laser");
        _defaultLayer = LayerMask.NameToLayer("Default");
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
        if(other.gameObject.layer == _laserLayer)
        {
            var laser = other.GetComponent<Projectile>();
            
            if(!laser.IsLocalProjectile) return;
            
            _invaders.RequestKill(invaderIndex, laser.InstigatorPlayerId);
        }
        
        if(other.gameObject.layer == _defaultLayer)
        {
            _invaders.HitBoundaries();
        }
    }
    
    public void DeactivateDelay()
    {
        _spriteRenderer.enabled = false;

        // Mantém o collider vivo por mais um instante: dá tempo pra lasers
        // cosméticos ainda em voo em outros clientes colidarem e se
        // autodestruírem naturalmente, antes de desativar o objeto inteiro.
        StartCoroutine(DeactivateAfterGracePeriod());
    }
    
    private IEnumerator DeactivateAfterGracePeriod()
    {
        yield return new WaitForSeconds(0.5f); // cobre RTT esperado + margem
        gameObject.SetActive(false);
    }
}
