using System;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace _Game.Scripts.SpaceInvaders
{
    public class PlayerController : MonoBehaviour
    {
        // Every client finds its own controller this way - since this class
        // never exists on remote machines, there's no risk of pointing at
        // someone else's controller.
        public static PlayerController Local { get; private set; }

        [SerializeField]
        private PlayerCharacter possessedPlayerCharacter;

        public bool HasPossessedPlayer => possessedPlayerCharacter != null;

        private void Awake()
        {
            Local = this;
        }

        private void OnDestroy()
        {
            if (Local == this)
            {
                Local = null;
            }
        }

        public void Possess(PlayerCharacter playerCharacter)
        {
            possessedPlayerCharacter = playerCharacter;
        }

        public void UnPossess()
        {
            possessedPlayerCharacter = null;
        }

        private void Update()
        {
            if (possessedPlayerCharacter != null)
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame || Mouse.current.leftButton.wasPressedThisFrame)
                {
                    possessedPlayerCharacter.TryShoot();
                }
            }
            else
            {
                // TODO: menu/spectator input (e.g. navigate options, cycle
                // between teammates' cameras) goes here instead.
            }
        }

        private void FixedUpdate()
        {
            if (possessedPlayerCharacter == null || possessedPlayerCharacter.bBotMode) return;

            Vector2 direction = Vector2.zero;
            
            EightWayMovement(direction);
            //FourWayMovement(direction);
        }
        
        private void EightWayMovement(Vector2 direction)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                direction.x -= 1f;
            }
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                direction.x += 1f;
            }
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            {
                direction.y += 1f;
            }
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            {
                direction.y -= 1f;
            }

            possessedPlayerCharacter.Move(direction);
        }
        
        private void FourWayMovement(Vector2 direction)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                direction.x -= 1f;
            }
            else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                direction.x += 1f;
            }
            else if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            {
                direction.y += 1f;
            }
            else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            {
                direction.y -= 1f;
            }

            possessedPlayerCharacter.Move(direction);
        }
    }
}