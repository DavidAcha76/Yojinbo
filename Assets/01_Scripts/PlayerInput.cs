using UnityEngine;

namespace Starter.Shooter
{
    /// <summary>
    /// Structure holding player input.
    /// </summary>
    public struct GameplayInput
    {
        public Vector2 LookRotation;
        public Vector2 MoveDirection;
        public bool Jump;
        public bool Fire;      // Disparo normal (pistola)
        public bool AltFire;   // Disparo alternativo (garfio)
        public bool Interact;  // E (altar)
    }

    /// <summary>
    /// PlayerInput handles accumulating player input from Unity.
    /// </summary>
    public sealed class PlayerInput : MonoBehaviour
    {
        public GameplayInput CurrentInput => _input;
        private GameplayInput _input;

        public void ResetInput()
        {
            // Reset input after it was used to detect changes correctly again
            _input.MoveDirection = default;
            _input.Jump = false;
            _input.Fire = false;
            _input.AltFire = false;
            _input.Interact = false;
        }

        private void Update()
        {
            // Accumulate input only if the cursor is locked.
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            // Mirar
            _input.LookRotation += new Vector2(-Input.GetAxisRaw("Mouse Y"), Input.GetAxisRaw("Mouse X"));

            // Movimiento
            var moveDirection = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            _input.MoveDirection = moveDirection.normalized;

            // Disparo primario (pistola normal)
            _input.Fire |= Input.GetButtonDown("Fire1");   // click izquierdo

            // Disparo alternativo (garfio)
            _input.AltFire |= Input.GetButtonDown("Fire2"); // click derecho

            // Saltar
            _input.Jump |= Input.GetButtonDown("Jump");

            // Interacción (altar)
            _input.Interact |= Input.GetKey(KeyCode.E);
        }
    }
}
