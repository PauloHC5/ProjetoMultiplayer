using Photon.Pun;
using System;
using System.Collections.Generic;
using System.Linq;
using _Game.Scripts;
using Photon.Realtime;
using UnityEngine;
using System.Collections;

public class Projectile : MonoBehaviour
{
    private enum ProjectileDirection
    {
        Up,
        Down
    }
    
    [Header("Projectile Settings")]
    [SerializeField] private ProjectileDirection direction;
    [SerializeField] private float speed;
    [SerializeField] private float projectileTimeToLive = 3f;
    [SerializeField] private float explodeDuration = 0.5f;
    [SerializeField] private Collider2D normalCollider;
    [SerializeField] private Collider2D piercingCollider;
    [SerializeField] private PlayerType instigatorPlayerType = PlayerType.None;
    public float Speed => speed;

    public int InstigatorId { get; private set; }
    public int ShotId { get; private set; }

    private bool _destroyed;
    private Rigidbody2D _rigidbody2D;
    private bool _pierceThroughEnemies = false;
    private int _enemiesLayer;
    private int _enemiesProjectilesLayer;
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    
    public SpriteRenderer SpriteRenderer { get; private set; }
    public Collider2D Collider2D => _pierceThroughEnemies ? piercingCollider : normalCollider;
    
    private int PiercingHash = Animator.StringToHash("Piercing");
    private int ExplodeHasg = Animator.StringToHash("Explode");
    
    private static readonly Dictionary<(int instigatorId, int shotId), Projectile> ProjectileRegistry = new();
    
    public Action<Projectile> Destroyed;    

    private void Awake()
    {
        SpriteRenderer = GetComponent<SpriteRenderer>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _animator = GetComponentInChildren<Animator>();
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        
        _enemiesLayer = LayerMask.NameToLayer("Enemies");
        _enemiesProjectilesLayer = LayerMask.NameToLayer("EnemiesProjectiles");        
    }

    public void Init(int instigatorPlayerId, int shotId, bool pierceThroughEnemies = false, bool doubleSpeed = false)
    {
        InstigatorId = instigatorPlayerId;
        ShotId = shotId;
        _pierceThroughEnemies = pierceThroughEnemies;
        
        if(_pierceThroughEnemies)
        {
            piercingCollider.enabled = true;
            normalCollider.enabled = false;
        }

        _animator.SetBool(PiercingHash, pierceThroughEnemies);

        ProjectileRegistry[(instigatorPlayerId, shotId)] = this;

        if (doubleSpeed)
        {
            speed *= 2f;
        }
        
        ProjectileVelocity();
    }

    private void Start()
    {
        Invoke(nameof(DestroyProjectile), projectileTimeToLive);
    }

    private void ProjectileVelocity()
    {
        var directionToGo = direction == ProjectileDirection.Up ? transform.up : transform.up * -1;
        var direction2D = new Vector2(directionToGo.x, directionToGo.y).normalized;
        
        _rigidbody2D.linearVelocity += direction2D * speed;
    }
    
    public void ChangeSpeed(float newSpeed)
    {
        speed = newSpeed;
        ProjectileVelocity();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(_pierceThroughEnemies && !other.CompareTag("Queen") && (other.gameObject.layer == _enemiesLayer || other.gameObject.layer == _enemiesProjectilesLayer))
            return;

        RequestDestroy();
        Destroyed?.Invoke(this);
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        RequestDestroy();
        Destroyed?.Invoke(this);
    }

    private void RequestDestroy()
    {
        if (_destroyed) return;
        
        DestroyProjectile();
        
        if(PhotonNetwork.IsConnected && ProjectileNetwork.Instance != null)
            ProjectileNetwork.RequestDestroy(InstigatorId, ShotId);
        else
        {
            DestroyRemote(InstigatorId, ShotId);
        }
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
        
        StartCoroutine(DestroyRoutine());
    }

    private IEnumerator DestroyRoutine()
    {
        _animator.SetTrigger(ExplodeHasg);
        _rigidbody2D.linearVelocity = Vector2.zero;
        Collider2D.enabled = false;

        if (instigatorPlayerType != PlayerType.None)
        {
            _spriteRenderer.color = instigatorPlayerType switch
            {
                PlayerType.Player1 => Color.green,
                PlayerType.Player2 => Color.blue,
                PlayerType.Player3 => Color.orangeRed,
                _ => Color.white
            };
        }

        yield return new WaitForSeconds(explodeDuration);

        Destroy(gameObject);
    }
}
