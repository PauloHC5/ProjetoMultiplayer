using System;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public Vector3 direction;
    
    public float speed;

    public Action destroyed;

    private void FixedUpdate()
    {
        this.transform.position += this.direction * (this.speed * Time.fixedDeltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        destroyed?.Invoke();
        Destroy(this.gameObject);
    }
}
