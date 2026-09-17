using Photon.Pun;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    public Vector2 direction;
    public float speed;

    public bool IsLocalProjectile { get; private set; }
    public int InstigatorId { get; private set; }
    public int ShotId { get; private set; }

    public Action Destroyed;
    private bool _destroyed;
    
    public SpriteRenderer SpriteRenderer { get; private set; }

    private Rigidbody2D _rigidbody2D;
    
    private static readonly Dictionary<(int instigatorId, int shotId), Projectile> _projectileRegistry = new();

    private void Awake()
    {
        SpriteRenderer = GetComponent<SpriteRenderer>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
    }

    public void Init(int instigatorPlayerId, bool isLocalProjectile, int shotId)
    {
        InstigatorId = instigatorPlayerId;
        IsLocalProjectile = isLocalProjectile;
        ShotId = shotId;

        _projectileRegistry[(instigatorPlayerId, shotId)] = this;
    }

    private void Start()
    {
        _rigidbody2D.linearVelocity += this.direction * this.speed;

        Invoke(nameof(DestroyProjectile), 3f);
    }    

    private void OnTriggerEnter2D(Collider2D other)
    {
        RequestDestroy();
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        RequestDestroy();
    }

    private void RequestDestroy()
    {
        if (_destroyed) return;
        
        DestroyProjectile();
        ProjectileNetwork.RequestDestroy(InstigatorId, ShotId);
    }
    
    public static void DestroyRemote(int instigatorId, int shotId)
    {
        if (_projectileRegistry.TryGetValue((instigatorId, shotId), out var p) && p != null)
        {
            p.DestroyProjectile();
        }
    }

    public void DestroyProjectile()
    {
        if (_destroyed) return;
        _destroyed = true;
 
        var shotId = _projectileRegistry.FirstOrDefault(x => x.Value == this).Key.shotId;
        _projectileRegistry.Remove((InstigatorId, ShotId));
 
        Destroyed?.Invoke();
        Destroy(gameObject);
    }
}
