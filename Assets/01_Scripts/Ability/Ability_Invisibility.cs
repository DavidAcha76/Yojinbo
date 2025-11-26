using UnityEngine;

namespace Starter.Shooter
{
    /// <summary>
    /// Poder de invisibilidad (tecla F).
    /// Maneja solo la lógica del poder; el Player solo la llama.
    /// </summary>
    public class Ability_Invisibility : MonoBehaviour
    {
        private Player _player;

        public void Initialize(Player player)
        {
            _player = player;
        }

        public void TryStartInvisibility()
        {
            if (_player == null) return;
            if (!_player.HasStateAuthority) return;
            if (_player._isInvisible) return;

            if (!_player.CheatFreeCostsActive)
            {
                if (_player.CarriedSouls < _player.InvisibilitySoulCost)
                    return;

                _player.SpendSoulsInternal(_player.InvisibilitySoulCost);
            }

            _player._isInvisible = true;

            if (!_player.IsAudioMutedInternal())
            {
                if (_player.InvisibilitySound != null && _player.InvisibilityClip != null)
                {
                    _player.InvisibilitySound.PlayOneShot(_player.InvisibilityClip);
                }
            }
        }

        public void CancelIfActive()
        {
            if (_player == null) return;
            if (!_player.HasStateAuthority) return;

            if (!_player._isInvisible)
                return;

            _player._isInvisible = false;
        }

        public void UpdateVisual()
        {
            if (_player == null)
                return;

            if (_player.InvisibilityRenderers == null || _player.InvisibilityRenderers.Length == 0)
            {
                _player.InvisibilityRenderers = _player.GetComponentsInChildren<Renderer>(true);
            }

            bool hideForThisClient = _player._isInvisible && !_player.HasStateAuthority;

            if (_player.InvisibilityRenderers != null)
            {
                for (int i = 0; i < _player.InvisibilityRenderers.Length; i++)
                {
                    var r = _player.InvisibilityRenderers[i];
                    if (r == null) continue;
                    r.enabled = !hideForThisClient;
                }
            }

            if (_player.Nameplate != null && _player.Nameplate.gameObject != null)
            {
                _player.Nameplate.gameObject.SetActive(!hideForThisClient);
            }
        }
    }
}
