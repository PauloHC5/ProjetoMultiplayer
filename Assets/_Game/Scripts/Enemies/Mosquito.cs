using System;
using System.Collections;
using _Game.Scripts.Enemies;
using UnityEngine;

public enum MosquitoType
{
    Normal,
    Infected,
    Queen
}

public class Mosquito : MosquitoBase
{
    public float scaleUpDuration = 0.5f;
    
    private Vector3 _originalScale;
    private int _defaultLayer;
    
    [HideInInspector]
    public MosquitosHorde.HordePositionKey hordePositionKey;
    
    public event Action OnMosquitoHitBoundaries;
    public event Action<Mosquito, int> OnMosquitoKilled;
    
    protected override void Awake()
    {
        base.Awake();
        
        _defaultLayer = LayerMask.NameToLayer("Default");
        _originalScale = transform.localScale;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(other.gameObject.layer == ProjectilesLayer)
        {
            var laser = other.GetComponent<Projectile>();
            
            OnMosquitoKilled?.Invoke(this, laser.InstigatorId);
        }
        
        if(other.gameObject.layer == _defaultLayer)
        {
            OnMosquitoHitBoundaries?.Invoke();
        }
        
        if(other.gameObject.layer == BarrierLayer)
        {
            OnMosquitoKilled?.Invoke(this, -1);
            BarriersManager.Instance.DamageBarrier(other.gameObject);
        }
    }

    private IEnumerator ScaleFrom0T1Interpolation()
    {
        Collider2D.enabled = false;
        
        float elapsedTime = 0f;

        transform.localScale = Vector3.zero;

        while (elapsedTime < scaleUpDuration)
        {
            float t = elapsedTime / scaleUpDuration;
            transform.localScale = Vector3.Lerp(Vector3.zero, _originalScale, Mathf.SmoothStep(0f, 1f, t));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.localScale = _originalScale;
        Collider2D.enabled = true;
    }

    private void OnEnable()
    {
        StartCoroutine(ScaleFrom0T1Interpolation());
    }
}
