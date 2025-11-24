using UnityEngine;
using Fusion;
using Fusion.Addons.SimpleKCC;
using UnityEngine.Rendering;

namespace Starter.Shooter
{
    public sealed class Player : NetworkBehaviour
    {
        private const byte SHOT_NONE = 0;
        private const byte SHOT_NORMAL = 1;
        private const byte SHOT_SPECIAL = 2;
        private const byte SHOT_CAGE = 3;

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

        [Header("Base Sounds")]
        public AudioSource FootstepSound;
        public AudioClip JumpAudioClip;
        public AudioClip LandAudioClip;

        [Header("Weapon Sounds")]
        public AudioSource FireSound;
        public AudioClip FireGunClip;

        public AudioSource ReloadSound;
        public AudioClip ReloadClip;

        [Header("Transformation Sounds")]
        public AudioSource AngelSound;
        public AudioClip AngelTransformLoopClip;

        public AudioSource DemonSound;
        public AudioClip DemonTransformLoopClip;

        [Header("Special Soul Shot Sounds")]
        public AudioSource SpecialShotSound;
        public AudioClip SpecialShotClip;

        [Header("Invisibility Sound")]
        public AudioSource InvisibilitySound;
        public AudioClip InvisibilityClip;

        [Header("Heal Sound")]
        public AudioSource HealSound;
        public AudioClip HealClip;

        [Header("Cage Shot Sounds")]
        public AudioSource CageShotSound;
        public AudioClip CageShotClip;

        [Header("VFX")]
        public ParticleSystem DustParticles;

        [Header("Souls / Altar")]
        public SoulAltar AltarOverride;

        [Header("Grapple Setup")]
        public GameObject GrappleProjectilePrefab;
        public Transform GrappleMuzzle;
        public float GrappleProjectileSpeed = 40f;
        public float GrappleMaxDistance = 40f;
        public float GrapplePullSpeed = 20f;
        public float GrappleStopDistance = 1.5f;
        public LayerMask GrappleHitMask;

        [Header("Special Soul Shot")]
        public int SpecialShotSoulCost = 10;
        public int SpecialShotDamage = 4;
        public float SpecialShotCooldown = 3f;
        public float SpecialShotRange = 200f;
        public LayerMask SpecialShotHitMask;

        private float _specialShotCooldownTimer;

        [Header("Ammo Setup")]
        public int MaxAmmo = 20;
        public float ReloadDuration = 3f;

        [Networked, HideInInspector]
        public int CurrentAmmo { get; set; }

        [Networked]
        private NetworkBool _isReloading { get; set; }

        private float _reloadTimer;
        private bool _reloadWasPlaying;

        [Header("Reload Shoot Animation")]
        public float ReloadShootAnimInterval = 0.4f;
        private float _reloadAnimTimer;

        [Header("Wings / Transformation")]
        public GameObject AngelWings;
        public GameObject DemonWings;
        public int TransformSoulCost = 15;
        public float TransformDuration = 20f;
        public float AngelSpeedMultiplier = 1.4f;
        public float DemonSpeedMultiplier = 1.3f;
        public int DemonMaxSpecialCharges = 3;
        public int DemonSpecialShotDamage = 6;
        public int DemonBonusHealth = 15;

        [Networked]
        private NetworkBool _isTransformed { get; set; }

        [Networked]
        private NetworkBool _isAngelForm { get; set; }

        private float _transformTimer;
        private float _baseWalkSpeed;
        private int _demonSpecialCharges;

        [Header("Invisibility Settings")]
        public int InvisibilitySoulCost = 5;
        public Renderer[] InvisibilityRenderers;

        [Header("Heal Power (Q)")]
        public int HealSoulCost = 2;

        [Header("Cage Power (C)")]
        public NetworkObject CagePrefab;
        public float CageDuration = 30f;
        public int CageSoulCost = 10;

        public bool IsTransformed => _isTransformed;
        public bool IsAngelForm => _isTransformed && _isAngelForm;
        public bool IsDemonForm => _isTransformed && !_isAngelForm;
        public bool IsInvisible => _isInvisible;
        public int DemonSpecialCharges => _demonSpecialCharges;

