using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Invaders : MonoBehaviour
{
    public Invader[] prefabs;
    public int rows = 5;
    public int columns = 11;
    public AnimationCurve speed;
    public float missileAttackRate = 1.0f;
    public Projectile missilePrefab;
    
    public int AmountKilled { get; private set; }
    public int TotalInvaders => this.rows * this.columns;
    public float PercertKilled => (float)this.AmountKilled / (float)this.TotalInvaders;
    public int AmountAlive => this.TotalInvaders - this.AmountKilled;

    private Vector3 _direction = Vector3.right;
    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main;
        
        for (int row = 0; row < this.rows; row++)
        {
            float width = 2.0f * (this.columns - 1);
            float height = 2.0f * (this.rows - 1);
            Vector2 centering = new Vector2(-width / 2, -height / 2);
            Vector3 rowPosition = new Vector3(centering.x, centering.y + (row * 2.0f), 0.0f);
            
            for(int col = 0; col < this.columns; col++)
            {
                Invader invader = Instantiate(this.prefabs[row], this.transform);
                Vector3 position = rowPosition;
                position.x += col * 2.0f;
                invader.transform.localPosition = position;
                invader.killed += InvaderKilled;
            }
        }
    }

    private void Start()
    {
        InvokeRepeating(nameof(MissileAttack), missileAttackRate, missileAttackRate);
    }

    private void FixedUpdate()  
    {
        this.transform.position += _direction * (this.speed.Evaluate(PercertKilled) * Time.fixedDeltaTime);

        Vector3 leftEdge = _mainCamera.ViewportToWorldPoint(Vector3.zero);
        Vector3 rightEdge = _mainCamera.ViewportToWorldPoint(Vector3.right);
        
        foreach (Transform invader in this.transform)
        {
            if(!invader.gameObject.activeInHierarchy) continue;

            if (_direction == Vector3.right && invader.position.x >= (rightEdge.x - 1.0f))
            {
                AdvanceRow();
            }
            else if (_direction == Vector3.left && invader.position.x <= (leftEdge.x + 1.0f))
            {
                AdvanceRow();
            }
                
        }
    }
    
    private void InvaderKilled()
    {
        AmountKilled++;
        
        if(AmountKilled >= TotalInvaders)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
    
    private void AdvanceRow()
    {
        _direction.x *= -1.0f;
        
        Vector3 position = this.transform.position;
        position.y -= 1.0f;
        this.transform.position = position;
    }
    
    private void MissileAttack()
    {
        foreach (Invader invader in this.GetComponentsInChildren<Invader>())
        {
            if(!invader.gameObject.activeInHierarchy) continue;

            if (UnityEngine.Random.value < (1f / (float)AmountAlive))
            {
                Instantiate(this.missilePrefab, invader.transform.position, Quaternion.identity);
                break;
            }
        }
    }
}
