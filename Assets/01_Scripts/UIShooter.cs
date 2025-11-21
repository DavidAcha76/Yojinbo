using System.Collections.Generic;
using System.Text;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Starter.Shooter
{
    /// <summary>
    /// Main UI script for Shooter sample.
    /// </summary>
    public class UIShooter : MonoBehaviour
    {
        [Header("References")]
        public GameManager GameManager;
        public CanvasGroup CanvasGroup;
        public TextMeshProUGUI BestHunter;
        public GameObject AliveGroup;
        public GameObject DeathGroup;
        public Image[] HealthIndicators;
        public CanvasGroup HitIndicator;

        [Header("UI Sound Setup")]
        public AudioSource AudioSource;
        public AudioClip ChickenKillClip;
        public AudioClip HitReceivedClip;
        public AudioClip DeathClip;

        [Header("Soul UI")]
        public TextMeshProUGUI ChickenCount;          // Almas que llevas encima
        public TextMeshProUGUI BestHunterSoulCount;   // Almas bancadas del mejor cazador

        [Header("Match UI")]
        [Tooltip("Texto para mostrar el tiempo restante en formato mm:ss.")]
        public TextMeshProUGUI TimerText;

        [Tooltip("Canvas/grupo que muestra el top de jugadores al final.")]
        public GameObject EndGameGroup;
        public TextMeshProUGUI EndGameTopText;

        [Tooltip("Canvas/grupo para el final benevolente (más almas puras que corruptas).")]
        public GameObject BenevolentEndingGroup;

        [Tooltip("Canvas/grupo para el final tirano (más almas corruptas que puras).")]
        public GameObject TyrantEndingGroup;

        private int _lastChickens = -1;
        private int _lastHealth = -1;
        private PlayerRef _bestHunter;

        private bool _lastMatchEnded = false;
        private float _endGameTimer = 0f;

        private void OnEnable()
        {
            if (BestHunter != null)
            {
                BestHunter.gameObject.SetActive(false);
            }

            if (EndGameGroup != null) EndGameGroup.SetActive(false);
            if (BenevolentEndingGroup != null) BenevolentEndingGroup.SetActive(false);
            if (TyrantEndingGroup != null) TyrantEndingGroup.SetActive(false);
        }

        private void Update()
        {
            // Fadeout hit indicator
            if (HitIndicator != null)
            {
                HitIndicator.alpha = Mathf.Lerp(HitIndicator.alpha, 0f, Time.deltaTime * 2f);
            }

            if (GameManager == null)
            {
                CanvasGroup.alpha = 0f;
                return;
            }

            var player = GameManager.LocalPlayer;
            if (player == null)
            {
                CanvasGroup.alpha = 0f;
                return;
            }

            // ======================
            // BEST HUNTER (NOMBRE)
            // ======================
            if (_bestHunter != GameManager.BestHunter)
            {
                _bestHunter = GameManager.BestHunter;

                if (BestHunter != null)
                {
                    if (_bestHunter == PlayerRef.None || GameManager.Runner == null)
                    {
                        BestHunter.text = string.Empty;
                        BestHunter.gameObject.SetActive(false);
                    }
                    else
                    {
                        var hunterObject = GameManager.Runner.GetPlayerObject(_bestHunter);
                        var hunter = hunterObject != null ? hunterObject.GetComponent<Player>() : null;

                        if (hunter != null)
                        {
                            BestHunter.text = hunter.Nickname;
                            BestHunter.gameObject.SetActive(true);
                        }
                        else
                        {
                            BestHunter.text = string.Empty;
                            BestHunter.gameObject.SetActive(false);
                        }
                    }
                }
            }

            // ======================
            // VIDA / MUERTE UI
            // ======================
            if (_lastHealth != player.Health.CurrentHealth)
            {
                bool isAlive = player.Health.IsAlive;

                if (_lastHealth > player.Health.CurrentHealth)
                {
                    // Show hit received
                    if (HitIndicator != null)
                    {
                        HitIndicator.alpha = 1f;
                    }

                    var clip = isAlive ? HitReceivedClip : DeathClip;
                    if (clip != null)
                    {
                        AudioSource.PlayOneShot(clip);
                    }
                }

                _lastHealth = player.Health.CurrentHealth;

                if (AliveGroup != null)
                    AliveGroup.SetActive(isAlive);

                if (DeathGroup != null)
                    DeathGroup.SetActive(isAlive == false);

                if (HealthIndicators != null)
                {
                    for (int i = 0; i < HealthIndicators.Length; i++)
                    {
                        HealthIndicators[i].enabled = _lastHealth > i;
                    }
                }
            }

            // ======================
            // SOULS UI (almas actuales que llevas encima)
            // ======================
            int currentSouls = player.ChickenKills; // aquí guardas las almas que llevas encima

            // Sonido solo cuando cambia la cantidad de almas que llevas encima
            if (currentSouls != _lastChickens)
            {
                if (currentSouls > _lastChickens && currentSouls > 0 && ChickenKillClip != null)
                {
                    AudioSource.PlayOneShot(ChickenKillClip);
                }

                _lastChickens = currentSouls;
            }

            // Siempre refrescamos texto, así al depositar se ve al instante
            CanvasGroup.alpha = 1f;

            if (ChickenCount != null)
            {
                ChickenCount.text = $"Almas: {currentSouls}";
            }

            // ======================
            // MEJOR CAZADOR: NOMBRE + ALMAS BANCADAS
            // ======================
            if (BestHunterSoulCount != null && GameManager != null)
            {
                int bestSouls = GameManager.BestHunterBankedSouls;
                string bestName = "-";

                if (GameManager.BestHunter != PlayerRef.None && GameManager.Runner != null)
                {
                    var bestObj = GameManager.Runner.GetPlayerObject(GameManager.BestHunter);
                    if (bestObj != null)
                    {
                        var bestPlayer = bestObj.GetComponent<Player>();
                        if (bestPlayer != null)
                        {
                            bestName = string.IsNullOrEmpty(bestPlayer.Nickname)
                                ? $"Player {bestObj.Id}"
                                : bestPlayer.Nickname;
                        }
                    }
                }

                BestHunterSoulCount.text = $"Mejor cazador: {bestName} ({bestSouls} almas)";
            }

            // ======================
            // TIMER DE PARTIDA (mm:ss)
            // ======================
            if (TimerText != null)
            {
                float time = GameManager.MatchTimeRemaining;
                if (time < 0f) time = 0f;

                int totalSeconds = Mathf.CeilToInt(time);
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;

                TimerText.text = $"{minutes:00}:{seconds:00}";
            }

            // ======================
            // FIN DE PARTIDA + TOP + FINALES
            // ======================
            bool matchEnded = GameManager.MatchEnded;

            // Detectar transición de "no terminado" -> "terminado"
            if (matchEnded && _lastMatchEnded == false)
            {
                _lastMatchEnded = true;
                _endGameTimer = 0f;

                // Activar canvas del top
                if (EndGameGroup != null) EndGameGroup.SetActive(true);
                if (BenevolentEndingGroup != null) BenevolentEndingGroup.SetActive(false);
                if (TyrantEndingGroup != null) TyrantEndingGroup.SetActive(false);

                if (EndGameTopText != null)
                {
                    EndGameTopText.text = BuildLeaderboardText();
                }
            }
            else if (matchEnded == false && _lastMatchEnded)
            {
                // Por si reinicias partida
                _lastMatchEnded = false;
                _endGameTimer = 0f;

                if (EndGameGroup != null) EndGameGroup.SetActive(false);
                if (BenevolentEndingGroup != null) BenevolentEndingGroup.SetActive(false);
                if (TyrantEndingGroup != null) TyrantEndingGroup.SetActive(false);
            }

            if (matchEnded)
            {
                _endGameTimer += Time.deltaTime;

                // Después de 6 segundos, ocultamos el top y mostramos final según pureza/corrupción
                if (_endGameTimer >= 6f)
                {
                    if (EndGameGroup != null) EndGameGroup.SetActive(false);

                    int totalPure = player.BankedPureSouls + player.CarriedPureSouls;
                    int totalCorrupt = player.BankedCorruptSouls + player.CarriedCorruptSouls;

                    bool isTyrant = totalCorrupt > totalPure;

                    if (isTyrant)
                    {
                        if (TyrantEndingGroup != null) TyrantEndingGroup.SetActive(true);
                        if (BenevolentEndingGroup != null) BenevolentEndingGroup.SetActive(false);
                    }
                    else
                    {
                        if (BenevolentEndingGroup != null) BenevolentEndingGroup.SetActive(true);
                        if (TyrantEndingGroup != null) TyrantEndingGroup.SetActive(false);
                    }
                }
            }
        }

        private string BuildLeaderboardText()
        {
            if (GameManager == null || GameManager.Runner == null)
                return "Sin datos";

            var players = new List<Player>();

            foreach (var pRef in GameManager.Runner.ActivePlayers)
            {
                var obj = GameManager.Runner.GetPlayerObject(pRef);
                if (obj == null)
                    continue;

                var p = obj.GetComponent<Player>();
                if (p != null)
                {
                    players.Add(p);
                }
            }

            if (players.Count == 0)
                return "Sin jugadores";

            // Ordenar por almas bancadas (de mayor a menor)
            players.Sort((a, b) => b.BankedSouls.CompareTo(a.BankedSouls));

            var sb = new StringBuilder();

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                string name = string.IsNullOrEmpty(p.Nickname) ? $"Player {i + 1}" : p.Nickname;
                sb.AppendLine($"{i + 1}. {name} - {p.BankedSouls} almas");
            }

            return sb.ToString();
        }
    }
}