        [Networked]
        private NetworkBool _isInvisible { get; set; }

        [Networked, HideInInspector, Capacity(24), OnChangedRender(nameof(OnNicknameChanged))]
        public string Nickname { get; set; }

        [Networked, HideInInspector]
        public int ChickenKills { get; set; }

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

        [Networked]
        private byte _lastShotType { get; set; }

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
                CurrentAmmo = MaxAmmo;
                _isInvisible = false;
                _isTransformed = false;
                _isAngelForm = false;
                _demonSpecialCharges = 0;
                _isReloading = false;
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

            if (AngelWings != null) AngelWings.SetActive(false);
            if (DemonWings != null) DemonWings.SetActive(false);

            StopTransformLoops();
            StopReloadSound();
            UpdateInvisibilityVisual();
            UpdateTransformationVisual();
        }

        public override void FixedUpdateNetwork()
        {
            if (KCC.Position.y < -15f && !_isTransformed)
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

                StopReloadSound();
                StopTransformLoops();
                CancelInvisibilityIfActive();

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

            if (_specialShotCooldownTimer > 0f)
            {
                _specialShotCooldownTimer -= Runner.DeltaTime;
                if (_specialShotCooldownTimer < 0f)
                    _specialShotCooldownTimer = 0f;
            }

            if (HasStateAuthority && _isReloading)
            {
                _reloadTimer -= Runner.DeltaTime;
                if (_reloadTimer <= 0f)
                {
                    _isReloading = false;
                    CurrentAmmo = MaxAmmo;
                }
            }

            if (HasStateAuthority && _isTransformed)
            {
                _transformTimer -= Runner.DeltaTime;
                if (_transformTimer <= 0f)
                {
                    EndTransformation();
                }
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

            Hitbox.enabled = Health.IsAlive && !(_isTransformed && _isAngelForm);

            UpdateInvisibilityVisual();
            UpdateTransformationVisual();
            UpdateReloadSoundVisual();
            UpdateReloadShootAnimation();
        }

        private void Awake()
        {
            AssignAnimationIDs();
            _baseWalkSpeed = WalkSpeed;
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
                if (Camera.main != null)
                {
                    Camera.main.transform.SetPositionAndRotation(CameraHandle.position, CameraHandle.rotation);
                }
            }
        }

        private void ProcessInput(GameplayInput input)
        {
            KCC.SetLookRotation(input.LookRotation, -90f, 90f);

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

            bool canJump = KCC.IsGrounded || _isTransformed;

            if (canJump && input.Jump)
            {
                jumpImpulse = JumpImpulse;
                _isJumping = true;
            }

            KCC.Move(_moveVelocity, jumpImpulse);

            var pitchRotation = KCC.GetLookRotation(true, false);
            CameraPivot.localRotation = Quaternion.Euler(pitchRotation);

            if (input.Transform)
            {
                TryStartTransformation();
            }

            if (input.Invisibility)
            {
                TryStartInvisibility();
            }

            if (input.Heal)
            {
                TryHealWithSouls();
            }

            if (input.Reload)
            {
                TryStartReload();
            }

            if (input.Fire)
            {
                if (!_isTransformed || !_isAngelForm)
                {
                    if (!_isReloading)
                    {
                        FireGun();
                    }
                }
            }

            if (input.AltFire)
            {
                FireGrapple();
            }

            if (input.SpecialFire)
            {
                FireSoulShot();
            }

            if (input.Cage)
            {
                FireCagePower();
            }
        }

        private void TryStartInvisibility()
        {
            if (!HasStateAuthority)
                return;

            if (_isInvisible)
                return;

            if (CarriedSouls < InvisibilitySoulCost)
                return;

            SpendSouls(InvisibilitySoulCost);
            _isInvisible = true;

            if (InvisibilitySound != null && InvisibilityClip != null)
            {
                InvisibilitySound.PlayOneShot(InvisibilityClip);
            }
        }

        private void CancelInvisibilityIfActive()
        {
            if (!HasStateAuthority)
                return;

            if (!_isInvisible)
                return;

            _isInvisible = false;
        }

