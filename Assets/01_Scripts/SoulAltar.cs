using UnityEngine;

namespace Starter.Shooter
{
    /// <summary>
    /// Simple altar where players can deposit the souls they carry.
    /// Player checks distance + E key + hold time.
    /// </summary>
    public class SoulAltar : MonoBehaviour
    {
        [Header("Interaction")]
        [Tooltip("Radius in world units within which the player can start depositing souls.")]
        public float InteractionRadius = 3f;

        [Tooltip("Seconds holding the interact key (E) to complete the deposit.")]
        public float HoldTimeToDeposit = 6f;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, InteractionRadius);
        }
    }
}
