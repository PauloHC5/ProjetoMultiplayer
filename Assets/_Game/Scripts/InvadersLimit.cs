using System;
using Unity.VisualScripting;
using UnityEngine;

public class InvadersLimit : MonoBehaviour
{
    public static event Action OnInvadersLimitReached;
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        OnInvadersLimitReached?.Invoke();
    }
}
