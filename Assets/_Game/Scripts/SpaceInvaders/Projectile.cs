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

    public int InstigatorId { get; private set; }
    public int ShotId { get; private set; }

    public Action<Projectile> Destroyed;
    private bool _destroyed;
    
    public SpriteRenderer SpriteRenderer { get; private set; }
    public Collider2D Collider2D { get; private set; }

    private Rigidbody2D _rigidbody2D;
    
    private static readonly Dictionary<(int instigatorId, int shotId), Projectile> ProjectileRegistry = new();

    private void Awake()
    {
        SpriteRenderer = GetComponent<SpriteRenderer>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
        Collider2D = GetComponent<Collider2D>();
    }

    public void Init(int instigatorPlayerId, int shotId)
    {
        InstigatorId = instigatorPlayerId;
        ShotId = shotId;

        ProjectileRegistry[(instigatorPlayerId, shotId)] = this;
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
        
        if(PhotonNetwork.IsConnected)
            ProjectileNetwork.RequestDestroy(InstigatorId, ShotId);
    }
    
    public static void DestroyRemote(int instigatorId, int shotId)
    {
        if (ProjectileRegistry.TryGetValue((instigatorId, shotId), out var p) && p != null)
        {
            p.DestroyProjectile();
        }
    }

    public void DestroyProjectile()
    {
        if (_destroyed) return;
        _destroyed = true;
 
        var shotId = ProjectileRegistry.FirstOrDefault(x => x.Value == this).Key.shotId;
        ProjectileRegistry.Remove((InstigatorId, ShotId));
 
        Destroyed?.Invoke(this);
        Destroy(gameObject);
    }
}
