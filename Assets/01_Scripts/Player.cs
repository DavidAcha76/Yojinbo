using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;
using UnityEngine.Rendering;

namespace Starter.Shooter
{
    /// <summary>
    /// Main player script - controls player movement, shooting, souls, altar and grappling hook.
    /// </summary>
    public sealed class Player : NetworkBehaviour
    {
        [Header("References")]
        public Health Health;
        public SimpleKCC KCC;
        public PlayerInput PlayerInput;
        public Animator Animator;
        public Transform CameraPivot;
        public Transform CameraHandle;
        public Transform ScalingRoot;
        public UINameplate Nameplate;
        public Collider Hitbox;
        public Renderer[] HeadRenderers;
        public GameObject[] FirstPersonOverlayObjects;

        [Header("Movement Setup")]
        public float WalkSpeed = 2f;
        public float JumpImpulse = 10f;
        public float UpGravity = 25f;
        public float DownGravity = 40f;

        [Header("Movement Accelerations")]
        public float GroundAcceleration = 55f;
        public float GroundDeceleration = 25f;
        public float AirAcceleration = 25f;
        public float AirDeceleration = 1.3f;

        [Header("Fire Setup (Gun)")]
        public LayerMask HitMask;
        public GameObject ImpactPrefab;
        public ParticleSystem MuzzleParticle;

        [Header("Animation Setup")]
        public Transform ChestTargetPosition;
        public Transform ChestBone;

        [Header("Sounds")]
        public AudioSource FireSound;
        public AudioSource FootstepSound;
        public AudioClip JumpAudioClip;
        public AudioClip LandAudioClip;

        [Header("VFX")]
        public ParticleSystem DustParticles;

        [Header("Souls / Altar")]
        [Tooltip("Optional: manually assign; otherwise Player will FindObjectOfType<SoulAltar>() on spawn.")]
        public SoulAltar AltarOverride;

        [Header("Grapple Setup")]
        [Tooltip("Prefab del proyectil del garfio (debe tener GrappleProjectile + LineRenderer).")]
        public GameObject GrappleProjectilePrefab;

        [Tooltip("Punta del arma donde empieza la cadena. Si es null, se usa CameraHandle.")]
        public Transform GrappleMuzzle;

        [Tooltip("Velocidad del proyectil del garfio.")]
        public float GrappleProjectileSpeed = 40f;

        [Tooltip("Distancia máxima del garfio.")]
        public float GrappleMaxDistance = 40f;

        [Tooltip("Velocidad a la que el jugador es jalado hacia el punto de impacto.")]
        public float GrapplePullSpeed = 20f;

        [Tooltip("Distancia mínima al objetivo para terminar el jalón.")]
        public float GrappleStopDistance = 1.5f;

        [Tooltip("Capas válidas para enganchar el garfio.")]
        public LayerMask GrappleHitMask;

        [Networked, HideInInspector, Capacity(24), OnChangedRender(nameof(OnNicknameChanged))]
        public string Nickname { get; set; }

        /// <summary>
        /// Total souls (carried). Used by UI as contador de almas actuales.
        /// </summary>
        [Networked, HideInInspector]
        public int ChickenKills { get; set; }

        // Souls system
        [Networked, HideInInspector]
        public int CarriedSouls { get; set; }

        [Networked, HideInInspector]
        public int CarriedPureSouls { get; set; }

        [Networked, HideInInspector]
        public int CarriedCorruptSouls { get; set; }

        [Networked, HideInInspector]
        public int BankedSouls { get; set; }

        [Networked, HideInInspector]
        public int BankedPureSouls { get; set; }

        [Networked, HideInInspector]
        public int BankedCorruptSouls { get; set; }

        [Networked, OnChangedRender(nameof(OnJumpingChanged))]
        private NetworkBool _isJumping { get; set; }
        [Networked]
        private Vector3 _hitPosition { get; set; }
        [Networked]
        private Vector3 _hitNormal { get; set; }
        [Networked]
        private int _fireCount { get; set; }

        // Animation IDs
        private int _animIDSpeedX;
        private int _animIDSpeedZ;
        private int _animIDMoveSpeedZ;
        private int _animIDGrounded;
        private int _animIDPitch;
        private int _animIDShoot;

        private Vector3 _moveVelocity;
        private int _visibleFireCount;

        private GameManager _gameManager;
        private SoulAltar _altar;
        private float _depositTimer;

        // Grapple state
        private bool _isGrappling;
        private Vector3 _grappleTarget;
        private GrappleProjectile _activeGrappleProjectile;

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                _gameManager = FindObjectOfType<GameManager>();

                _altar = AltarOverride != null ? AltarOverride : FindObjectOfType<SoulAltar>();

