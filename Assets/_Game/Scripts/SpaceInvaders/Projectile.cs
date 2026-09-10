using Photon.Pun;
using System;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class Projectile : MonoBehaviourPun
{
    public Vector3 direction;
    
    public float speed;

    public Action destroyed;
    
    public SpriteRenderer SpriteRenderer { get; private set; }

    private void Awake()
    {
        SpriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void FixedUpdate()
    {
        this.transform.position += this.direction * (this.speed * Time.fixedDeltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(!photonView.IsMine) return;
        
        destroyed?.Invoke();
        PhotonNetwork.Destroy(this.gameObject);
    }
}
