using UnityEngine;
using Fusion;

namespace Starter.Shooter
{
    /// <summary>
    /// Componente de la jaula de atrapado.
    /// Controla únicamente el tiempo de vida del objeto en red.
    /// </summary>
    public class CageTrap : NetworkBehaviour
    {
        /// <summary>
        /// Timer de vida sincronizado por Fusion.
        /// Solo la StateAuthority lo modifica y hace el Despawn.
        /// </summary>
        [Networked]
        private TickTimer LifeTimer { get; set; }

        /// <summary>
        /// Tiempo por defecto de vida de la jaula (en segundos).
        /// </summary>
        [Header("Lifetime")]
        public float DefaultLifetime = 30f;

        /// <summary>
        /// Se llama cuando el objeto es spawneado por Fusion (Runner.Spawn).
        /// </summary>
        public override void Spawned()
        {
            // Solo la StateAuthority controla el ciclo de vida
            if (Object.HasStateAuthority && !LifeTimer.IsRunning)
            {
                LifeTimer = TickTimer.CreateFromSeconds(Runner, DefaultLifetime);
            }
        }

        /// <summary>
        /// Permite ajustar el tiempo de vida desde el que la spawnea.
        /// Solo debe llamarse desde la StateAuthority.
        /// </summary>
        public void SetLifetime(float seconds)
        {
            if (!Object.HasStateAuthority)
                return;

            if (seconds <= 0f)
                seconds = DefaultLifetime;

            LifeTimer = TickTimer.CreateFromSeconds(Runner, seconds);
        }

        /// <summary>
        /// Chequea el timer y destruye la jaula cuando expira.
        /// </summary>
        public override void FixedUpdateNetwork()
        {
            if (Object.HasStateAuthority && LifeTimer.Expired(Runner))
            {
                Runner.Despawn(Object);
            }
        }
    }
}
