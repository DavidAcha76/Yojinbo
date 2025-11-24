using UnityEngine;
using Fusion;

namespace Starter.Shooter
{
    public class CageTrap : NetworkBehaviour
    {
        [Networked]
        private TickTimer LifeTimer { get; set; }

        public float DefaultLifetime = 30f;

        public override void Spawned()
        {
            if (Object.HasStateAuthority && !LifeTimer.IsRunning)
            {
                LifeTimer = TickTimer.CreateFromSeconds(Runner, DefaultLifetime);
            }
        }

        public void SetLifetime(float seconds)
        {
            if (!Object.HasStateAuthority)
                return;

            if (seconds <= 0f)
                seconds = DefaultLifetime;

            LifeTimer = TickTimer.CreateFromSeconds(Runner, seconds);
        }

        public override void FixedUpdateNetwork()
        {
            if (Object.HasStateAuthority && LifeTimer.Expired(Runner))
            {
                Runner.Despawn(Object);
            }
        }
    }
}
