using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;
using UnityEngine.Rendering;

namespace Starter.Shooter
{
    /// <summary>
    /// Main player script - controls player movement, shooting and now soul collection/deposit.
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

        [Header("Fire Setup")]
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

        [Networked, HideInInspector, Capacity(24), OnChangedRender(nameof(OnNicknameChanged))]
        public string Nickname { get; set; }

        /// <summary>
        /// Total souls (carried). Used by UI as contador de almas que llevas encima.
        /// </summary>
        [Networked, HideInInspector]
        public int ChickenKills { get; set; }

        // Souls system
        /// <summary>Souls currently carried by the player (pure + corrupt).</summary>
        [Networked, HideInInspector]
        public int CarriedSouls { get; set; }

        /// <summary>Pure souls carried (obtained by hunting yokai).</summary>
        [Networked, HideInInspector]
        public int CarriedPureSouls { get; set; }

        /// <summary>Corrupt souls carried (obtained by killing other players).</summary>
        [Networked, HideInInspector]
        public int CarriedCorruptSouls { get; set; }

        /// <summary>Souls already deposited at the altar (pure + corrupt).</summary>
        [Networked, HideInInspector]
        public int BankedSouls { get; set; }

        /// <summary>Pure souls already deposited.</summary>
        [Networked, HideInInspector]
        public int BankedPureSouls { get; set; }

        /// <summary>Corrupt souls already deposited.</summary>
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
        private SoulAltar _altar;     // Resolved altar instance (override or scene search)
        private float _depositTimer;  // Timer for holding E near the altar

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                _gameManager = FindObjectOfType<GameManager>();

                // Resolve altar reference
                _altar = AltarOverride != null ? AltarOverride : FindObjectOfType<SoulAltar>();

