using UnityEngine;
using Fusion;

namespace Starter.Shooter
{
    /// <summary>
    /// Plataforma que cae cuando un jugador la pisa.
    /// - Al pisarla: espera "FallDelay" segundos mientras tiembla.
    /// - Luego cae (activa gravedad).
    /// - Permanece caída "RespawnDelay" segundos.
    /// - Después vuelve a su posición inicial y se reactiva.
    /// 
    /// Setup recomendado:
    /// - Mismo GameObject:
    ///   - Rigidbody
    ///       - Use Gravity = false
    ///       - Is Kinematic = true
    ///   - 2 colliders:
    ///       1) Uno sólido (isTrigger = false) -> piso.
    ///       2) Uno trigger (isTrigger = true) -> detector del jugador.
    ///   - NetworkObject
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NetworkObject))]
    public class FallingPlatform : NetworkBehaviour
    {
        [Header("Timers")]
        [Tooltip("Tiempo que tarda en caer después de que un jugador la pisa.")]
        public float FallDelay = 2f;

        [Tooltip("Tiempo que la plataforma permanece caída antes de volver a su posición inicial.")]
        public float RespawnDelay = 10f;

        [Header("Detección de jugador")]
        [Tooltip("Si es true, solo reacciona a objetos con componente Player.")]
        public bool OnlyReactToPlayer = true;

        [Header("Shake (temblor antes de caer)")]
        [Tooltip("Amplitud del temblor en unidades de mundo.")]
        public float ShakeAmplitude = 0.05f;

        [Tooltip("Frecuencia del temblor.")]
        public float ShakeFrequency = 20f;

        [Header("Protección de spawn")]
        [Tooltip("Tiempo tras el spawn en el que ignora cualquier trigger para que no caiga sola.")]
        public float IgnoreTriggersAfterSpawn = 0.3f;

        private Rigidbody _rigidbody;
        private Collider[] _colliders;

        // Posición y rotación inicial
        private Vector3 _startPosition;
        private Quaternion _startRotation;

        // Temblor local
        private float _shakeTimer;
        private Vector3 _shakeBasePos;

        // Para ignorar triggers justo al inicio (solo StateAuthority lo usa)
        private float _ignoreTriggersTimer;

        // Timers de red
        [Networked]
        private TickTimer FallTimer { get; set; }

        [Networked]
        private TickTimer RespawnTimer { get; set; }

        // Estados
        [Networked, OnChangedRender(nameof(OnTriggeredChanged))]
        public NetworkBool IsTriggered { get; set; }

        [Networked, OnChangedRender(nameof(OnDownChanged))]
        public NetworkBool IsDown { get; set; }

        public override void Spawned()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _colliders = GetComponents<Collider>();

            _startPosition = transform.position;
            _startRotation = transform.rotation;

            // De entrada TODAS las copias (host y clientes) dejan la plataforma estática
            ForceStaticLocal();

            if (Object.HasStateAuthority)
            {
                IsTriggered = false;
                IsDown = false;
                FallTimer = TickTimer.None;
                RespawnTimer = TickTimer.None;
                _ignoreTriggersTimer = IgnoreTriggersAfterSpawn;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;

            // Ignora triggers un pequeño tiempo tras el spawn para que no se active sola
            if (_ignoreTriggersTimer > 0f)
            {
                _ignoreTriggersTimer -= Runner.DeltaTime;
            }

            // Si está marcada como "triggered" y el timer expira -> cae
            if (IsTriggered && FallTimer.Expired(Runner))
            {
                StartFall();
            }

            // Si está abajo y el timer expira -> respawn
            if (IsDown && RespawnTimer.Expired(Runner))
            {
                ResetPlatform();
            }
        }

        private void Update()
        {
            // Temblor visual
            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.deltaTime;

                // Solo temblar mientras la plataforma sigue estática
                if (_rigidbody != null && _rigidbody.isKinematic)
                {
                    float t = Time.time * ShakeFrequency;

                    float offsetY = Mathf.Sin(t) * ShakeAmplitude;
                    float offsetX = Mathf.Cos(t * 0.7f) * ShakeAmplitude * 0.5f;

                    transform.position = _shakeBasePos + new Vector3(offsetX, offsetY, 0f);
                }

                if (_shakeTimer <= 0f && _rigidbody != null && _rigidbody.isKinematic)
                {
                    transform.position = _shakeBasePos;
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!Object.HasStateAuthority)
                return;

            // Evitar activarse por colisiones iniciales raras
            if (_ignoreTriggersTimer > 0f)
                return;

            if (IsTriggered || IsDown)
                return;

            if (OnlyReactToPlayer)
            {
                var player = other.GetComponentInParent<Player>();
                if (player == null)
                    return;
            }

            // Empezar proceso de caída
            IsTriggered = true;
            FallTimer = TickTimer.CreateFromSeconds(Runner, FallDelay);
        }

        /// <summary>
        /// Cambia visualmente cuando IsTriggered cambia (temblor).
        /// </summary>
        private void OnTriggeredChanged()
        {
            if (IsTriggered)
            {
                // Empezar shake
                _shakeBasePos = transform.position;
                _shakeTimer = FallDelay;
            }
            else
            {
                // Cancelar shake
                _shakeTimer = 0f;
                if (_rigidbody != null && _rigidbody.isKinematic)
                {
                    transform.position = _shakeBasePos;
                }
            }
        }

        /// <summary>
        /// Se llama cuando IsDown cambia (arriba/abajo) para reflejar estado en todos los clientes.
        /// </summary>
        private void OnDownChanged()
        {
            if (IsDown)
            {
                // Está caída -> en la autoridad es dinámica, en los demás depende de cómo sincronices transform
                if (Object.HasStateAuthority)
                    SetDynamicLocal();
                else
                    ForceStaticLocal(); // si usas NetworkTransform, solo sigues la posición, no simulas física
            }
            else
            {
                // Está arriba -> resetear localmente a estado estático
                ForceStaticLocal();
                transform.position = _startPosition;
                transform.rotation = _startRotation;
            }
        }

        /// <summary>
        /// Lógica de caída (solo autoridad).
        /// </summary>
        private void StartFall()
        {
            IsTriggered = false;
            IsDown = true;

            _shakeTimer = 0f;

            // Solo la autoridad realmente simula la física
            SetDynamicLocal();

            RespawnTimer = TickTimer.CreateFromSeconds(Runner, RespawnDelay);
        }

        /// <summary>
        /// Respawn en la posición original (solo autoridad).
        /// </summary>
        private void ResetPlatform()
        {
            IsDown = false;

            transform.position = _startPosition;
            transform.rotation = _startRotation;

            _shakeTimer = 0f;

            SetStaticLocal();

            FallTimer = TickTimer.None;
            RespawnTimer = TickTimer.None;

            // Volvemos a ignorar triggers un pequeño tiempo por seguridad
            _ignoreTriggersTimer = IgnoreTriggersAfterSpawn;
        }

        // ==========================
        // Helpers de física local
        // ==========================

        /// <summary>
        /// Pone la plataforma estática (sin gravedad, kinemática).
        /// </summary>
        private void SetStaticLocal()
        {
            if (_rigidbody != null)
            {
                _rigidbody.useGravity = false;
                _rigidbody.isKinematic = true;
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Pone la plataforma dinámica (con gravedad).
        /// Solo debería llamarse en StateAuthority.
        /// </summary>
        private void SetDynamicLocal()
        {
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
                _rigidbody.useGravity = true;
            }
        }

        /// <summary>
        /// Fuerza estado estático local (para todos los peers al spawn).
        /// </summary>
        private void ForceStaticLocal()
        {
            SetStaticLocal();

            if (_colliders != null)
            {
                for (int i = 0; i < _colliders.Length; i++)
                {
                    if (_colliders[i] == null) continue;
                    _colliders[i].enabled = true;
                    // isTrigger lo configuras tú en el inspector (uno sólido, uno trigger).
                }
            }
        }
    }
}
