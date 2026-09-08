using System;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Game.Scripts.SpaceInvaders
{
    [RequireComponent(typeof(PhotonView))]
    public class PlayerController : MonoBehaviourPun
    {
        [HideInInspector]
        public Player PossessedPlayer;

        private void Update()
        {
            if(!photonView.IsMine || PossessedPlayer == null)
                return;
            
            if (Keyboard.current.spaceKey.wasPressedThisFrame || Mouse.current.leftButton.wasPressedThisFrame)
            {
                PossessedPlayer.TryShoot();
            }
        }
        
        private void FixedUpdate()
        {
            if (!photonView.IsMine) return;
 
            Vector2 direction = Vector2.zero;
 
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                direction = Vector2.left;
            else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                direction = Vector2.right;
 
            PossessedPlayer.Move(direction);
        }
    }
}