                Nickname = PlayerPrefs.GetString("PlayerName");
            }

            OnNicknameChanged();

            _visibleFireCount = _fireCount;

            if (HasStateAuthority)
            {
                for (int i = 0; i < HeadRenderers.Length; i++)
                {
                    HeadRenderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }

                int overlayLayer = LayerMask.NameToLayer("FirstPersonOverlay");
                for (int i = 0; i < FirstPersonOverlayObjects.Length; i++)
                {
                    FirstPersonOverlayObjects[i].layer = overlayLayer;
                }

                KCC.Settings.ForcePredictedLookRotation = true;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (KCC.Position.y < -15f)
            {
                Health.TakeHit(1000);
            }

            if (Health.IsFinished)
            {
                if (_gameManager == null)
                {
                    _gameManager = FindObjectOfType<GameManager>();
                }

                bool canRespawn = true;

                if (_gameManager != null)
                {
                    if (_gameManager.IsSuddenDeath && _gameManager.MatchEnded == false)
                    {
                        canRespawn = false;
                    }
                }

                if (canRespawn)
                {
                    Respawn(_gameManager.GetSpawnPosition());
                }

                _isGrappling = false;

                if (_altar != null)
                {
                    _altar.UpdateDepositUI(false, 0f, _altar.HoldTimeToDeposit);
                }

                KCC.SetActive(false);
                PlayerInput.ResetInput();
                return;
            }

            var input = Health.IsAlive ? PlayerInput.CurrentInput : default;

            ProcessInput(input);

            HandleAltarDeposit(input);

            if (KCC.IsGrounded)
            {
                _isJumping = false;
            }

            KCC.SetActive(Health.IsAlive);

            PlayerInput.ResetInput();
        }

        public override void Render()
        {
            if (HasStateAuthority)
            {
                KCC.SetLookRotation(PlayerInput.CurrentInput.LookRotation, -90f, 90f);
            }

            var moveSpeed = transform.InverseTransformVector(KCC.RealVelocity);

            Animator.SetFloat(_animIDSpeedX, moveSpeed.x, 0.1f, Time.deltaTime);
            Animator.SetFloat(_animIDSpeedZ, moveSpeed.z, 0.1f, Time.deltaTime);
            Animator.SetBool(_animIDGrounded, KCC.IsGrounded);
            Animator.SetFloat(_animIDPitch, KCC.GetLookRotation(true, false).x, 0.02f, Time.deltaTime);

            FootstepSound.enabled = KCC.IsGrounded && KCC.RealSpeed > 1f;
            ScalingRoot.localScale = Vector3.Lerp(ScalingRoot.localScale, Vector3.one, Time.deltaTime * 8f);

            var emission = DustParticles.emission;
            emission.enabled = KCC.IsGrounded && KCC.RealSpeed > 1f;

            ShowFireEffects();

            Hitbox.enabled = Health.IsAlive;
        }

        private void Awake()
        {
            AssignAnimationIDs();
        }

        private void LateUpdate()
        {
            if (Health.IsAlive == false)
                return;

            var pitchRotation = KCC.GetLookRotation(true, false);
            CameraPivot.localRotation = Quaternion.Euler(pitchRotation);

            float blendAmount = HasStateAuthority ? 0.05f : 0.2f;
            ChestBone.position = Vector3.Lerp(ChestTargetPosition.position, ChestBone.position, blendAmount);
            ChestBone.rotation = Quaternion.Lerp(ChestTargetPosition.rotation, ChestBone.rotation, blendAmount);

            if (HasStateAuthority)
            {
                Camera.main.transform.SetPositionAndRotation(CameraHandle.position, CameraHandle.rotation);
            }
        }

        private void ProcessInput(GameplayInput input)
        {
            KCC.SetLookRotation(input.LookRotation, -90f, 90f);

            // Si estamos siendo jalados por el garfio, priorizamos ese movimiento
            if (_isGrappling)
            {
                HandleGrappleMovement();
                return;
            }

            KCC.SetGravity(KCC.RealVelocity.y >= 0f ? UpGravity : DownGravity);

            var moveDirection = KCC.TransformRotation * new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);
            var desiredMoveVelocity = moveDirection * WalkSpeed;

            float acceleration;
            if (desiredMoveVelocity == Vector3.zero)
            {
                acceleration = KCC.IsGrounded ? GroundDeceleration : AirDeceleration;
            }
            else
            {
                acceleration = KCC.IsGrounded ? GroundAcceleration : AirAcceleration;
            }

            _moveVelocity = Vector3.Lerp(_moveVelocity, desiredMoveVelocity, acceleration * Runner.DeltaTime);
            float jumpImpulse = 0f;

