using UnityEngine;
using Fusion;

namespace Starter.Shooter
{
    /// <summary>
    /// Plataforma que cae cuando un jugador la pisa.
    /// - Al pisarla: espera "FallDelay" segundos mientras tiembla.
    /// - Luego "cae" moviéndose hacia abajo sin usar rigidbody dinámico.
    /// - Permanece caída "RespawnDelay" segundos.
    /// - Después vuelve a su posición inicial y se reactiva.
    /// 
    /// Setup recomendado:
    /// - Mismo GameObject:
    ///   - Rigidbody:
    ///       - Use Gravity = false
    ///       - Is Kinematic = true
    ///   - Colliders:
    ///       - Uno sólido (isTrigger = false) para el piso.
    ///       - (Opcional) Uno extra con isTrigger = true para detección del player.
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

        [Header("Movimiento de caída (sin física dinámica)")]
        [Tooltip("Distancia que se moverá hacia abajo cuando caiga.")]
        public float FallDistance = 5f;

        [Tooltip("Velocidad a la que cae (unidades por segundo).")]
        public float FallSpeed = 10f;

        [Header("Protección de spawn")]
        [Tooltip("Tiempo tras el spawn en el que ignora triggers para que no caiga sola.")]
        public float IgnoreTriggersAfterSpawn = 0.3f;

        private Rigidbody _rigidbody;
        private Collider[] _colliders;

        // Posición/rotación inicial
        private Vector3 _startPosition;
        private Quaternion _startRotation;

        // Temblor local
        private float _shakeTimer;
        private Vector3 _shakeBasePos;

        // Timer local para ignorar triggers al inicio (solo lo usa la autoridad)
        private float _ignoreTriggersTimer;

        // Para animar la caída sin física
        private bool _localIsFalling;
        private float _currentFallOffset;

        // Timers de red
        [Networked]
        private TickTimer FallTimer { get; set; }

        [Networked]
        private TickTimer RespawnTimer { get; set; }

        // Estados sincronizados
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

            // Forzar que SIEMPRE sea kinemático y sin gravedad (para evitar el error de Unity)
            ForceStaticLocal();

            if (Object.HasStateAuthority)
            {
                IsTriggered = false;
                IsDown = false;
                FallTimer = TickTimer.None;
                RespawnTimer = TickTimer.None;
                _ignoreTriggersTimer = IgnoreTriggersAfterSpawn;
            }

            _localIsFalling = false;
            _currentFallOffset = 0f;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority)
                return;

            // Ignorar triggers un rato tras el spawn
            if (_ignoreTriggersTimer > 0f)
            {
                _ignoreTriggersTimer -= Runner.DeltaTime;
            }

            // Si está "armada" para caer y el timer expira -> cae
            if (IsTriggered && FallTimer.Expired(Runner))
            {
                StartFall();
            }

            // Si está abajo y el timer de respawn expira -> respawn
            if (IsDown && RespawnTimer.Expired(Runner))
            {
                ResetPlatform();
            }
        }

        private void Update()
        {
            // Temblor previo a la caída
            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.deltaTime;

                // Solo temblar mientras NO está caída y no está en anim de caída
                if (!_localIsFalling)
                {
                    float t = Time.time * ShakeFrequency;

                    float offsetY = Mathf.Sin(t) * ShakeAmplitude;
                    float offsetX = Mathf.Cos(t * 0.7f) * ShakeAmplitude * 0.5f;

                    transform.position = _shakeBasePos + new Vector3(offsetX, offsetY, 0f);
                }

                if (_shakeTimer <= 0f && !_localIsFalling)
                {
                    transform.position = _shakeBasePos;
                }
            }

            // Animación de caída sin física
            if (_localIsFalling)
            {
                _currentFallOffset += FallSpeed * Time.deltaTime;

                if (_currentFallOffset >= FallDistance)
                {
                    _currentFallOffset = FallDistance;
                    _localIsFalling = false;
                }

                transform.position = _startPosition + Vector3.down * _currentFallOffset;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!Object.HasStateAuthority)
                return;

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

            // Marcar como triggered y arrancar el timer de caída
            IsTriggered = true;
            FallTimer = TickTimer.CreateFromSeconds(Runner, FallDelay);
        }

        /// <summary>
        /// Se llama en todos los clientes cuando IsTriggered cambia.
        /// Maneja el inicio/fin del shake.
        /// </summary>
        private void OnTriggeredChanged()
        {
            if (IsTriggered)
            {
                _shakeBasePos = transform.position;
                _shakeTimer = FallDelay;
            }
            else
            {
                _shakeTimer = 0f;
                if (!_localIsFalling)
                {
                    transform.position = _shakeBasePos;
                }
            }
        }

        /// <summary>
        /// Se llama cuando IsDown cambia (true = caída, false = arriba).
        /// </summary>
        private void OnDownChanged()
        {
            if (IsDown)
            {
                // Empezar animación de caída en todos
                _localIsFalling = true;
                _currentFallOffset = 0f;
                _shakeTimer = 0f;
            }
            else
            {
                // Volver a la posición original en todos
                _localIsFalling = false;
                _currentFallOffset = 0f;
                transform.position = _startPosition;
                transform.rotation = _startRotation;
                _shakeTimer = 0f;

                ForceStaticLocal();
            }
        }

        /// <summary>
        /// Lógica de inicio de caída (solo autoridad).
        /// </summary>
        private void StartFall()
        {
            IsTriggered = false;
            IsDown = true;

            // Arrancamos timer de estar abajo
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, RespawnDelay);
        }

        /// <summary>
        /// Respawn (solo autoridad).
        /// </summary>
        private void ResetPlatform()
        {
            IsDown = false;

            FallTimer = TickTimer.None;
            RespawnTimer = TickTimer.None;

            _ignoreTriggersTimer = IgnoreTriggersAfterSpawn;
        }

        // ==========================
        // Helpers de física local
        // ==========================

        /// <summary>
        /// Fuerza Rigidbody como kinemático + sin gravedad (para evitar concave+dinámico).
        /// </summary>
        private void ForceStaticLocal()
        {
            if (_rigidbody != null)
            {
                _rigidbody.useGravity = false;
                _rigidbody.isKinematic = true;
                _rigidbody.velocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

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
