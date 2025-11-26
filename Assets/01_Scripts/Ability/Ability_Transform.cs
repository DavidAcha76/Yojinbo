using UnityEngine;

namespace Starter.Shooter
{
    /// <summary>
    /// Transformación Ángel / Demonio (G).
    /// </summary>
    public class Ability_Transform : MonoBehaviour
    {
        private Player _player;

        public void Initialize(Player player)
        {
            _player = player;
        }

        public void TryStartTransformation()
        {
            if (_player == null) return;
            if (!_player.HasStateAuthority) return;
            if (_player._isTransformed) return;

            int spentPure = 0;
            int spentCorrupt = 0;

            if (!_player.CheatFreeCostsActive)
            {
                if (!_player.TrySpendSoulsForTransformInternal(_player.TransformSoulCost, out spentPure, out spentCorrupt))
                    return;
            }
            else
            {
                spentPure = _player.CarriedPureSouls;
                spentCorrupt = _player.CarriedCorruptSouls;
            }

            _player.CancelInvisibilityIfActive();

            bool angel = spentPure >= spentCorrupt;

            _player._isTransformed = true;
            _player._isAngelForm = angel;
            _player._transformTimer = _player.TransformDuration;

            _player._baseWalkSpeed = _player.WalkSpeed;

            if (angel)
            {
                _player.WalkSpeed = _player._baseWalkSpeed * _player.AngelSpeedMultiplier;
                _player._demonSpecialCharges = 0;
            }
            else
            {
                _player.WalkSpeed = _player._baseWalkSpeed * _player.DemonSpeedMultiplier;
                _player._demonSpecialCharges = _player.DemonMaxSpecialCharges;

                if (_player.Health != null && _player.DemonBonusHealth > 0)
                {
                    _player.Health.TakeHit(-_player.DemonBonusHealth);
                }
            }
        }

        public void Tick(float dt)
        {
            if (_player == null) return;
            if (!_player.HasStateAuthority) return;
            if (!_player._isTransformed) return;

            _player._transformTimer -= dt;
            if (_player._transformTimer <= 0f)
            {
                EndTransformation();
            }
        }

        private void EndTransformation()
        {
            if (_player == null) return;

            bool wasDemon = _player._isTransformed && !_player._isAngelForm;

            _player._isTransformed = false;
            _player._isAngelForm = false;
            _player._transformTimer = 0f;

            _player.WalkSpeed = _player._baseWalkSpeed;
            _player._demonSpecialCharges = 0;

            if (wasDemon && _player.Health != null)
            {
                if (_player.Health.CurrentHealth > _player.Health.InitialHealth)
                {
                    int extra = _player.Health.CurrentHealth - _player.Health.InitialHealth;
                    if (extra > 0)
                    {
                        _player.Health.TakeHit(extra, true);
                    }
                }
            }

            StopTransformLoops();
        }

        public void StopTransformLoops()
        {
            if (_player == null) return;

            if (_player.AngelSound != null)
            {
                _player.AngelSound.loop = false;
                _player.AngelSound.Stop();
            }

            if (_player.DemonSound != null)
            {
                _player.DemonSound.loop = false;
                _player.DemonSound.Stop();
            }
        }

        public void UpdateVisual()
        {
            if (_player == null) return;

            bool muted = _player.IsAudioMutedInternal();

            bool angelActive = _player._isTransformed && _player._isAngelForm;
            bool demonActive = _player._isTransformed && !_player._isAngelForm;

            if (_player.AngelWings != null && _player.AngelWings.activeSelf != angelActive)
                _player.AngelWings.SetActive(angelActive);

            if (_player.DemonWings != null && _player.DemonWings.activeSelf != demonActive)
                _player.DemonWings.SetActive(demonActive);

            // Audio ángel
            if (_player.AngelSound != null)
            {
                if (muted)
                {
                    if (_player.AngelSound.isPlaying)
                    {
                        _player.AngelSound.loop = false;
                        _player.AngelSound.Stop();
                    }
                }
                else
                {
                    if (angelActive)
                    {
                        if (!_player.AngelSound.isPlaying)
                        {
                            if (_player.AngelTransformLoopClip != null && _player.AngelSound.clip != _player.AngelTransformLoopClip)
                            {
                                _player.AngelSound.clip = _player.AngelTransformLoopClip;
                            }
                            _player.AngelSound.loop = true;
                            _player.AngelSound.Play();
                        }
                    }
                    else if (_player.AngelSound.isPlaying)
                    {
                        _player.AngelSound.loop = false;
                        _player.AngelSound.Stop();
                    }
                }
            }

            // Audio demonio
            if (_player.DemonSound != null)
            {
                if (muted)
                {
                    if (_player.DemonSound.isPlaying)
                    {
                        _player.DemonSound.loop = false;
                        _player.DemonSound.Stop();
                    }
                }
                else
                {
                    if (demonActive)
                    {
                        if (!_player.DemonSound.isPlaying)
                        {
                            if (_player.DemonTransformLoopClip != null && _player.DemonSound.clip != _player.DemonTransformLoopClip)
                            {
                                _player.DemonSound.clip = _player.DemonTransformLoopClip;
                            }
                            _player.DemonSound.loop = true;
                            _player.DemonSound.Play();
                        }
                    }
                    else if (_player.DemonSound.isPlaying)
                    {
                        _player.DemonSound.loop = false;
                        _player.DemonSound.Stop();
                    }
                }
            }
        }
    }
}
