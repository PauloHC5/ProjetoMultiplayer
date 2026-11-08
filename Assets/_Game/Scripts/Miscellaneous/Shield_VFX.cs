using System;
using UnityEngine;

public class Shield_VFX : MonoBehaviour
{
    private enum HitSide {
        Left,
        Right,
        Up,
        Down
    }
    
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private Collider2D _collider2D;
    
    private readonly int HitUpHash = Animator.StringToHash("HitUp");
    private readonly int HitDownHash = Animator.StringToHash("HitDown");
    private readonly int HitLeftHash = Animator.StringToHash("HitLeft");
    private readonly int HitRightHash = Animator.StringToHash("HitRight");
    private readonly int BreakHash = Animator.StringToHash("Break");
    private readonly int AnimMultiplierHash = Animator.StringToHash("AnimMultiplier");

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _collider2D = GetComponent<Collider2D>();
    }
    
    public void RiseShield(float duration)
    {
        _spriteRenderer.enabled = true;
        _animator.enabled = true;
        _collider2D.enabled = true;
        
        _animator.Rebind();
        _animator.Update(0f);
        
        float oneThirdDuration = duration / 3f;
        
        float durationAboutToBreak = duration - oneThirdDuration;
        
        Invoke(nameof(ShieldBreaking), durationAboutToBreak);
    }
    
    private void ShieldBreaking()
    {
        _animator.SetFloat(AnimMultiplierHash, 3f);
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        HitSide hitSide = GetHitSide(other.transform.position, transform.position);
        
        Debug.Log($"Shield hit from {hitSide}");
        
        switch (hitSide)
        {
            case HitSide.Up:
                _animator.SetTrigger(HitUpHash);
                break;
            case HitSide.Down:
                _animator.SetTrigger(HitDownHash);
                break;
            case HitSide.Left:
                _animator.SetTrigger(HitLeftHash);
                break;
            case HitSide.Right:
                _animator.SetTrigger(HitRightHash);
                break;
        }
    }

    
    private HitSide GetHitSide(Vector2 hitterPos, Vector2 targetPos)
    {
        // Direção de onde veio o hit (do alvo para o atacante)
        Vector2 dir = hitterPos - targetPos;

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return dir.x > 0 ? HitSide.Right : HitSide.Left;
        else
            return dir.y > 0 ? HitSide.Up : HitSide.Down;
    }

    
    public void BreakShield()
    {
        _collider2D.enabled = false;
        _animator.SetTrigger(BreakHash);
    }

    public void OnBreak()
    {
        _spriteRenderer.enabled = false;
        _animator.enabled = false;
    }
}
