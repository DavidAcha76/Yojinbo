using UnityEngine;

namespace Starter.Shooter
{
    /// <summary>
    /// Zona de arena:
    /// - Restringe a otros Players (no al dueño) a no salir del radio.
    /// - Lógica se ejecuta solo en la autoridad del dueño (para no pelear con otros runners).
    /// Visual:
    /// - El "efecto oscuro" lo haces con el material del MeshRenderer del hijo (color oscuro + alpha).
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class ArenaZone : MonoBehaviour
    {
        private Player _owner;
        private float _radius;

        public void Initialize(Player owner)
        {
            _owner = owner;

            var col = GetComponent<SphereCollider>();
            if (col != null)
            {
                float maxScale = Mathf.Max(
                    transform.lossyScale.x,
                    Mathf.Max(transform.lossyScale.y, transform.lossyScale.z)
                );
                _radius = col.radius * maxScale;
            }
            else
            {
                _radius = 5f;
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (_owner == null)
                return;

            if (!_owner.HasStateAuthority)
                return;

            var otherPlayer = other.GetComponentInParent<Player>();
            if (otherPlayer == null)
                return;

            if (otherPlayer == _owner)
                return;

            if (otherPlayer.Health == null || !otherPlayer.Health.IsAlive)
                return;

            Vector3 center = transform.position;
            Vector3 pos = otherPlayer.KCC != null ? otherPlayer.KCC.Position : otherPlayer.transform.position;

            Vector3 dir = pos - center;
            float dist = dir.magnitude;

            // Si está dentro del radio, no hacemos nada
            if (dist <= _radius)
                return;

            if (dist < 0.001f)
            {
                dir = Vector3.forward;
                dist = 1f;
            }

            dir /= dist;

            float margin = 0.1f;
            Vector3 clampedPos = center + dir * (_radius - margin);

            if (otherPlayer.KCC != null)
            {
                otherPlayer.KCC.SetPosition(clampedPos);
            }
            else
            {
                otherPlayer.transform.position = clampedPos;
            }
        }
    }
}
