using Photon.Pun;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements.Experimental;
using static UnityEditor.PlayerSettings;

public class MosquitoDeathBehaviour : MonoBehaviour
{
    public Sprite deadSprite;
    public float _fallDuration = 0.45f;
    public float _fallDistance = 0.6f;
    public float _movementAmplitude = 2f;
    public float _deathDuration = 5f;
    public float _swayAmplitude = 0.25f;
    public float _swayCycles = 2f;
    public float _blinkDuration = 0.1f;

    private SpriteRenderer _spriteRenderer;
    private Animator _animator;
    private Collider2D _collider2D;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _animator = GetComponent<Animator>();
        _collider2D = GetComponent<Collider2D>();
    }

    private void Start()
    {        
        _animator.enabled = false;
        _collider2D.enabled = false;
        

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {        
        Vector3 start = transform.localPosition;
        Vector3 end = start + Vector3.down * _fallDistance;
        float dir = Random.value < 0.5f ? -1f : 1f;

        for (float t = 0f; t < _fallDuration; t += Time.deltaTime)
        {
            float t01 = t / _fallDuration;
            float k = Easing.InOutBack(t01);
            k = EasyCurves.Amplify(k, t01, _movementAmplitude);
            Vector3 pos = Vector3.LerpUnclamped(start, end, k);
            
            float sway = Mathf.Sin(t01 * _swayCycles * Mathf.PI * 2f) * _swayAmplitude * dir;
            pos.x += sway;

            transform.localPosition = pos;

            yield return null;
        }

        transform.localPosition = end;

        //_spriteRenderer.sprite = deadSprite;

        yield return new WaitForSeconds(1.5f);

        StartCoroutine(BlinkEffect());

        yield return new WaitForSeconds(_deathDuration);

        StopCoroutine(BlinkEffect());

        _spriteRenderer.enabled = true;

        gameObject.SetActive(false);
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
}