        private void UpdateInvisibilityVisual()
        {
            if (InvisibilityRenderers == null || InvisibilityRenderers.Length == 0)
            {
                InvisibilityRenderers = GetComponentsInChildren<Renderer>(true);
            }

            bool hideForThisClient = _isInvisible && !HasStateAuthority;

            if (InvisibilityRenderers != null)
            {
                for (int i = 0; i < InvisibilityRenderers.Length; i++)
                {
                    var r = InvisibilityRenderers[i];
                    if (r == null) continue;
                    r.enabled = !hideForThisClient;
                }
            }

            if (Nameplate != null && Nameplate.gameObject != null)
            {
                Nameplate.gameObject.SetActive(!hideForThisClient);
            }
        }

        private void TryHealWithSouls()
        {
            if (!HasStateAuthority)
                return;

            if (!Health.IsAlive)
                return;

            if (HealSoulCost <= 0)
                return;

            if (CarriedSouls < HealSoulCost)
                return;

            if (Health.CurrentHealth >= Health.InitialHealth)
                return;

            CancelInvisibilityIfActive();

            SpendSouls(HealSoulCost);
            Health.TakeHit(-1);

            if (HealSound != null && HealClip != null)
            {
                HealSound.PlayOneShot(HealClip);
            }
        }

        private void TryStartReload()
        {
            if (!HasStateAuthority)
                return;

            if (_isTransformed && !_isAngelForm)
                return;

            if (_isReloading)
                return;

            if (CurrentAmmo >= MaxAmmo)
                return;

            CancelInvisibilityIfActive();

            _isReloading = true;
            _reloadTimer = ReloadDuration;
            _reloadAnimTimer = 0f;
        }

        private void UpdateReloadShootAnimation()
        {
            if (!_isReloading || Animator == null)
                return;

            _reloadAnimTimer -= Time.deltaTime;
            if (_reloadAnimTimer <= 0f)
            {
                Animator.SetTrigger(_animIDShoot);
                _reloadAnimTimer = ReloadShootAnimInterval;
            }
        }

        private void UpdateReloadSoundVisual()
        {
            if (ReloadSound == null)
                return;

            bool shouldPlay = _isReloading;

            if (shouldPlay && !_reloadWasPlaying)
            {
                if (ReloadClip != null)
                {
                    ReloadSound.clip = ReloadClip;
                }
                ReloadSound.loop = true;
                ReloadSound.Play();
            }
            else if (!shouldPlay && _reloadWasPlaying)
            {
                ReloadSound.loop = false;
                ReloadSound.Stop();
            }

            _reloadWasPlaying = shouldPlay;
        }

        private void StopReloadSound()
        {
            if (ReloadSound != null)
            {
                ReloadSound.loop = false;
                ReloadSound.Stop();
            }
            _reloadWasPlaying = false;
        }

        private void TryStartTransformation()
        {
            if (!HasStateAuthority)
                return;

            if (_isTransformed)
                return;

            int spentPure;
            int spentCorrupt;

            if (!TrySpendSoulsForTransform(TransformSoulCost, out spentPure, out spentCorrupt))
                return;

            CancelInvisibilityIfActive();

            bool angel = spentPure >= spentCorrupt;

            _isTransformed = true;
            _isAngelForm = angel;
            _transformTimer = TransformDuration;

            _baseWalkSpeed = WalkSpeed;

            if (angel)
            {
                WalkSpeed = _baseWalkSpeed * AngelSpeedMultiplier;
                _demonSpecialCharges = 0;
            }
            else
            {
                WalkSpeed = _baseWalkSpeed * DemonSpeedMultiplier;
                _demonSpecialCharges = DemonMaxSpecialCharges;

                if (Health != null && DemonBonusHealth > 0)
                {
                    Health.TakeHit(-DemonBonusHealth);
                }
            }
        }

        private void EndTransformation()
        {
            bool wasDemon = _isTransformed && !_isAngelForm;

            _isTransformed = false;
            _isAngelForm = false;
            _transformTimer = 0f;

            WalkSpeed = _baseWalkSpeed;
            _demonSpecialCharges = 0;

            if (wasDemon && Health != null)
            {
                if (Health.CurrentHealth > Health.InitialHealth)
                {
                    int extra = Health.CurrentHealth - Health.InitialHealth;
                    if (extra > 0)
                    {
                        Health.TakeHit(extra, true);
                    }
                }
            }

            StopTransformLoops();
        }