            if (KCC.IsGrounded && input.Jump)
            {
                jumpImpulse = JumpImpulse;
                _isJumping = true;
            }

            KCC.Move(_moveVelocity, jumpImpulse);

            var pitchRotation = KCC.GetLookRotation(true, false);
            CameraPivot.localRotation = Quaternion.Euler(pitchRotation);

            // Click izquierdo: pistola normal
            if (input.Fire)
            {
                FireGun();
            }

            // Click derecho: garfio
            if (input.AltFire)
            {
                FireGrapple();
            }
        }

        /// <summary>
        /// Movimiento durante el jalón del garfio.
        /// </summary>
        private void HandleGrappleMovement()
        {
            Vector3 toTarget = _grappleTarget - KCC.Position;
            float distance = toTarget.magnitude;

            if (distance <= GrappleStopDistance)
            {
                _isGrappling = false;
                return;
            }

            Vector3 dir = toTarget.normalized;
            Vector3 velocity = dir * GrapplePullSpeed;

            KCC.Move(velocity, 0f);
        }

        /// <summary>
        /// Llamado por el proyectil de garfio cuando impacta.
        /// </summary>
        public void StartGrapple(Vector3 target)
        {
            if (!Health.IsAlive)
                return;

            _grappleTarget = target;
            _isGrappling = true;
        }

        /// <summary>
        /// Llamado por el proyectil cuando se destruye (hit o sin hit).
        /// </summary>
        public void OnGrappleProjectileFinished(bool didHit)
        {
            _activeGrappleProjectile = null;

            // Si no hubo hit, aseguramos que no quede en modo grappling raro
            if (!didHit)
            {
                _isGrappling = false;
            }
        }

        /// <summary>
        /// Pistola normal (hitscan).
        /// </summary>
        private void FireGun()
        {
            _hitPosition = Vector3.zero;

            if (Physics.Raycast(CameraHandle.position, CameraHandle.forward, out var hitInfo, 200f, HitMask))
            {
                var health = hitInfo.collider != null ? hitInfo.collider.GetComponentInParent<Health>() : null;
                if (health != null)
                {
                    health.Killed = OnEnemyKilled;
                    health.TakeHit(1, true);
                }

                _hitPosition = hitInfo.point;
                _hitNormal = hitInfo.normal;
            }

            _fireCount++;
        }

        /// <summary>
        /// Pistola-garfio: dispara un proyectil con cadena.
        /// </summary>
        private void FireGrapple()
        {
            if (GrappleProjectilePrefab == null)
                return;

            // No disparar si ya hay un garfio activo o ya estás siendo jalado
            if (_activeGrappleProjectile != null || _isGrappling)
                return;

            Transform muzzle = GrappleMuzzle != null ? GrappleMuzzle : CameraHandle;
            Vector3 spawnPos = muzzle.position;

            // Rotación no es crítica, la dirección REAl viene de la cámara
            //Quaternion spawnRot = Quaternion.LookRotation(CameraHandle.forward);
            Quaternion spawnRot = Quaternion.Euler(90, 90, -90); 

            var projGO = Instantiate(GrappleProjectilePrefab, spawnPos, spawnRot);
            var proj = projGO.GetComponent<GrappleProjectile>();
            if (proj != null)
            {
                _activeGrappleProjectile = proj;

                proj.Init(
                    this,
                    muzzle,
                    CameraHandle.forward,       // Dirección 100% basada en la cámara
                    GrappleProjectileSpeed,
                    GrappleMaxDistance,
                    GrappleHitMask
                );
            }
        }

        private void Respawn(Vector3 position)
        {
            CarriedSouls = 0;
            CarriedPureSouls = 0;
            CarriedCorruptSouls = 0;
            _depositTimer = 0f;

            _isGrappling = false;
            _activeGrappleProjectile = null;

            if (_altar != null)
            {
                _altar.UpdateDepositUI(false, 0f, _altar.HoldTimeToDeposit);
            }

            UpdateTotalSouls(this);

            Health.Revive();

            KCC.SetPosition(position);
            KCC.SetLookRotation(0f, 0f);

            _moveVelocity = Vector3.zero;
        }

        private void HandleAltarDeposit(GameplayInput input)
        {
            if (HasStateAuthority == false)
                return;

            if (_altar == null)
            {
                _altar = AltarOverride != null ? AltarOverride : FindObjectOfType<SoulAltar>();
            }

            if (_altar == null)
            {
                _depositTimer = 0f;
                return;
            }

            if (Health.IsAlive == false)
            {
                _depositTimer = 0f;
                _altar.UpdateDepositUI(false, 0f, _altar.HoldTimeToDeposit);
                return;
            }

            if (CarriedSouls <= 0)
            {
                _depositTimer = 0f;
                _altar.UpdateDepositUI(false, 0f, _altar.HoldTimeToDeposit);
                return;
            }

            float distance = Vector3.Distance(KCC.Position, _altar.transform.position);
            if (distance > _altar.InteractionRadius)
            {
                _depositTimer = 0f;
                _altar.UpdateDepositUI(false, 0f, _altar.HoldTimeToDeposit);
                return;
            }

            if (input.Interact)
            {
                _depositTimer += Runner.DeltaTime;

                float progress = Mathf.Clamp01(_depositTimer / _altar.HoldTimeToDeposit);
                float remaining = Mathf.Max(0f, _altar.HoldTimeToDeposit - _depositTimer);

                _altar.UpdateDepositUI(true, progress, remaining);

                if (_depositTimer >= _altar.HoldTimeToDeposit)
                {
                    DepositSoulsIntoAltar();
                    _depositTimer = 0f;
                    _altar.UpdateDepositUI(false, 0f, _altar.HoldTimeToDeposit);
                }
            }
            else
            {
                _depositTimer = 0f;
                _altar.UpdateDepositUI(false, 0f, _altar.HoldTimeToDeposit);
            }
        }

        private void OnEnemyKilled(Health enemyHealth)
        {
            if (enemyHealth.GetComponent<Chicken>() != null)
            {
                CarriedPureSouls += 1;
                CarriedSouls += 1;

                UpdateTotalSouls(this);
                return;
            }

            var victimPlayer = enemyHealth.GetComponent<Player>();
            if (victimPlayer != null)
            {
                StealSoulsFromPlayer(victimPlayer);
            }

            UpdateTotalSouls(this);
        }

        private void StealSoulsFromPlayer(Player victim)
        {
            if (victim == null)
                return;

            int victimCarried = victim.CarriedSouls;
            if (victimCarried <= 0)
                return;

            int amountToSteal = Mathf.FloorToInt(victimCarried * 0.6f);
            if (amountToSteal <= 0)
                return;

            int stealFromPure = Mathf.Min(amountToSteal, victim.CarriedPureSouls);
            int stealFromCorrupt = amountToSteal - stealFromPure;
            stealFromCorrupt = Mathf.Min(stealFromCorrupt, victim.CarriedCorruptSouls);

            victim.CarriedPureSouls -= stealFromPure;
            victim.CarriedCorruptSouls -= stealFromCorrupt;
            victim.CarriedSouls -= amountToSteal;
            if (victim.CarriedSouls < 0)
                victim.CarriedSouls = 0;

            CarriedCorruptSouls += amountToSteal;
            CarriedSouls += amountToSteal;

            UpdateTotalSouls(victim);
            UpdateTotalSouls(this);
        }

        private void DepositSoulsIntoAltar()
        {
            if (CarriedSouls <= 0)
                return;

            BankedPureSouls += CarriedPureSouls;
            BankedCorruptSouls += CarriedCorruptSouls;

            CarriedPureSouls = 0;
            CarriedCorruptSouls = 0;
            CarriedSouls = 0;

            BankedSouls = BankedPureSouls + BankedCorruptSouls;

            UpdateTotalSouls(this);
        }

        private static void UpdateTotalSouls(Player p)
        {
            if (p == null)
                return;

            p.ChickenKills = p.CarriedSouls;
        }

        private void ShowFireEffects()
        {
            if (_visibleFireCount < _fireCount)
            {
                FireSound.PlayOneShot(FireSound.clip);
                MuzzleParticle.Play();
                Animator.SetTrigger(_animIDShoot);

                if (_hitPosition != Vector3.zero)
                {
                    Instantiate(ImpactPrefab, _hitPosition, Quaternion.LookRotation(_hitNormal));
                }
            }

            _visibleFireCount = _fireCount;
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeedX = Animator.StringToHash("SpeedX");
            _animIDSpeedZ = Animator.StringToHash("SpeedZ");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDPitch = Animator.StringToHash("Pitch");
            _animIDShoot = Animator.StringToHash("Shoot");
        }

        private void OnJumpingChanged()
        {
            if (_isJumping)
            {
                AudioSource.PlayClipAtPoint(JumpAudioClip, KCC.Position, 0.5f);
            }
            else
            {
                AudioSource.PlayClipAtPoint(LandAudioClip, KCC.Position, 1f);
            }

            if (HasStateAuthority == false)
            {
                ScalingRoot.localScale = _isJumping ? new Vector3(0.5f, 1.5f, 0.5f) : new Vector3(1.25f, 0.75f, 1.25f);
            }
        }

        private void OnNicknameChanged()
        {
            if (HasStateAuthority)
                return;

            Nameplate.SetNickname(Nickname);
        }
    }
}
