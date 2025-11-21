using UnityEngine;

namespace Starter.Shooter
{
    /// <summary>
    /// Altar donde los jugadores depositan las almas.
    /// Se encarga de inicializar la UI de progreso y exponer un método
    /// para actualizarla.
    /// </summary>
    public class SoulAltar : MonoBehaviour
    {
        [Header("Interaction")]
        [Tooltip("Radio dentro del cual el jugador puede empezar a canalizar almas.")]
        public float InteractionRadius = 3f;

        [Tooltip("Segundos que se debe mantener E para completar el depósito.")]
        public float HoldTimeToDeposit = 6f;

        private SoulDepositProgressUI _progressUI;

        private void Awake()
        {
            // Creamos o recuperamos la UI global para el depósito
            _progressUI = SoulDepositProgressUI.GetOrCreateInstance();
        }

        /// <summary>
        /// Llamada desde el Player para actualizar la UI del depósito.
        /// </summary>
        /// <param name="visible">Si la barra debe mostrarse o esconderse.</param>
        /// <param name="progress">Progreso 0..1 del canalizado.</param>
        /// <param name="remainingSeconds">Segundos restantes para completar.</param>
        public void UpdateDepositUI(bool visible, float progress, float remainingSeconds)
        {
            if (_progressUI == null)
                return;

            if (!visible)
            {
                _progressUI.Hide();
            }
            else
            {
                _progressUI.Show(progress, remainingSeconds);
            }
        }
    }
}
