using UnityEngine;
using Fusion;

namespace Starter.Shooter
{
    /// <summary>
    /// Poder de jaula (tecla C).
    /// Se encarga de:
    /// - Validar coste de almas
    /// - Hacer raycast hacia donde mira el jugador
    /// - Decidir posición de spawn (sobre un jugador o en el punto de impacto)
    /// - Spawnear la jaula en red con Runner.Spawn
    /// - Registrar el disparo para VFX/SFX (SHOT_CAGE)
    /// </summary>
    public class Ability_Cage : MonoBehaviour
    {
        private Player _player;

        /// <summary>
        /// Inicializa la ability con el Player dueño.
        /// Llamar desde Player.Spawned().
        /// </summary>
        public void Initialize(Player player)
        {
            _player = player;
        }

        /// <summary>
        /// Intenta disparar la jaula.
        /// Solo la StateAuthority del Player ejecuta la lógica de spawn.
        /// </summary>
        public void FireCage()
        {
            // Validaciones básicas
            if (_player == null)
                return;

            if (!_player.HasStateAuthority)
                return;

            if (_player.Health == null || !_player.Health.IsAlive)
                return;

            if (_player.CagePrefab == null)
                return;

            // Validar coste de almas (si no hay cheat de coste 0)
            if (!_player.CheatFreeCostsActive)
            {
                if (_player.CageSoulCost <= 0)
                    return;

                if (_player.CarriedSouls < _player.CageSoulCost)
                    return;
            }

            // Raycast desde la cámara del jugador
            Vector3 origin = _player.CameraHandle.position + _player.CameraHandle.forward * 0.1f;
            Vector3 direction = _player.CameraHandle.forward;
            float maxDistance = 200f;

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                maxDistance,
                _player.HitMask,
                QueryTriggerInteraction.Ignore
            );

            if (hits == null || hits.Length == 0)
                return;

            // Ordenar por distancia
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Vector3 spawnPos = Vector3.zero;
            Vector3 impactPos = Vector3.zero;
            Vector3 impactNormal = Vector3.zero;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null)
                    continue;

                // Ignorar colisiones con el propio jugador
                if (hit.collider.transform.IsChildOf(_player.transform))
                    continue;

                // Si golpea a un jugador, centramos la jaula en su posición (KCC o transform)
                Health hitHealth = hit.collider.GetComponentInParent<Health>();
                Player targetPlayer = hitHealth != null ? hitHealth.GetComponent<Player>() : null;

                if (targetPlayer != null)
                {
                    // Usar la posición de la KCC si existe (más estable)
                    spawnPos = targetPlayer.KCC != null
                        ? targetPlayer.KCC.Position
                        : targetPlayer.transform.position;
                }
                else
                {
                    // Si pega escenario, usar el punto de impacto
                    spawnPos = hit.point;
                }

                impactPos = hit.point;
                impactNormal = hit.normal;

                found = true;
                break;
            }

            if (!found)
                return;

            // Cobrar almas (si corresponde)
            if (!_player.CheatFreeCostsActive)
            {
                _player.SpendSoulsInternal(_player.CageSoulCost);
            }

            // Cancelar invisibilidad si estaba activa
            _player.CancelInvisibilityIfActive();

            // Spawnear la jaula EN RED usando Runner.Spawn
            NetworkObject cageObj = _player.Runner.Spawn(
                _player.CagePrefab,
                spawnPos,
                Quaternion.identity,
                _player.Object.InputAuthority,
                (runner, obj) =>
                {
                    // Init callback: setear tiempo de vida
                    CageTrap cageTrap = obj.GetComponent<CageTrap>();
                    if (cageTrap != null)
                    {
                        cageTrap.SetLifetime(_player.CageDuration);
                    }
                }
            );

            // Registrar el disparo para que todos vean SFX/VFX de jaula
            _player.RegisterShotFromAbility(
                Player.SHOT_CAGE,
                impactPos,
                impactNormal
            );
        }
    }
}
