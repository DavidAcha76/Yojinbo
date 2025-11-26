using UnityEngine;
using Fusion;

namespace Starter.Shooter
{
    /// <summary>
    /// Poder de jaula (C).
    /// </summary>
    public class Ability_Cage : MonoBehaviour
    {
        private Player _player;

        public void Initialize(Player player)
        {
            _player = player;
        }

        public void FireCage()
        {
            if (_player == null) return;
            if (!_player.HasStateAuthority) return;
            if (!_player.Health.IsAlive) return;
            if (_player.CagePrefab == null) return;

            if (!_player.CheatFreeCostsActive)
            {
                if (_player.CageSoulCost <= 0)
                    return;

                if (_player.CarriedSouls < _player.CageSoulCost)
                    return;
            }

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

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Vector3 spawnPos = Vector3.zero;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null)
                    continue;

                if (hit.collider.transform.IsChildOf(_player.transform))
                    continue;

                var health = hit.collider.GetComponentInParent<Health>();
                Player targetPlayer = health != null ? health.GetComponent<Player>() : null;

                if (targetPlayer != null)
                {
                    spawnPos = targetPlayer.KCC != null ? targetPlayer.KCC.Position : targetPlayer.transform.position;
                }
                else
                {
                    spawnPos = hit.point;
                }

                _player._hitPosition = hit.point;
                _player._hitNormal = hit.normal;

                found = true;
                break;
            }

            if (!found)
                return;

            if (!_player.CheatFreeCostsActive)
            {
                _player.SpendSoulsInternal(_player.CageSoulCost);
            }

            _player.CancelInvisibilityIfActive();

            NetworkObject cageObj = _player.Runner.Spawn(_player.CagePrefab, spawnPos, Quaternion.identity, _player.Object.InputAuthority);
            var cageTrap = cageObj.GetComponent<CageTrap>();
            if (cageTrap != null)
            {
                cageTrap.SetLifetime(_player.CageDuration);
            }

            _player._lastShotType = Player.SHOT_CAGE;
            _player._fireCount++;
        }
    }
}
