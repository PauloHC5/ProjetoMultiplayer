using System.Collections;
using _Game.Scripts;
using UnityEngine;

public class Invader : MonoBehaviour
{
    
    [HideInInspector] public int invaderIndex;
    public int points;
    
    private SpriteRenderer _spriteRenderer;
    private int _animationFrame;
    private bool _hitPredicted;
    
    private int _laserLayer;
    private int _defaultLayer;
    
    public event System.Action OnInvaderHitBoundaries;
    public event System.Action<int, int> OnInvaderKilled;
    
    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        
        _laserLayer = LayerMask.NameToLayer("Laser");
        _defaultLayer = LayerMask.NameToLayer("Default");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(other.gameObject.layer == _laserLayer)
        {
            var laser = other.GetComponent<Projectile>();
            
            OnInvaderKilled?.Invoke(invaderIndex, laser.InstigatorId);
        }
        
        if(other.gameObject.layer == _defaultLayer)
        {
            OnInvaderHitBoundaries?.Invoke();
        }
    }
}
