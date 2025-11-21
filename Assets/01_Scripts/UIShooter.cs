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

        private int _lastChickens = -1;
        private int _lastHealth = -1;
        private PlayerRef _bestHunter;

        private void OnEnable()
        {
            if (BestHunter != null)
            {
                BestHunter.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            // Fadeout del indicador de golpe
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
                    // Mostrar golpe recibido
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
                    DeathGroup.SetActive(!isAlive);

                if (HealthIndicators != null)
                {
                    for (int i = 0; i < HealthIndicators.Length; i++)
                    {
                        HealthIndicators[i].enabled = _lastHealth > i;
                    }
                }
            }

            // ======================
            // SOULS UI
            // ======================

            int currentSouls = player.ChickenKills; // aquí guardamos "almas que llevas"

            // Sonido solo cuando cambia la cantidad de almas que llevas encima
            if (currentSouls != _lastChickens)
            {
                if (currentSouls > _lastChickens && currentSouls > 0 && ChickenKillClip != null)
                {
                    AudioSource.PlayOneShot(ChickenKillClip);
                }

                _lastChickens = currentSouls;
            }

            // SIEMPRE refrescamos texto, aunque no cambie, para que el depósito se vea al instante
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
        }
    }
}