                // Set player nickname that is saved in UIGameMenu
                Nickname = PlayerPrefs.GetString("PlayerName");
            }

            // In case the nickname is already changed,
            // we need to trigger the change manually
            OnNicknameChanged();

            // Reset visible fire count
            _visibleFireCount = _fireCount;

            if (HasStateAuthority)
            {
                // For input authority deactivate head renderers so they are not obstructing the view
                for (int i = 0; i < HeadRenderers.Length; i++)
                {
                    HeadRenderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }

                // Some objects (e.g. weapon) are renderer with secondary Overlay camera.
                // This prevents weapon clipping into the wall when close to the wall.
                int overlayLayer = LayerMask.NameToLayer("FirstPersonOverlay");
                for (int i = 0; i < FirstPersonOverlayObjects.Length; i++)
                {
                    FirstPersonOverlayObjects[i].layer = overlayLayer;
                }

                // Look rotation interpolation is skipped for local player.
                // Look rotation is set manually in Render.
                KCC.Settings.ForcePredictedLookRotation = true;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (KCC.Position.y < -15f)
            {
                // Player fell, let's kill him
                Health.TakeHit(1000);
            }

            if (Health.IsFinished)
            {
                // Si no tenemos referencia al GameManager, la buscamos una vez
                if (_gameManager == null)
                {
                    _gameManager = FindObjectOfType<GameManager>();
                }

                bool canRespawn = true;

                if (_gameManager != null)
                {
                    // Durante sudden death (últimos 30 segundos) y antes de que termine la partida,
                    // las muertes son definitivas.
                    if (_gameManager.IsSuddenDeath && _gameManager.MatchEnded == false)
                    {
                        canRespawn = false;
                    }
                }

                if (canRespawn)
                {
                    Respawn(_gameManager.GetSpawnPosition());
                }

                // Si no puede respawnear, simplemente se queda muerto.
                // Nos aseguramos de ocultar cualquier UI de depósito activa.
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

            // Manejo del altar (depositar almas + UI de progreso)
            HandleAltarDeposit(input);

            if (KCC.IsGrounded)
            {
                // Stop jumping
                _isJumping = false;
            }

            KCC.SetActive(Health.IsAlive);

            PlayerInput.ResetInput();
        }

        public override void Render()
        {
            if (HasStateAuthority)
            {
                // Set look rotation for Render.
                KCC.SetLookRotation(PlayerInput.CurrentInput.LookRotation, -90f, 90f);
            }

            // Transform velocity vector to local space.
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

            // Disable hits when player is dead
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

            // Update camera pivot (influences ChestIK)
            // (KCC look rotation is set earlier in Render)
            var pitchRotation = KCC.GetLookRotation(true, false);
            CameraPivot.localRotation = Quaternion.Euler(pitchRotation);

            // Dummy IK solution, we are snapping chest bone to prepared ChestTargetPosition position
            // Lerping blends the fixed position with little bit of animation position.
            float blendAmount = HasStateAuthority ? 0.05f : 0.2f;
            ChestBone.position = Vector3.Lerp(ChestTargetPosition.position, ChestBone.position, blendAmount);
            ChestBone.rotation = Quaternion.Lerp(ChestTargetPosition.rotation, ChestBone.rotation, blendAmount);

            // Only local player needs to update the camera
            if (HasStateAuthority)
            {
                // Transfer properties from camera handle to Main Camera.
                Camera.main.transform.SetPositionAndRotation(CameraHandle.position, CameraHandle.rotation);
            }
        }

        private void ProcessInput(GameplayInput input)
        {
            KCC.SetLookRotation(input.LookRotation, -90f, 90f);

            // It feels better when player falls quicker
            KCC.SetGravity(KCC.RealVelocity.y >= 0f ? UpGravity : DownGravity);

            // Calculate correct move direction from input (rotated based on latest KCC rotation)
            var moveDirection = KCC.TransformRotation * new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);
            var desiredMoveVelocity = moveDirection * WalkSpeed;

            float acceleration;
            if (desiredMoveVelocity == Vector3.zero)
            {
                // No desired move velocity - we are stopping.
                acceleration = KCC.IsGrounded == true ? GroundDeceleration : AirDeceleration;
            }
            else
            {
                acceleration = KCC.IsGrounded == true ? GroundAcceleration : AirAcceleration;
            }

            _moveVelocity = Vector3.Lerp(_moveVelocity, desiredMoveVelocity, acceleration * Runner.DeltaTime);
            float jumpImpulse = 0f;

            // Comparing current input buttons to previous input buttons - this prevents glitches when input is lost
            if (KCC.IsGrounded && input.Jump)
            {
                // Set world space jump vector
                jumpImpulse = JumpImpulse;
                _isJumping = true;
            }

            KCC.Move(_moveVelocity, jumpImpulse);

            // Update camera pivot so fire transform (CameraHandle) is correct
            var pitchRotation = KCC.GetLookRotation(true, false);
            CameraPivot.localRotation = Quaternion.Euler(pitchRotation);

            if (input.Fire)
            {
                Fire();
            }
        }

        /// <summary>
        /// Maneja la lógica de depósito en el altar (mantener E) y notifica al altar
        /// para que actualice la UI de progreso.
        /// Solo corre en la autoridad de estado.
        /// </summary>
        private void HandleAltarDeposit(GameplayInput input)
        {
            if (HasStateAuthority == false)
                return;

            // Resolver altar una vez
            if (_altar == null)
            {
                _altar = AltarOverride != null ? AltarOverride : FindObjectOfType<SoulAltar>();
            }

            if (_altar == null)
            {
                _depositTimer = 0f;
                return;
            }

            // Si está muerto, no canaliza
            if (Health.IsAlive == false)
            {
                _depositTimer = 0f;
                _altar.UpdateDepositUI(false, 0f, _altar.HoldTimeToDeposit);
                return;
            }

            // Si no lleva almas, no canaliza
            if (CarriedSouls <= 0)
            {
                _depositTimer = 0f;
                _altar.UpdateDepositUI(false, 0f, _altar.HoldTimeToDeposit);
                return;
            }

            // Distancia al altar
            float distance = Vector3.Distance(KCC.Position, _altar.transform.position);
            if (distance > _altar.InteractionRadius)
            {
                _depositTimer = 0f;
                _altar.UpdateDepositUI(false, 0f, _altar.HoldTimeToDeposit);
                return;
            }

            // Está cerca del altar, vivo y con almas encima
            if (input.Interact)
            {
                _depositTimer += Runner.DeltaTime;

                float progress = Mathf.Clamp01(_depositTimer / _altar.HoldTimeToDeposit);
                float remaining = Mathf.Max(0f, _altar.HoldTimeToDeposit - _depositTimer);

                // Mostrar/actualizar la barra
                _altar.UpdateDepositUI(true, progress, remaining);

                if (_depositTimer >= _altar.HoldTimeToDeposit)
                {
                    // Depósito completado
                    DepositSoulsIntoAltar();

                    // Reset timer + ocultar UI
                    _depositTimer = 0f;
                    _altar.UpdateDepositUI(false, 0f, _altar.HoldTimeToDeposit);
                }
            }
            else
            {
                // Soltó la E -> cancelar canalización
                _depositTimer = 0f;
                _altar.UpdateDepositUI(false, 0f, _altar.HoldTimeToDeposit);
            }
        }

        private void Fire()
        {
            // Clear hit position in case nothing will be hit
            _hitPosition = Vector3.zero;

            // Whole projectile path and effects are immediately processed (= hitscan projectile)
            if (Physics.Raycast(CameraHandle.position, CameraHandle.forward, out var hitInfo, 200f, HitMask))
            {
                // Deal damage
                var health = hitInfo.collider != null ? hitInfo.collider.GetComponentInParent<Health>() : null;
                if (health != null)
                {
                    health.Killed = OnEnemyKilled;
                    health.TakeHit(1, true);
                }

                // Save hit point to correctly show bullet path on all clients.
                // This however works only for single projectile per FUN and with higher fire cadence
                // some projectiles might not be fired on proxies because we save only the position
                // of the LAST hit.
                _hitPosition = hitInfo.point;
                _hitNormal = hitInfo.normal;
            }

            // In this example projectile count property (fire count) is used not only for weapon fire effects
            // but to spawn the projectile visuals themselves.
            _fireCount++;
        }

        private void Respawn(Vector3 position)
        {
            // On respawn we only clear what the player was carrying.
            // Banked souls (already offered at the altar) remain.
            CarriedSouls = 0;
            CarriedPureSouls = 0;
            CarriedCorruptSouls = 0;
            _depositTimer = 0f;

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

        /// <summary>
        /// Called when this player has killed another entity (chicken or player).
        /// </summary>
        private void OnEnemyKilled(Health enemyHealth)
        {
            // Chicken / yokai = pure souls
            if (enemyHealth.GetComponent<Chicken>() != null)
            {
                CarriedPureSouls += 1;
                CarriedSouls += 1;

                UpdateTotalSouls(this);
                return;
            }

            // Player kill = steal 60% of carried souls from the victim
            var victimPlayer = enemyHealth.GetComponent<Player>();
            if (victimPlayer != null)
            {
                StealSoulsFromPlayer(victimPlayer);
            }

            UpdateTotalSouls(this);
        }

        /// <summary>
        /// Steals 60% of the souls the victim is currently carrying.
        /// Stolen souls become corrupt on the killer.
        /// </summary>
        private void StealSoulsFromPlayer(Player victim)
        {
            if (victim == null)
                return;

            int victimCarried = victim.CarriedSouls;
            if (victimCarried <= 0)
                return;

            // 60% of carried souls, floored
            int amountToSteal = Mathf.FloorToInt(victimCarried * 0.6f);
            if (amountToSteal <= 0)
                return;

            // Remove from victim: first from pure, then from corrupt
            int stealFromPure = Mathf.Min(amountToSteal, victim.CarriedPureSouls);
            int stealFromCorrupt = amountToSteal - stealFromPure;
            stealFromCorrupt = Mathf.Min(stealFromCorrupt, victim.CarriedCorruptSouls);

            victim.CarriedPureSouls -= stealFromPure;
            victim.CarriedCorruptSouls -= stealFromCorrupt;
            victim.CarriedSouls -= amountToSteal;
            if (victim.CarriedSouls < 0)
                victim.CarriedSouls = 0;

            // All stolen souls become corrupt on the killer
            CarriedCorruptSouls += amountToSteal;
            CarriedSouls += amountToSteal;

            UpdateTotalSouls(victim);
            UpdateTotalSouls(this);
        }

        /// <summary>
        /// Deposits all carried souls into the altar as banked souls.
        /// Pure stay pure, corrupt stay corrupt.
        /// </summary>
        private void DepositSoulsIntoAltar()
        {
            if (CarriedSouls <= 0)
                return;

            // Mover lo que llevas al banco
            BankedPureSouls += CarriedPureSouls;
            BankedCorruptSouls += CarriedCorruptSouls;

            // Limpiar lo que llevas encima
            CarriedPureSouls = 0;
            CarriedCorruptSouls = 0;
            CarriedSouls = 0;

            // Recalcular total bancado
            BankedSouls = BankedPureSouls + BankedCorruptSouls;

            // Actualizar contador visible (al depositar, se va a 0)
            UpdateTotalSouls(this);
        }

        /// <summary>
        /// Keeps ChickenKills in sync as carried souls.
        /// This is used by UI para mostrar "Almas" actuales.
        /// </summary>
        private static void UpdateTotalSouls(Player p)
        {
            if (p == null)
                return;

            p.ChickenKills = p.CarriedSouls;
        }

        private void ShowFireEffects()
        {
            // Notice we are not using OnChangedRender for fireCount property but instead
            // we are checking against a local variable and show fire effects only when visible
            // fire count is SMALLER. This prevents triggering false fire effects when
            // local player mispredicted fire (e.g. input got lost) and fireCount property got decreased.
            if (_visibleFireCount < _fireCount)
            {
                FireSound.PlayOneShot(FireSound.clip);
                MuzzleParticle.Play();
                Animator.SetTrigger(_animIDShoot);

                if (_hitPosition != Vector3.zero)
                {
                    // Impact gets destroyed automatically with DestroyAfter script
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
                return; // Do not show nickname for local player

            Nameplate.SetNickname(Nickname);
        }
    }
}
