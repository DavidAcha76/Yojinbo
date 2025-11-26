using UnityEngine;

namespace Starter.Shooter
{
    /// <summary>
    /// Ultimate de detener el tiempo (P).
    /// </summary>
    public class Ability_TimeStop : MonoBehaviour
    {
        private Player _player;

        public void Initialize(Player player)
        {
            _player = player;
        }

        public void TryActivate()
        {
            if (_player == null) return;
            if (!_player.HasStateAuthority) return;
            if (!_player.Health.IsAlive) return;
            if (_player.TimeStopActive) return;

            if (!_player.CheatFreeCostsActive)
            {
                if (_player.TimeStopSoulCost <= 0)
                    return;

                if (_player.CarriedSouls < _player.TimeStopSoulCost)
                    return;

                _player.SpendSoulsInternal(_player.TimeStopSoulCost);
            }

            _player.CancelInvisibilityIfActive();

            _player.TimeStopActive = true;
            _player.TimeStopTimer = _player.TimeStopDuration;
        }

        public void Tick(float dt)
        {
            if (_player == null) return;
            if (!_player.HasStateAuthority) return;
            if (!_player.TimeStopActive) return;

            _player.TimeStopTimer -= dt;
            if (_player.TimeStopTimer <= 0f)
            {
                _player.TimeStopActive = false;
                _player.TimeStopTimer = 0f;
            }
        }

        public void Cancel()
        {
            if (_player == null) return;
            if (!_player.HasStateAuthority) return;
            if (!_player.TimeStopActive) return;

            _player.TimeStopActive = false;
            _player.TimeStopTimer = 0f;
        }
    }
}
