using UnityEngine;
using Fusion;

namespace Starter.Shooter
{
    /// <summary>
    /// Handles player connections (spawning of Player instances) and match flow:
    /// - Spawns players
    /// - Keeps track of best hunter (by banked souls)
    /// - Controls match timer, sudden death and match end.
    /// </summary>
    public sealed class GameManager : NetworkBehaviour
    {
        [Header("Setup")]
        public Player PlayerPrefab;

        [Header("Match Setup")]
        [Tooltip("Duración de la partida en segundos (7 minutos = 420).")]
        public float MatchDurationSeconds = 420f; // 7 minutos

        [Networked]
        public PlayerRef BestHunter { get; set; }

        // Cuántas almas bancadas tiene el mejor cazador (en el altar)
        [Networked]
        public int BestHunterBankedSouls { get; set; }

        // Tiempo restante de partida (segundos)
        [Networked]
        public float MatchTimeRemaining { get; set; }

        // Últimos 30 segundos: muertes definitivas
        [Networked]
        public NetworkBool IsSuddenDeath { get; set; }

        // Bandera de fin de partida
        [Networked]
        public NetworkBool MatchEnded { get; set; }

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

            // Spawn local player
            LocalPlayer = Runner.Spawn(PlayerPrefab, GetSpawnPosition(), Quaternion.identity, Runner.LocalPlayer);
            Runner.SetPlayerObject(Runner.LocalPlayer, LocalPlayer.Object);

            // Inicializar timer solo en la autoridad
            if (Object.HasStateAuthority)
            {
                if (MatchTimeRemaining <= 0f)
                {
                    MatchTimeRemaining = MatchDurationSeconds;
                    IsSuddenDeath = false;
                    MatchEnded = false;
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            // Solo la autoridad de estado actualiza lógica de partida
            if (Object.HasStateAuthority == false)
                return;

            // 1) Actualizar timer y estado de partida
            if (MatchEnded == false)
            {
                MatchTimeRemaining -= Runner.DeltaTime;
                if (MatchTimeRemaining < 0f)
                {
                    MatchTimeRemaining = 0f;
                }

                // Últimos 30 segundos -> sudden death
                if (IsSuddenDeath == false && MatchTimeRemaining <= 30f)
                {
                    IsSuddenDeath = true;
                }

                // Fin de partida
                if (MatchTimeRemaining <= 0f)
                {
                    MatchEnded = true;
                }
            }

            // 2) Recalcular mejor cazador por almas bancadas (siempre, para que el top quede bien)
            BestHunter = PlayerRef.None;
            BestHunterBankedSouls = 0;

            foreach (var playerRef in Runner.ActivePlayers)
            {
                var playerObject = Runner.GetPlayerObject(playerRef);
                var player = playerObject != null ? playerObject.GetComponent<Player>() : null;

                if (player == null)
                    continue;

                int banked = player.BankedSouls;

                if (banked > BestHunterBankedSouls)
                {
                    BestHunterBankedSouls = banked;
                    BestHunter = player.Object.StateAuthority;
                }
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            // Clear the reference because UI can try to access it even after despawn
            LocalPlayer = null;
        }
    }
}
