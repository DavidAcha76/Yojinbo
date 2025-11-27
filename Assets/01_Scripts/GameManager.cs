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

        [Header("Gameplay Music")]
        [Tooltip("AudioSource que reproduce la música de gameplay (BGM).")]
        public AudioSource GameplayMusicSource;

        [Tooltip("Clip de música principal de la partida.")]
        public AudioClip GameplayMusicClip;

        [Tooltip("¿La música de gameplay debe hacer loop?")]
        public bool LoopGameplayMusic = true;

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

            // Arrancar música de gameplay en cada cliente (solo si el tiempo no está parado)
            if (!IsTimeStopped)
            {
                StartGameplayMusic();
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (Object.HasStateAuthority == false)
                return;

            bool timeStopped = false;

            // Revisar si algún jugador tiene activo el Time Stop
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
                // El tiempo de partida solo avanza si no está detenido
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

            // Recalcular BestHunter según almas bancadas
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

        /// <summary>
        /// Se llama en todos los clientes cuando IsTimeStopped cambia (true/false).
        /// Controla SFX globales de Time Stop y también la música de gameplay.
        /// </summary>
        private void OnTimeStoppedChanged()
        {
            if (IsTimeStopped)
            {
                // SFX de inicio de Time Stop
                if (TimeStopGlobalAudio != null && TimeStopStartClip != null)
                {
                    TimeStopGlobalAudio.PlayOneShot(TimeStopStartClip);
                }

                // Pausar música de gameplay mientras el tiempo está detenido
                if (GameplayMusicSource != null && GameplayMusicSource.isPlaying)
                {
                    GameplayMusicSource.Pause();
                }
            }
            else
            {
                // SFX de final de Time Stop
                if (TimeStopGlobalAudio != null && TimeStopEndClip != null)
                {
                    TimeStopGlobalAudio.PlayOneShot(TimeStopEndClip);
                }

                // Reanudar música de gameplay cuando el tiempo vuelve a correr
                ResumeGameplayMusic();
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this)
                Instance = null;

            LocalPlayer = null;
        }

        /// <summary>
        /// Devuelve el deltaTime "real" del mundo considerando si el tiempo está detenido.
        /// </summary>
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

        // ===========================
        // MÚSICA DE GAMEPLAY (BGM)
        // ===========================

        /// <summary>
        /// Inicia la música de gameplay si todo está configurado.
        /// </summary>
        private void StartGameplayMusic()
        {
            if (GameplayMusicSource == null)
                return;

            if (GameplayMusicClip != null && GameplayMusicSource.clip != GameplayMusicClip)
            {
                GameplayMusicSource.clip = GameplayMusicClip;
            }

            GameplayMusicSource.loop = LoopGameplayMusic;

            if (!GameplayMusicSource.isPlaying)
            {
                GameplayMusicSource.Play();
            }
        }

        /// <summary>
        /// Reanuda la música de gameplay después de un Time Stop.
        /// Si nunca se ha reproducido, la lanza desde el inicio.
        /// </summary>
        private void ResumeGameplayMusic()
        {
            if (GameplayMusicSource == null)
                return;

            if (GameplayMusicClip != null && GameplayMusicSource.clip != GameplayMusicClip)
            {
                GameplayMusicSource.clip = GameplayMusicClip;
            }

            GameplayMusicSource.loop = LoopGameplayMusic;

            // Si estaba pausada, reanudar; si no, iniciar
            if (GameplayMusicSource.time > 0f && !GameplayMusicSource.isPlaying)
            {
                GameplayMusicSource.UnPause();
            }
            else if (!GameplayMusicSource.isPlaying)
            {
                GameplayMusicSource.Play();
            }
        }
    }
}
