using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class Player : MonoBehaviour
{
    public Projectile laserPrefab;
    public float speed = 5.0f;
    public AudioCue shootSfx;
        
    private bool _laserActive;

    private void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame || Mouse.current.leftButton.wasPressedThisFrame)
        {
            Shoot();
        }
    }

    private void FixedUpdate()
    {
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
        {
            transform.position += Vector3.left * (speed * Time.fixedDeltaTime);
        }
        else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
        {
            transform.position += Vector3.right * (speed * Time.fixedDeltaTime);
        }
    }
    
    private void Shoot()
    {
        if(!_laserActive){
            Projectile projectile = Instantiate(laserPrefab, transform.position, Quaternion.identity);
            projectile.destroyed += () => _laserActive = false;
            _laserActive = true;
            GameManager.Instance.AudioManager.PlaySfx(shootSfx);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(other.gameObject.layer == LayerMask.NameToLayer("Missile") || other.gameObject.layer == LayerMask.NameToLayer("Invader"))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