        private void StopTransformLoops()
        {
            if (AngelSound != null)
            {
                AngelSound.loop = false;
                AngelSound.Stop();
            }

            if (DemonSound != null)
            {
                DemonSound.loop = false;
                DemonSound.Stop();
            }
        }

        private void UpdateTransformationVisual()
        {
            bool angelActive = _isTransformed && _isAngelForm;
            bool demonActive = _isTransformed && !_isAngelForm;

            if (AngelWings != null && AngelWings.activeSelf != angelActive)
                AngelWings.SetActive(angelActive);

            if (DemonWings != null && DemonWings.activeSelf != demonActive)
                DemonWings.SetActive(demonActive);

            if (AngelSound != null)
            {
                if (angelActive)
                {
                    if (!AngelSound.isPlaying)
                    {
                        if (AngelTransformLoopClip != null && AngelSound.clip != AngelTransformLoopClip)
                        {
                            AngelSound.clip = AngelTransformLoopClip;
                        }
                        AngelSound.loop = true;
                        AngelSound.Play();
                    }
                }
                else if (AngelSound.isPlaying)
                {
                    AngelSound.loop = false;
                    AngelSound.Stop();
                }
            }

            if (DemonSound != null)
            {
                if (demonActive)
                {
                    if (!DemonSound.isPlaying)
                    {
                        if (DemonTransformLoopClip != null && DemonSound.clip != DemonTransformLoopClip)
                        {
                            DemonSound.clip = DemonTransformLoopClip;
                        }
                        DemonSound.loop = true;
                        DemonSound.Play();
                    }
                }
                else if (DemonSound.isPlaying)
                {
                    DemonSound.loop = false;
                    DemonSound.Stop();
                }
            }
        }

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

        public void StartGrapple(Vector3 target)
        {
            if (!Health.IsAlive)
                return;

            _grappleTarget = target;
            _isGrappling = true;
        }

        public void OnGrappleProjectileFinished(bool didHit)
        {
            _activeGrappleProjectile = null;

            if (!didHit)
            {
                _isGrappling = false;
            }
        }

