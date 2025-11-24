using Fusion;
using UnityEngine;

namespace Starter.Shooter
{
    public class Chicken : NetworkBehaviour
    {
        [Header("References")]
        public Health Health;
        public NetworkTransform NetworkTransform;
        public ParticleSystem FlyParticles;

        [Networked]
        private Vector3 _startPosition { get; set; }
        [Networked]
        private float _speed { get; set; }
        [Networked]
        private float _maxTravelDistance { get; set; }

        public void Respawn(Vector3 position, Quaternion rotation, float speed, float maxTravelDistance)
        {
            Health.Revive();

            _startPosition = position;
            _speed = speed;
            _maxTravelDistance = maxTravelDistance;

            NetworkTransform.Teleport(position, rotation);
        }

        public override void FixedUpdateNetwork()
        {
            if (Health.IsAlive == false)
                return;

            if (Vector3.Distance(_startPosition, transform.position) > _maxTravelDistance)
            {
                Health.TakeHit(1000);
                return;
            }

            float dt = GameManager.GetWorldDeltaTime(Runner);
            transform.Translate(Vector3.forward * _speed * dt, Space.Self);
        }

        public override void Render()
        {
            var emission = FlyParticles.emission;
            emission.enabled = Health.IsAlive;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (HasStateAuthority == false)
                return;

            Health.TakeHit(1000);
        }
    }
}
