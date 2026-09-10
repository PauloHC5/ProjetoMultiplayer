using System;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Game.Scripts.SpaceInvaders
{
    public class PlayerController : MonoBehaviour
    {
        // Every client finds its own controller this way - since this class
        // never exists on remote machines, there's no risk of pointing at
        // someone else's controller.
        public static PlayerController Local { get; private set; }

        private PlayerCharacter _possessedPlayerCharacter;

        public bool HasPossessedPlayer => _possessedPlayerCharacter != null;

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
            _possessedPlayerCharacter = playerCharacter;
        }

        public void UnPossess()
        {
            _possessedPlayerCharacter = null;
        }

        private void Update()
        {
            if (_possessedPlayerCharacter != null)
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame || Mouse.current.leftButton.wasPressedThisFrame)
                {
                    _possessedPlayerCharacter.TryShoot();
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
            if (_possessedPlayerCharacter == null) return;

            Vector2 direction = Vector2.zero;

            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                direction = Vector2.left;
            else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                direction = Vector2.right;
            
            _possessedPlayerCharacter.Move(direction);
        }
    }
}