using UnityEngine;

namespace Starter.Shooter
{
    /// <summary>
    /// Poder de curación (Q).
    /// </summary>
    public class Ability_Heal : MonoBehaviour
    {
        private Player _player;

        public void Initialize(Player player)
        {
            _player = player;
        }

        public void TryHeal()
        {
            if (_player == null) return;
            if (!_player.HasStateAuthority) return;
            if (!_player.Health.IsAlive) return;

            if (!_player.CheatFreeCostsActive)
            {
                if (_player.HealSoulCost <= 0)
                    return;

                if (_player.CarriedSouls < _player.HealSoulCost)
                    return;
            }

            if (_player.Health.CurrentHealth >= _player.Health.InitialHealth)
                return;

            _player.CancelInvisibilityIfActive();

            if (!_player.CheatFreeCostsActive)
            {
                _player.SpendSoulsInternal(_player.HealSoulCost);
            }

            // Curamos 1 de vida (igual que antes)
            _player.Health.TakeHit(-1);

            if (!_player.IsAudioMutedInternal())
            {
                if (_player.HealSound != null && _player.HealClip != null)
                {
                    _player.HealSound.PlayOneShot(_player.HealClip);
                }
            }
        }
    }
}
