using UnityEngine;

namespace Starter.Shooter
{
    public struct GameplayInput
    {
        public Vector2 LookRotation;
        public Vector2 MoveDirection;
        public bool Jump;
        public bool Fire;
        public bool AltFire;
        public bool Interact;
        public bool Transform;
        public bool Reload;
        public bool SpecialFire;
        public bool Invisibility;
        public bool Heal;
        public bool Cage;
    }

    public sealed class PlayerInput : MonoBehaviour
    {
        public GameplayInput CurrentInput => _input;
        private GameplayInput _input;

        public void ResetInput()
        {
            _input.MoveDirection = default;
            _input.Jump = false;
            _input.Fire = false;
            _input.AltFire = false;
            _input.Interact = false;
            _input.Transform = false;
            _input.Reload = false;
            _input.SpecialFire = false;
            _input.Invisibility = false;
            _input.Heal = false;
            _input.Cage = false;
        }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            _input.LookRotation += new Vector2(-Input.GetAxisRaw("Mouse Y"), Input.GetAxisRaw("Mouse X"));

            var moveDirection = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            _input.MoveDirection = moveDirection.normalized;

            _input.Fire |= Input.GetButtonDown("Fire1");
            _input.AltFire |= Input.GetButtonDown("Fire2");
            _input.Jump |= Input.GetButtonDown("Jump");
            _input.Interact |= Input.GetKey(KeyCode.E);
            _input.Transform |= Input.GetKeyDown(KeyCode.G);
            _input.Reload |= Input.GetKeyDown(KeyCode.R);
            _input.SpecialFire |= Input.GetKeyDown(KeyCode.T);
            _input.Invisibility |= Input.GetKeyDown(KeyCode.F);
            _input.Heal |= Input.GetKeyDown(KeyCode.Q);
            _input.Cage |= Input.GetKeyDown(KeyCode.C);
        }
    }
}
