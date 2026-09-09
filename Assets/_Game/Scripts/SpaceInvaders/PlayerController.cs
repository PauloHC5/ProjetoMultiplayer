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

        private Player _possessedPlayer;

        public bool HasPossessedPlayer => _possessedPlayer != null;

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

        public void Possess(Player player)
        {
            _possessedPlayer = player;
        }

        public void UnPossess()
        {
            _possessedPlayer = null;
        }

        private void Update()
        {
            if (_possessedPlayer != null)
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame || Mouse.current.leftButton.wasPressedThisFrame)
                {
                    _possessedPlayer.TryShoot();
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
            if (_possessedPlayer == null) return;

            Vector2 direction = Vector2.zero;

            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                direction = Vector2.left;
            else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                direction = Vector2.right;
            
            _possessedPlayer.Move(direction);
        }
    }
}