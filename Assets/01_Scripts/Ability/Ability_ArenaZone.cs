using UnityEngine;
using Fusion;

namespace Starter.Shooter
{
    /// <summary>
    /// Poder de "arena oscura":
    /// - Crea una zona esférica que no deja salir a otros players (no al dueño).
    /// - Dura ArenaZoneDuration segundos.
    /// - Cuesta ArenaZoneSoulCost almas (o 0 si CheatFreeCostsActive).
    /// - Mientras está activa:
    ///   - Player.ArenaBuffActive = true.
    ///   - El Player se mueve y salta más rápido (se maneja en Player.ProcessInput).
    ///   - StealSoulsFromPlayer roba 100% de almas.
    /// </summary>
    public class Ability_ArenaZone : MonoBehaviour
    {
        private Player _player;
        private float _timer;
        private NetworkObject _spawnedZone;

        public void Initialize(Player player)
        {
            _player = player;
        }

        /// <summary>
        /// Se llama desde Player.ProcessInput cuando se presiona Z (input.ArenaZone).
        /// </summary>
        public void TryActivateArena()
        {
            if (_player == null)
                return;

            if (!_player.HasStateAuthority)
                return;

            if (!_player.Health.IsAlive)
                return;

            // Ya está activa
            if (_player.ArenaBuffActive)
                return;

            if (_player.ArenaZonePrefab == null)
            {
                Debug.LogWarning("[Ability_ArenaZone] ArenaZonePrefab no asignado en Player.");
                return;
            }

            // Costos (respeta cheat de costos 0)
            int cost = _player.CheatFreeCostsActive ? 0 : _player.ArenaZoneSoulCost;

            if (cost > 0 && _player.CarriedSouls < cost)
                return;

            if (cost > 0)
            {
                _player.SpendSoulsPublic(cost);
            }

            // Cancelar invisibilidad si estaba activa
            _player.CancelInvisibilityIfActive_Internal();

            // Activar buff
            _player.ArenaBuffActive = true;
            _timer = _player.ArenaZoneDuration;

            // Spawn de la zona en la posición actual del player
            Vector3 pos = _player.KCC != null ? _player.KCC.Position : _player.transform.position;
            Quaternion rot = Quaternion.identity;

            _spawnedZone = _player.Runner.Spawn(
                _player.ArenaZonePrefab,
                pos,
                rot,
                _player.Object.InputAuthority,
                (runner, obj) =>
                {
                    var zone = obj.GetComponent<ArenaZone>();
                    if (zone != null)
                    {
                        zone.Initialize(_player);
                    }
                });

            Debug.Log("[Ability_ArenaZone] Arena activada por " + _player.Nickname);
        }

        /// <summary>
        /// Se llama desde Player.FixedUpdateNetwork (myTimeFlow).
        /// </summary>
        public void Tick(float dt)
        {
            if (_player == null)
                return;

            if (!_player.HasStateAuthority)
                return;

            if (!_player.ArenaBuffActive)
                return;

            _timer -= dt;
            if (_timer <= 0f)
            {
                EndArena();
            }
        }

        /// <summary>
        /// Finaliza la arena por tiempo.
        /// </summary>
        private void EndArena()
        {
            if (_player == null)
                return;

            if (!_player.HasStateAuthority)
                return;

            if (!_player.ArenaBuffActive)
                return;

            _player.ArenaBuffActive = false;

            if (_spawnedZone != null && _player.Runner != null && _spawnedZone.IsValid)
            {
                _player.Runner.Despawn(_spawnedZone);
            }

            _spawnedZone = null;

            Debug.Log("[Ability_ArenaZone] Arena finalizada (tiempo agotado).");
        }

        /// <summary>
        /// Cancelación directa (muerte, respawn, etc.) llamada desde Player.
        /// </summary>
        public void ForceStopArena()
        {
            if (_player == null)
                return;

            if (!_player.HasStateAuthority)
                return;

            if (_spawnedZone != null && _player.Runner != null && _spawnedZone.IsValid)
            {
                _player.Runner.Despawn(_spawnedZone);
            }

            _spawnedZone = null;
            _player.ArenaBuffActive = false;
        }
    }
}
