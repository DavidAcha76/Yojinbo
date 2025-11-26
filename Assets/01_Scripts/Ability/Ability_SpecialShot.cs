using UnityEngine;

namespace Starter.Shooter
{
    /// <summary>
    /// Disparo especial de almas (T).
    /// </summary>
    public class Ability_SpecialShot : MonoBehaviour
    {
        private Player _player;
        private float _cooldownTimer;

        public void Initialize(Player player)
        {
            _player = player;
        }

        public void Tick(float dt)
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= dt;
                if (_cooldownTimer < 0f)
                    _cooldownTimer = 0f;
            }
        }

        public void FireSpecialShot()
        {
            if (_player == null) return;
            if (!_player.HasStateAuthority) return;
            if (_player._isReloading) return;
            if (_player._isTransformed && _player._isAngelForm) return;

            bool demonFreeShot = _player._isTransformed && !_player._isAngelForm && _player._demonSpecialCharges > 0;

            if (_cooldownTimer > 0f)
                return;

            if (!demonFreeShot && !_player.CheatFreeCostsActive)
            {
                if (_player.CarriedSouls < _player.SpecialShotSoulCost)
                    return;

                _player.SpendSoulsInternal(_player.SpecialShotSoulCost);
            }
            else if (demonFreeShot)
            {
                _player._demonSpecialCharges = Mathf.Max(0, _player._demonSpecialCharges - 1);
            }

            _player.CancelInvisibilityIfActive();

            _cooldownTimer = _player.SpecialShotCooldown;

            _player._hitPosition = Vector3.zero;
            _player._hitNormal = Vector3.zero;

            Vector3 origin = _player.CameraHandle.position + _player.CameraHandle.forward * 0.1f;
            Vector3 direction = _player.CameraHandle.forward;
            float maxDistance = _player.SpecialShotRange;

            LayerMask mask = _player.SpecialShotHitMask.value != 0 ? _player.SpecialShotHitMask : _player.HitMask;

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                maxDistance,
                mask,
                QueryTriggerInteraction.Ignore
            );

            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                for (int i = 0; i < hits.Length; i++)
                {
                    RaycastHit hit = hits[i];

                    if (hit.collider == null)
                        continue;

                    if (hit.collider.transform.IsChildOf(_player.transform))
                        continue;

                    _player._hitPosition = hit.point;
                    _player._hitNormal = hit.normal;

                    var health = hit.collider.GetComponentInParent<Health>();
                    if (health != null && health != _player.Health)
                    {
                        var targetPlayer = health.GetComponent<Player>();

                        if (targetPlayer != null && targetPlayer.IsAngelForm)
                        {
                            break;
                        }

                        int damage = _player.SpecialShotDamage;

                        if (demonFreeShot)
                        {
                            damage = _player.DemonSpecialShotDamage;
                        }

                        // Ahora compila porque OnEnemyKilled es internal
                        health.Killed = _player.OnEnemyKilled;
                        health.TakeHit(damage, true);
                        break;
                    }

                    var destructible = hit.collider.GetComponentInParent<DestructiblePlatform>();
                    if (destructible != null)
                    {
                        destructible.DisableTemporarily(10f);
                        break;
                    }

                    break;
                }
            }

            _player._lastShotType = Player.SHOT_SPECIAL;
            _player._fireCount++;
        }
    }
}
