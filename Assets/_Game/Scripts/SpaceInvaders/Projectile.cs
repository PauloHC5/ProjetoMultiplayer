using Photon.Pun;
using System;
using UnityEngine;

public class Projectile : MonoBehaviourPun
{
    public Vector3 direction;
    
    public float speed;

    public Action Destroyed;
    
    public SpriteRenderer SpriteRenderer { get; private set; }

    private void Awake()
    {
        SpriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        Invoke(nameof(DestroyProjectile), 3f);
    }

    private void FixedUpdate()
    {
        this.transform.position += this.direction * (this.speed * Time.fixedDeltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        DestroyProjectile();
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        DestroyProjectile();
    }

    private void DestroyProjectile()
    {
        Destroyed?.Invoke();
        Destroy(this.gameObject);
    }
}