        private void FireGun()
        {
            if (!HasStateAuthority)
                return;

            if (_isReloading)
                return;

            if (_isTransformed && _isAngelForm)
                return;

            bool isDemon = _isTransformed && !_isAngelForm;

            if (!isDemon)
            {
                if (CurrentAmmo <= 0)
                    return;

                CurrentAmmo = Mathf.Max(0, CurrentAmmo - 1);
            }

            CancelInvisibilityIfActive();

            _hitPosition = Vector3.zero;
            _hitNormal = Vector3.zero;

            Vector3 origin = CameraHandle.position + CameraHandle.forward * 0.1f;
            Vector3 direction = CameraHandle.forward;
            float maxDistance = 200f;

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                maxDistance,
                HitMask,
                QueryTriggerInteraction.Ignore
            );

            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                for (int i = 0; i < hits.Length; i++)
                {
                    RaycastHit hit = hits[i];

                    if (hit.collider == null)
                        continue;

                    if (hit.collider.transform.IsChildOf(transform))
                        continue;

                    _hitPosition = hit.point;
                    _hitNormal = hit.normal;

                    var health = hit.collider.GetComponentInParent<Health>();
                    if (health != null && health != Health)
                    {
                        var targetPlayer = health.GetComponent<Player>();

                        if (targetPlayer != null && targetPlayer.IsAngelForm)
                        {
                            break;
                        }

                        int damage = 1;
                        health.Killed = OnEnemyKilled;
                        health.TakeHit(damage, true);
                    }

                    break;
                }
            }

            _lastShotType = SHOT_NORMAL;
            _fireCount++;
        }

        private void FireGrapple()
        {
            if (!HasStateAuthority)
                return;

            if (GrappleProjectilePrefab == null)
                return;

            if (_activeGrappleProjectile != null || _isGrappling)
                return;

            CancelInvisibilityIfActive();

            Transform muzzle = GrappleMuzzle != null ? GrappleMuzzle : CameraHandle;
            Vector3 spawnPos = muzzle.position;
            Quaternion spawnRot = Quaternion.Euler(90, 90, -90);

            var projGO = Instantiate(GrappleProjectilePrefab, spawnPos, spawnRot);
            var proj = projGO.GetComponent<GrappleProjectile>();
            if (proj != null)
            {
                _activeGrappleProjectile = proj;

                proj.Init(
                    this,
                    muzzle,
                    CameraHandle.forward,
                    GrappleProjectileSpeed,
                    GrappleMaxDistance,
                    GrappleHitMask
                );
            }
        }

        private void FireSoulShot()
        {
            if (!HasStateAuthority)
                return;

            if (_isReloading)
                return;

            if (_isTransformed && _isAngelForm)
                return;

            bool demonFreeShot = _isTransformed && !_isAngelForm && _demonSpecialCharges > 0;

            if (_specialShotCooldownTimer > 0f)
                return;

            if (!demonFreeShot)
            {
                if (CarriedSouls < SpecialShotSoulCost)
                    return;

                SpendSouls(SpecialShotSoulCost);
            }
            else
            {
                _demonSpecialCharges = Mathf.Max(0, _demonSpecialCharges - 1);
            }

            CancelInvisibilityIfActive();

            _specialShotCooldownTimer = SpecialShotCooldown;

            _hitPosition = Vector3.zero;
            _hitNormal = Vector3.zero;

            Vector3 origin = CameraHandle.position + CameraHandle.forward * 0.1f;
            Vector3 direction = CameraHandle.forward;
            float maxDistance = SpecialShotRange;

            LayerMask mask = SpecialShotHitMask.value != 0 ? SpecialShotHitMask : HitMask;

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                maxDistance,
                mask,
                QueryTriggerInteraction.Ignore
            );

            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                for (int i = 0; i < hits.Length; i++)
                {
                    RaycastHit hit = hits[i];

                    if (hit.collider == null)
                        continue;

                    if (hit.collider.transform.IsChildOf(transform))
                        continue;

                    _hitPosition = hit.point;
                    _hitNormal = hit.normal;

                    var health = hit.collider.GetComponentInParent<Health>();
                    if (health != null && health != Health)
                    {
                        var targetPlayer = health.GetComponent<Player>();

                        if (targetPlayer != null && targetPlayer.IsAngelForm)
                        {
                            break;
                        }

                        int damage = SpecialShotDamage;

                        if (demonFreeShot)
                        {
                            damage = DemonSpecialShotDamage;
                        }

                        health.Killed = OnEnemyKilled;
                        health.TakeHit(damage, true);
                        break;
                    }

                    var destructible = hit.collider.GetComponentInParent<DestructiblePlatform>();
                    if (destructible != null)
                    {
                        destructible.DisableTemporarily(10f);
                        break;
                    }

                    break;
                }
            }

            _lastShotType = SHOT_SPECIAL;
            _fireCount++;
        }

        private void FireCagePower()
        {
            if (!HasStateAuthority)
                return;

            if (!Health.IsAlive)
                return;

            if (CagePrefab == null)
                return;

            if (CageSoulCost <= 0)
                return;

            if (CarriedSouls < CageSoulCost)
                return;

            Vector3 origin = CameraHandle.position + CameraHandle.forward * 0.1f;
            Vector3 direction = CameraHandle.forward;
            float maxDistance = 200f;

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                maxDistance,
                HitMask,
                QueryTriggerInteraction.Ignore
            );

            if (hits == null || hits.Length == 0)
                return;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Vector3 spawnPos = Vector3.zero;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null)
                    continue;

                if (hit.collider.transform.IsChildOf(transform))
                    continue;

                var health = hit.collider.GetComponentInParent<Health>();
                Player targetPlayer = health != null ? health.GetComponent<Player>() : null;

                if (targetPlayer != null)
                {
                    spawnPos = targetPlayer.KCC != null ? targetPlayer.KCC.Position : targetPlayer.transform.position;
                }
                else
                {
                    spawnPos = hit.point;
                }

                _hitPosition = hit.point;
                _hitNormal = hit.normal;

                found = true;
                break;
            }

            if (!found)
                return;

            SpendSouls(CageSoulCost);
            CancelInvisibilityIfActive();

            NetworkObject cageObj = Runner.Spawn(CagePrefab, spawnPos, Quaternion.identity, Object.InputAuthority);
            var cageTrap = cageObj.GetComponent<CageTrap>();
            if (cageTrap != null)
            {
                cageTrap.SetLifetime(CageDuration);
            }

            _lastShotType = SHOT_CAGE;
            _fireCount++;
        }

        private void SpendSouls(int amount)
        {
            if (amount <= 0)
                return;

            int fromPure = Mathf.Min(amount, CarriedPureSouls);
            CarriedPureSouls -= fromPure;
            amount -= fromPure;

            if (amount > 0)
            {
                int fromCorrupt = Mathf.Min(amount, CarriedCorruptSouls);
                CarriedCorruptSouls -= fromCorrupt;
                amount -= fromCorrupt;
            }

            CarriedSouls = Mathf.Max(0, CarriedPureSouls + CarriedCorruptSouls);

            UpdateTotalSouls(this);
        }

        private bool TrySpendSoulsForTransform(int amount, out int spentPure, out int spentCorrupt)
        {
            spentPure = 0;
            spentCorrupt = 0;

            if (amount <= 0)
                return false;

            if (CarriedSouls < amount)
                return false;

            int remaining = amount;

            int usePure = Mathf.Min(remaining, CarriedPureSouls);
            CarriedPureSouls -= usePure;
            spentPure = usePure;
            remaining -= usePure;

            if (remaining > 0)
            {
                int useCorrupt = Mathf.Min(remaining, CarriedCorruptSouls);
                CarriedCorruptSouls -= useCorrupt;
                spentCorrupt = useCorrupt;
                remaining -= useCorrupt;
            }

            CarriedSouls = Mathf.Max(0, CarriedPureSouls + CarriedCorruptSouls);

            UpdateTotalSouls(this);

            return remaining <= 0;
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

            _specialShotCooldownTimer = 0f;

            _isReloading = false;
            _reloadTimer = 0f;
            CurrentAmmo = MaxAmmo;
            StopReloadSound();

            _isTransformed = false;
            _isAngelForm = false;
            _demonSpecialCharges = 0;
            StopTransformLoops();

            _isInvisible = false;
            UpdateInvisibilityVisual();
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
                CancelInvisibilityIfActive();

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

                if (UIShooter.Instance != null && HasStateAuthority)
                {
                    string victimName = string.IsNullOrWhiteSpace(victimPlayer.Nickname)
                        ? "Jugador"
                        : victimPlayer.Nickname;

                    UIShooter.Instance.RegisterKill(victimName);
                }
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
                if (_lastShotType == SHOT_NORMAL)
                {
                    if (FireSound != null && FireGunClip != null)
                    {
                        FireSound.PlayOneShot(FireGunClip);
                    }
                }
                else if (_lastShotType == SHOT_SPECIAL)
                {
                    if (SpecialShotSound != null && SpecialShotClip != null)
                    {
                        SpecialShotSound.PlayOneShot(SpecialShotClip);
                    }
                }
                else if (_lastShotType == SHOT_CAGE)
                {
                    if (CageShotSound != null && CageShotClip != null)
                    {
                        CageShotSound.PlayOneShot(CageShotClip);
                    }
                }

                if (MuzzleParticle != null)
                    MuzzleParticle.Play();

                Animator.SetTrigger(_animIDShoot);

                if (_hitPosition != Vector3.zero && ImpactPrefab != null)
                {
                    var impact = Instantiate(ImpactPrefab, _hitPosition, Quaternion.LookRotation(_hitNormal));

                    if (_lastShotType == SHOT_SPECIAL && impact != null)
                    {
                        impact.transform.localScale *= 5f;
                    }
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
                ScalingRoot.localScale = _isJumping
                    ? new Vector3(0.5f, 1.5f, 0.5f)
                    : new Vector3(1.25f, 0.75f, 1.25f);
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
