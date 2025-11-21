using UnityEngine;
using Fusion;

namespace Starter.Shooter
{
    /// <summary>
    /// Handles player connections (spawning of Player instances) and tracks best soul hoarder.
    /// </summary>
    public sealed class GameManager : NetworkBehaviour
    {
        public Player PlayerPrefab;

        [Networked]
        public PlayerRef BestHunter { get; set; }

        // Cuántas almas bancadas tiene el mejor cazador
        [Networked]
        public int BestHunterBankedSouls { get; set; }

        public Player LocalPlayer { get; private set; }

        private SpawnPoint[] _spawnPoints;

        public Vector3 GetSpawnPosition()
        {
            var spawnPoint = _spawnPoints[Random.Range(0, _spawnPoints.Length)];
            var randomPositionOffset = Random.insideUnitCircle * spawnPoint.Radius;
            return spawnPoint.transform.position + new Vector3(randomPositionOffset.x, 0f, randomPositionOffset.y);
        }

        public override void Spawned()
        {
            _spawnPoints = FindObjectsOfType<SpawnPoint>();

            LocalPlayer = Runner.Spawn(PlayerPrefab, GetSpawnPosition(), Quaternion.identity, Runner.LocalPlayer);
            Runner.SetPlayerObject(Runner.LocalPlayer, LocalPlayer.Object);
        }

        public override void FixedUpdateNetwork()
        {
            BestHunter = PlayerRef.None;
            BestHunterBankedSouls = 0;

            foreach (var playerRef in Runner.ActivePlayers)
            {
                var playerObject = Runner.GetPlayerObject(playerRef);
                var player = playerObject != null ? playerObject.GetComponent<Player>() : null;

                if (player == null)
                    continue;

                int banked = player.BankedSouls;

                // IMPORTANTE: ya no filtramos por Health.IsAlive
                // El top sigue siéndolo aunque esté muerto, mientras nadie lo supere en almas depositadas.
                if (banked > BestHunterBankedSouls)
                {
                    BestHunterBankedSouls = banked;
                    BestHunter = player.Object.StateAuthority;
                }
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            // Limpiar referencia porque la UI puede intentar acceder incluso después de despawn
            LocalPlayer = null;
        }
    }
}
