using UnityEngine;
using Fusion;

namespace Starter.Shooter
{
    public sealed class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Setup")]
        public Player PlayerPrefab;

        [Header("Match Setup")]
        [Tooltip("Duración de la partida en segundos (7 minutos = 420).")]
        public float MatchDurationSeconds = 420f;

        [Networked]
        public PlayerRef BestHunter { get; set; }

        [Networked]
        public int BestHunterBankedSouls { get; set; }

        [Networked]
        public float MatchTimeRemaining { get; set; }

        [Networked]
        public NetworkBool IsSuddenDeath { get; set; }

        [Networked]
        public NetworkBool MatchEnded { get; set; }

        [Header("Time Stop Audio")]
        public AudioSource TimeStopGlobalAudio;
        public AudioClip TimeStopStartClip;
        public AudioClip TimeStopEndClip;

        [Networked, OnChangedRender(nameof(OnTimeStoppedChanged))]
        public NetworkBool IsTimeStopped { get; set; }

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
            Instance = this;

            _spawnPoints = FindObjectsOfType<SpawnPoint>();

            LocalPlayer = Runner.Spawn(PlayerPrefab, GetSpawnPosition(), Quaternion.identity, Runner.LocalPlayer);
            Runner.SetPlayerObject(Runner.LocalPlayer, LocalPlayer.Object);

            if (Object.HasStateAuthority)
            {
                if (MatchTimeRemaining <= 0f)
                {
                    MatchTimeRemaining = MatchDurationSeconds;
                    IsSuddenDeath = false;
                    MatchEnded = false;
                    IsTimeStopped = false;
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (Object.HasStateAuthority == false)
                return;

            bool timeStopped = false;

            foreach (var playerRef in Runner.ActivePlayers)
            {
                var playerObject = Runner.GetPlayerObject(playerRef);
                var player = playerObject != null ? playerObject.GetComponent<Player>() : null;

                if (player == null)
                    continue;

                if (player.TimeStopActive)
                {
                    timeStopped = true;
                    break;
                }
            }

            IsTimeStopped = timeStopped;

            if (MatchEnded == false)
            {
                if (!IsTimeStopped)
                {
                    MatchTimeRemaining -= Runner.DeltaTime;
                    if (MatchTimeRemaining < 0f)
                    {
                        MatchTimeRemaining = 0f;
                    }
                }

                if (IsSuddenDeath == false && MatchTimeRemaining <= 30f)
                {
                    IsSuddenDeath = true;
                }

                if (MatchTimeRemaining <= 0f)
                {
                    MatchEnded = true;
                }
            }

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

        private void OnTimeStoppedChanged()
        {
            if (IsTimeStopped)
            {
                if (TimeStopGlobalAudio != null && TimeStopStartClip != null)
                {
                    TimeStopGlobalAudio.PlayOneShot(TimeStopStartClip);
                }
            }
            else
            {
                if (TimeStopGlobalAudio != null && TimeStopEndClip != null)
                {
                    TimeStopGlobalAudio.PlayOneShot(TimeStopEndClip);
                }
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this)
                Instance = null;

            LocalPlayer = null;
        }

        public static float GetWorldDeltaTime(NetworkRunner runner)
        {
            if (Instance == null)
            {
                return runner != null ? runner.DeltaTime : Time.fixedDeltaTime;
            }

            if (Instance.IsTimeStopped)
                return 0f;

            return runner != null ? runner.DeltaTime : Time.fixedDeltaTime;
        }
    }
}
