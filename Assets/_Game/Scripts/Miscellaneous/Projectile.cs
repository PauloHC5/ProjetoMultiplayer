using Photon.Pun;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    private enum ProjectileDirection
    {
        Up,
        Down
    }
    
    [Header("Projectile Settings")]
    [SerializeField] private ProjectileDirection direction;
    public float speed;

    public int InstigatorId { get; private set; }
    public int ShotId { get; private set; }

    private bool _destroyed;
    private Rigidbody2D _rigidbody2D;
    private int _enemiesLayer;
    private bool _pierceThroughEnemies = false;
    
    public SpriteRenderer SpriteRenderer { get; private set; }
    public Collider2D Collider2D { get; private set; }
    
    
    private static readonly Dictionary<(int instigatorId, int shotId), Projectile> ProjectileRegistry = new();
    
    public Action<Projectile> Destroyed;

    private void Awake()
    {
        SpriteRenderer = GetComponent<SpriteRenderer>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
        Collider2D = GetComponent<Collider2D>();
        
        _enemiesLayer = LayerMask.NameToLayer("Enemies");
    }

    public void Init(int instigatorPlayerId, int shotId, bool pierceThroughEnemies = false, bool doubleSpeed = false)
    {
        InstigatorId = instigatorPlayerId;
        ShotId = shotId;
        _pierceThroughEnemies = pierceThroughEnemies;

        ProjectileRegistry[(instigatorPlayerId, shotId)] = this;

        if (doubleSpeed)
        {
            speed *= 2f;
        }
        
        ProjectileVelocity();
    }

    private void Start()
    {
        Invoke(nameof(DestroyProjectile), 3f);
    }

    private void ProjectileVelocity()
    {
        var directionToGo = direction == ProjectileDirection.Up ? transform.up : transform.up * -1;
        var direction2D = new Vector2(directionToGo.x, directionToGo.y).normalized;
        
        _rigidbody2D.linearVelocity += direction2D * speed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(_pierceThroughEnemies && other.gameObject.layer == _enemiesLayer)
            return;
        
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
