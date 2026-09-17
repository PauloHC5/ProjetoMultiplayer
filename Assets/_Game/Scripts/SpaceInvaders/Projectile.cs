using Photon.Pun;
using System;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    public Vector2 direction;
    public float speed;

    public bool IsLocalProjectile { get; private set; }
    public int InstigatorPlayerId { get; private set; }

    public Action Destroyed;
    
    public SpriteRenderer SpriteRenderer { get; private set; }

    private Rigidbody2D _rigidbody2D;

    private void Awake()
    {
        SpriteRenderer = GetComponent<SpriteRenderer>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
    }

    public void Init(int instigatorPlayerId, bool isLocalProjectile)
    {
        InstigatorPlayerId = instigatorPlayerId;
        IsLocalProjectile = isLocalProjectile;
    }

    private void Start()
    {
        _rigidbody2D.linearVelocity += this.direction * this.speed;

        Invoke(nameof(DestroyProjectile), 3f);
    }    

    private void OnTriggerEnter2D(Collider2D other)
    {
        DestroyProjectile();
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        DestroyProjectile();
    }

    public void DestroyProjectile()
    {
        Destroyed?.Invoke();
        Destroy(this.gameObject);
    }
}
