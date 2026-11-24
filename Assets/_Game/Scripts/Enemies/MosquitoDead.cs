using System;
using System.Collections;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.UIElements.Experimental;
using Random = UnityEngine.Random;

public class MosquitoDead : MonoBehaviour
{
    public Sprite deadSprite;
    public bool bEnableBehaviourMovement = true;
    public bool bSelfDestructOnDeath = false;
    public float _deathDuration = 5f;
    public float _blinkDuration = 0.1f;
    
    [ShowIf("bEnableBehaviourMovement")]
    public float _fallDuration = 0.45f;
    
    [ShowIf("bEnableBehaviourMovement")]
    public float _fallDistance = 0.6f;
    
    [ShowIf("bEnableBehaviourMovement")]
    public float _swayAmplitude = 0.25f;

    [ShowIf("bEnableBehaviourMovement")]
    public float _swayCycles = 2f;
    
    
    [Header("Settings")]
    [SerializeField] private Material onDamageMaterial;
    [SerializeField] private float onDamageVfxDuration = 0.2f;

    private SpriteRenderer _spriteRenderer;
    private Animator _animator;
    private Material _originalMaterial;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if(_spriteRenderer == null)
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        
        _animator = GetComponent<Animator>();
        if(_animator == null)
            _animator = GetComponentInChildren<Animator>();
        _originalMaterial = _spriteRenderer.material;
    }

    private void OnEnable()
    {        
        _animator.enabled = true;
        _spriteRenderer.enabled = true;
        _spriteRenderer.material = _originalMaterial;
        StartCoroutine(DeathRoutine());
        StartCoroutine(OnDamageVFXRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        if (bEnableBehaviourMovement)
        {
            Vector3 start = transform.localPosition;
            Vector3 end = start + Vector3.down * _fallDistance;
            float dir = Random.value < 0.5f ? -1f : 1f;

            for (float t = 0f; t < _fallDuration; t += Time.deltaTime)
            {
                float t01 = t / _fallDuration;
                float k = EasyCurves.InBack(t01);
                Vector3 pos = Vector3.LerpUnclamped(start, end, k);

                float sway = Mathf.Sin(t01 * _swayCycles * Mathf.PI * 2f) * _swayAmplitude * dir;
                pos.x += sway;

                transform.localPosition = pos;

                yield return null;
            }
            transform.localPosition = end;
        }

        _animator.enabled = false;
        _spriteRenderer.sprite = deadSprite;

        yield return new WaitForSeconds(1.5f);

        StartCoroutine(BlinkEffect());

        yield return new WaitForSeconds(_deathDuration);

        StopCoroutine(BlinkEffect());
        if (bSelfDestructOnDeath)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private IEnumerator BlinkEffect()
    {        
        _spriteRenderer.enabled = true;

        float elapsedTime = 0.0f;
        while (true)
        {
            _spriteRenderer.enabled = !_spriteRenderer.enabled;

            yield return new WaitForSeconds(_blinkDuration);
            elapsedTime += 0.2f;
        }
    }
    
    private IEnumerator OnDamageVFXRoutine()
    {
        _spriteRenderer.material = onDamageMaterial;
        yield return new WaitForSeconds(onDamageVfxDuration);
        _spriteRenderer.material = _originalMaterial;
    }
}
