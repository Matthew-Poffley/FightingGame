using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FightingGame;

public class Stickman
{
    private const float LineThickness = 4f;
    private const float MoveSpeed = 250f;

    private const float HeadRadius = 13f;
    private const float BodyLength = 32f;
    private const float ArmLength = 17f;
    private const float LegLength = 18f;
    private const float BodyHalfWidth = 17f;
    private const float GunLength = 16f;

    private const float JumpSpeed = 1050f;
    private const float Gravity = 1600f;
    private const float MaxGroundSnapDistance = 24f; // per-frame slope-following tolerance; see Update()
    private const float BlockMomentumFriction = 3f; // decay rate for horizontal momentum carried into a block

    private const float GroundPoundFallSpeed = 2200f; // held constant while pounding, well above normal terminal fall speed
    private const float GroundPoundBaseRadius = 70f;
    private const float GroundPoundBaseDamage = 20f;

    private const float AoETickInterval = 0.5f; // how often the aura deals damage to nearby enemies
    private const float AoEDamagePerRadiusRatio = 0.12f; // baseline damage/tick contributed by radius alone, before any AoEAuraDamage stacks
    private const float AoEGlowPulseSpeed = 6f; // radians/sec for the aura's glow pulse

    // How long a ground-pound landing stuns the stomper themself (a brief recovery window - risk
    // for using the ability) - the duration for victims caught in the blast is public on Game1's
    // TriggerGroundPoundImpact instead, since that's applied through ApplyHit's stunDuration param.
    private const float GroundPoundSelfStunDuration = 0.7f;
    private const float StunOrbitSpeed = 10f; // radians/sec for the dizzy-stars visual

    private const float CrouchScale = 0.55f;
    private const float StanceSplay = 0.35f; // radians, legs/arms apart when standing
    private const float CrouchSplay = 0.55f;
    private const float WalkSwingAngle = 0.9f; // radians, max swing during a stride
    private const float WalkCycleSpeed = 8f; // stride rate while moving

    private const float MaxHealth = 100f;
    private const float GunDamage = 12f;
    private const float BulletPushback = 14f;
    private const float BlockPushback = 6f;
    private const float FireCooldownDuration = 0.35f;
    private const float RecoilDuration = 0.12f;
    private const int MagazineSize = 6;
    private const float ReloadDuration = 1.2f;
    private const float MaxBlockStamina = 2f;
    private const float BlockRechargeDuration = 3f;

    // How long a poison hit keeps draining health - kept in sync with the "3s" in Upgrades.GetDescription.
    private const float PoisonDuration = 3f;
    private const float PoisonBleedInterval = 0.5f; // how often a poisoned stickman drips blood
    private const float PoisonFlashSpeed = 9f; // radians/sec for the poisoned skin-tint pulse

    private const float DeathAngularAcceleration = 6f;
    private static readonly float MaxDeathAngle = MathHelper.PiOver2;

    // Smooth-cartoon-cow proportions: a bigger rounder head, wide forward muzzle, short stub horns,
    // and chunkier stubby limbs/hooves - reads as a soft mascot rather than a stick figure with a
    // cow head, while still built entirely from Primitives2D shapes (no sprite pipeline exists here).
    private const float HeadSize = HeadRadius * 2.3f;
    private const float EarSize = HeadRadius * 1f;
    private const float HornLength = 4f;
    private const float HornThickness = 6f;
    private const float SnoutWidth = HeadRadius * 1.3f;
    private const float SnoutHeight = HeadRadius * 1.05f;
    private const float BodyWidth = BodyHalfWidth * 2.6f;
    private const float LimbThickness = 14f;
    private const float HoofSize = 13f;
    private const float SpotSize = 10f;
    private const float LimbSpotSize = 8f;
    private const float TailLength = 14f;
    private const float TailThickness = 7f;

    private static readonly Color CowBodyColor = new Color(250, 248, 240);
    private static readonly Color CowSnoutColor = new Color(235, 175, 185);
    private static readonly Color CowDarkColor = new Color(25, 25, 25);
    private static readonly Color CowHornColor = new Color(200, 190, 165);

    private float _verticalVelocity;
    private bool _isGrounded = true;
    private bool _canJump = true;
    private float _walkCycleTime;
    private bool _isCrouching;
    private bool _isBlocking;
    private float _fireCooldown;
    private float _recoilTimer;
    private int _ammoInMagazine = MagazineSize;
    private bool _isReloading;
    private float _reloadElapsed;
    private float _blockStamina = MaxBlockStamina;
    private bool _blockExhausted;

    private bool _isGroundPounding;
    private bool _crouchWasHeld; // previous frame's CrouchHeld, for edge-detecting the ground-pound trigger
    private float _stunTimer;

    private bool _isDead;
    private float _fallSign;
    private float _deathAngle;
    private float _deathAngularVelocity;

    // Active poison status from the most recent poisoned hit - overwritten (not stacked) by the next one.
    private float _poisonRemainingDuration;
    private float _poisonDamagePerSecond;
    private bool _poisonAttackerOnRight;
    private float _poisonBleedTimer;
    private float _poisonFlashTimer;

    // Persistent upgrade bonuses picked between rounds - never touched by ResetForNewRound, so they stack for the whole match.
    private int _bonusMagazineSize;
    private float _fireCooldownMultiplier = 1f;
    private float _reloadDurationMultiplier = 1f;
    private float _moveSpeedMultiplier = 1f;
    private float _bonusMoveSpeed;
    private float _jumpSpeedMultiplier = 1f;
    private float _bonusMaxHealth;
    private float _maxHealthMultiplier = 1f;
    private float _bonusDamage;
    private float _damageMultiplier = 1f;
    private float _bonusReloadReduction;
    private float _bonusBlockStamina;
    private float _blockStaminaMultiplier = 1f;
    private float _bulletSpeedMultiplier = 1f;
    private float _bulletRadiusMultiplier = 1f;
    private float _bulletGravityMultiplier = 1f;
    private int _bonusBulletCount;
    private float _magazineSizeMultiplier = 1f;
    private int _bulletCountReduction;
    private float _bonusPoisonDamagePerSecond;
    private float _bonusHomingTurnRate;
    private int _bonusBounceCount;
    private float _bonusExplosionRadius;
    private float _knockbackMultiplier = 1f;
    private float _bonusHealthRegenPerSecond;
    private int _bonusExtraLives;
    private int _bonusExtraJumps;
    private int _bonusCardChoices;
    private float _bonusLifeStealPercent;
    private float _lifeStealMultiplier = 1f;
    private float _bonusGroundPoundRadius;
    private float _groundPoundRadiusMultiplier = 1f;
    private float _groundPoundDamageMultiplier = 1f;
    private float _bonusAoERadius;
    private float _aoeRadiusMultiplier = 1f;
    private float _bonusAoEDamagePerTick;
    private float _aoeDamageMultiplier = 1f;

    private float _aoeTickTimer;
    private float _aoeGlowTimer; // free-running phase clock for the aura's glow pulse - never resets mid-round

    // How many extra lives/air jumps are still unspent this round - reset from the persistent
    // _bonus* fields at the start of each round (see ResetForNewRound).
    private int _remainingExtraLives;
    private int _remainingExtraJumps;

    // Horizontal speed carried into the most recent unlocked move - kept moving (and decaying)
    // while blocking instead of snapping to a dead stop. See BlockMomentumFriction.
    private float _lastMoveVelocityX;

    public bool IsGrounded => _isGrounded;
    public bool FacingRight { get; private set; } = true;
    public Vector2 AimDirection { get; private set; } = Vector2.UnitX;
    public float Health { get; private set; } = MaxHealth;
    public float HealthFraction => Health / EffectiveMaxHealth;
    public bool IsAlive => Health > 0f;

    public int EffectiveMagazineSize => Math.Max(1, (int)((MagazineSize + _bonusMagazineSize) * _magazineSizeMultiplier));
    public float EffectiveFireCooldown => FireCooldownDuration * _fireCooldownMultiplier;
    public float EffectiveReloadDuration => MathHelper.Max(ReloadDuration - _bonusReloadReduction, 0.1f) * _reloadDurationMultiplier;
    public float EffectiveMoveSpeed => MoveSpeed * _moveSpeedMultiplier + _bonusMoveSpeed;
    public float EffectiveJumpSpeed => JumpSpeed * _jumpSpeedMultiplier;
    public float EffectiveMaxHealth => (MaxHealth + _bonusMaxHealth) * _maxHealthMultiplier;
    public float EffectiveDamage => (GunDamage + _bonusDamage) * _damageMultiplier;
    public float EffectiveMaxBlockStamina => (MaxBlockStamina + _bonusBlockStamina) * _blockStaminaMultiplier;
    public float BulletSpeedMultiplier => _bulletSpeedMultiplier;
    public float BulletRadiusMultiplier => _bulletRadiusMultiplier;
    public float BulletGravityMultiplier => _bulletGravityMultiplier;
    public int EffectiveBulletCount => Math.Max(1, 1 + _bonusBulletCount - _bulletCountReduction);
    public float EffectivePoisonDamagePerSecond => _bonusPoisonDamagePerSecond;
    public float EffectiveHomingTurnRate => _bonusHomingTurnRate;
    public int EffectiveBounceCount => _bonusBounceCount;
    public float EffectiveExplosionRadius => _bonusExplosionRadius;
    public float EffectiveKnockbackMultiplier => _knockbackMultiplier;
    public float EffectiveHealthRegenPerSecond => _bonusHealthRegenPerSecond;
    public int EffectiveExtraLives => _bonusExtraLives;
    public int RemainingExtraLives => _remainingExtraLives;
    public int EffectiveExtraJumps => _bonusExtraJumps;
    public int RemainingExtraJumps => _remainingExtraJumps;
    public int EffectiveBonusCardChoices => _bonusCardChoices;
    public float EffectiveLifeStealFraction => (_bonusLifeStealPercent * _lifeStealMultiplier) / 100f;
    public float EffectiveGroundPoundRadius => (GroundPoundBaseRadius + _bonusGroundPoundRadius) * _groundPoundRadiusMultiplier;
    public float EffectiveGroundPoundDamage => GroundPoundBaseDamage * _groundPoundDamageMultiplier;
    public bool IsGroundPounding => _isGroundPounding;
    public bool IsStunned => _stunTimer > 0f;
    public float EffectiveAoERadius => _bonusAoERadius * _aoeRadiusMultiplier;
    public float EffectiveAoEDamagePerTick => (EffectiveAoERadius * AoEDamagePerRadiusRatio + _bonusAoEDamagePerTick) * _aoeDamageMultiplier;
    public float EffectiveAoEDamagePerSecond => EffectiveAoEDamagePerTick / AoETickInterval;
    public bool IsPoisoned => _poisonRemainingDuration > 0f;
    public float PoisonFraction => _poisonRemainingDuration / PoisonDuration;

    public void ApplyUpgrade(Card card)
    {
        switch (card.Type)
        {
            case UpgradeType.ExtraMagazine: _bonusMagazineSize += (int)card.Amount; break;
            case UpgradeType.RapidFire: _fireCooldownMultiplier *= 1f - card.Amount / 100f; break;
            case UpgradeType.BiggerBullets: _bulletRadiusMultiplier *= 1f + card.Amount / 100f; break;
            case UpgradeType.FasterBullets: _bulletSpeedMultiplier *= 1f + card.Amount / 100f; break;
            case UpgradeType.SwiftBoots: _moveSpeedMultiplier *= 1f + card.Amount / 100f; break;
            case UpgradeType.TrackSpikes: _bonusMoveSpeed += card.Amount; break;
            case UpgradeType.HigherJumps: _jumpSpeedMultiplier *= 1f + card.Amount / 100f; break;
            case UpgradeType.ThickSkin: _bonusMaxHealth += card.Amount; break;
            case UpgradeType.VitalSurge: _maxHealthMultiplier *= 1f + card.Amount / 100f; break;
            case UpgradeType.HollowPoints: _bonusDamage += card.Amount; break;
            case UpgradeType.HeavyCaliber: _damageMultiplier *= 1f + card.Amount / 100f; break;
            case UpgradeType.QuickHands: _reloadDurationMultiplier *= 1f - card.Amount / 100f; break;
            case UpgradeType.SpeedLoader: _bonusReloadReduction += card.Amount; break;
            case UpgradeType.IronGuard: _bonusBlockStamina += card.Amount; break;
            case UpgradeType.GuardTraining: _blockStaminaMultiplier *= 1f + card.Amount / 100f; break;
            case UpgradeType.Buckshot: _bonusBulletCount += (int)card.Amount; break;
            case UpgradeType.ToxicRounds: _bonusPoisonDamagePerSecond += card.Amount; break;
            case UpgradeType.HomingRounds: _bonusHomingTurnRate += MathHelper.ToRadians(card.Amount); break;
            case UpgradeType.RicochetRounds: _bonusBounceCount += (int)card.Amount; break;
            case UpgradeType.ExplosiveRounds: _bonusExplosionRadius += card.Amount; break;
            case UpgradeType.KnockbackForce: _knockbackMultiplier *= 1f + card.Amount / 100f; break;
            case UpgradeType.CurseWeakness: _damageMultiplier *= 1f - card.Amount / 100f; break;
            case UpgradeType.CurseSlowness: _moveSpeedMultiplier *= 1f - card.Amount / 100f; break;
            case UpgradeType.CurseFragile: _maxHealthMultiplier *= 1f - card.Amount / 100f; break;
            case UpgradeType.CurseShrunkenRounds: _bulletRadiusMultiplier *= 1f - card.Amount / 100f; break;
            case UpgradeType.CurseSlowRounds: _bulletSpeedMultiplier *= 1f - card.Amount / 100f; break;
            case UpgradeType.CurseEmptyMag: _magazineSizeMultiplier *= 1f - card.Amount / 100f; break;
            case UpgradeType.CurseJammedGun: _bulletCountReduction += (int)card.Amount; break;
            case UpgradeType.HealthRegen: _bonusHealthRegenPerSecond += card.Amount; break;
            case UpgradeType.ExtraLife: _bonusExtraLives += (int)card.Amount; break;
            case UpgradeType.ExtraJump: _bonusExtraJumps += (int)card.Amount; break;
            case UpgradeType.KeenEye: _bonusCardChoices += (int)card.Amount; break;
            case UpgradeType.GroundPoundRadius: _bonusGroundPoundRadius += card.Amount; break;
            case UpgradeType.GroundPoundPower: _groundPoundDamageMultiplier *= 1f + card.Amount / 100f; break;
            case UpgradeType.CurseGroundPoundRadius: _groundPoundRadiusMultiplier *= 1f - card.Amount / 100f; break;
            case UpgradeType.CurseGroundPoundPower: _groundPoundDamageMultiplier *= 1f - card.Amount / 100f; break;
            case UpgradeType.AoEAura: _bonusAoERadius += card.Amount; break;
            case UpgradeType.AoEAuraSize: _bonusAoERadius += card.Amount; break;
            case UpgradeType.AoEAuraDamage: _bonusAoEDamagePerTick += card.Amount; break;
            case UpgradeType.CurseAoERadius: _aoeRadiusMultiplier *= 1f - card.Amount / 100f; break;
            case UpgradeType.CurseAoEDamage: _aoeDamageMultiplier *= 1f - card.Amount / 100f; break;
            case UpgradeType.FlatTrajectory: _bulletGravityMultiplier *= 1f - card.Amount / 100f; break;
            case UpgradeType.CurseHeavyRounds: _bulletGravityMultiplier *= 1f + card.Amount / 100f; break;
            case UpgradeType.LifeSteal: _bonusLifeStealPercent += card.Amount; break;
            case UpgradeType.CurseLifeSteal: _lifeStealMultiplier *= 1f - card.Amount / 100f; break;
        }
    }

    // A Mega Curse wipes one stat back to its untouched starting value - the inverse of
    // ApplyUpgrade, for whichever type Upgrades.RollMegaCurse targeted. Only ever called with a
    // non-curse type (see Upgrades.ResettableTypes) - curses aren't something to "reset" here.
    public void ResetUpgrade(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.ExtraMagazine: _bonusMagazineSize = 0; break;
            case UpgradeType.RapidFire: _fireCooldownMultiplier = 1f; break;
            case UpgradeType.BiggerBullets: _bulletRadiusMultiplier = 1f; break;
            case UpgradeType.FasterBullets: _bulletSpeedMultiplier = 1f; break;
            case UpgradeType.SwiftBoots: _moveSpeedMultiplier = 1f; break;
            case UpgradeType.TrackSpikes: _bonusMoveSpeed = 0f; break;
            case UpgradeType.HigherJumps: _jumpSpeedMultiplier = 1f; break;
            case UpgradeType.ThickSkin: _bonusMaxHealth = 0f; break;
            case UpgradeType.VitalSurge: _maxHealthMultiplier = 1f; break;
            case UpgradeType.HollowPoints: _bonusDamage = 0f; break;
            case UpgradeType.HeavyCaliber: _damageMultiplier = 1f; break;
            case UpgradeType.QuickHands: _reloadDurationMultiplier = 1f; break;
            case UpgradeType.SpeedLoader: _bonusReloadReduction = 0f; break;
            case UpgradeType.IronGuard: _bonusBlockStamina = 0f; break;
            case UpgradeType.GuardTraining: _blockStaminaMultiplier = 1f; break;
            case UpgradeType.Buckshot: _bonusBulletCount = 0; break;
            case UpgradeType.ToxicRounds: _bonusPoisonDamagePerSecond = 0f; break;
            case UpgradeType.HomingRounds: _bonusHomingTurnRate = 0f; break;
            case UpgradeType.RicochetRounds: _bonusBounceCount = 0; break;
            case UpgradeType.ExplosiveRounds: _bonusExplosionRadius = 0f; break;
            case UpgradeType.KnockbackForce: _knockbackMultiplier = 1f; break;
            case UpgradeType.HealthRegen: _bonusHealthRegenPerSecond = 0f; break;
            case UpgradeType.ExtraLife: _bonusExtraLives = 0; _remainingExtraLives = 0; break;
            case UpgradeType.ExtraJump: _bonusExtraJumps = 0; _remainingExtraJumps = 0; break;
            case UpgradeType.KeenEye: _bonusCardChoices = 0; break;
            case UpgradeType.GroundPoundRadius: _bonusGroundPoundRadius = 0f; break;
            case UpgradeType.GroundPoundPower: _groundPoundDamageMultiplier = 1f; break;
            case UpgradeType.AoEAura: _bonusAoERadius = 0f; break;
            case UpgradeType.AoEAuraSize: _bonusAoERadius = 0f; break;
            case UpgradeType.AoEAuraDamage: _bonusAoEDamagePerTick = 0f; break;
            case UpgradeType.FlatTrajectory: _bulletGravityMultiplier = 1f; break;
            case UpgradeType.LifeSteal: _bonusLifeStealPercent = 0f; break;
        }
    }

    // Set for the one frame something notable happens, so the caller (Game1) can trigger a sound effect.
    public bool FiredThisFrame { get; private set; }
    public bool JumpedThisFrame { get; private set; }
    public bool LandedThisFrame { get; private set; }
    public bool ReloadStartedThisFrame { get; private set; }
    public bool DiedThisFrame { get; private set; }
    public bool PoisonBleedThisFrame { get; private set; }
    public bool GroundPoundLandedThisFrame { get; private set; }
    public bool AoETickThisFrame { get; private set; }
    public Vector2 MuzzlePosition { get; private set; }

    // Feet position (bottom-center of the stickman).
    public Vector2 Position;

    public void Update(GameTime gameTime, PlayerInput input, float minX, float maxX, Level level)
    {
        float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
        FiredThisFrame = false;
        JumpedThisFrame = false;
        LandedThisFrame = false;
        ReloadStartedThisFrame = false;
        DiedThisFrame = false;
        PoisonBleedThisFrame = false;
        GroundPoundLandedThisFrame = false;
        AoETickThisFrame = false;

        if (_isDead)
        {
            UpdateDeath(delta, level);
            return;
        }

        if (_poisonRemainingDuration > 0f)
        {
            UpdatePoison(delta);
            if (_isDead)
                return;
        }

        if (_bonusHealthRegenPerSecond > 0f)
            Health = MathHelper.Min(EffectiveMaxHealth, Health + _bonusHealthRegenPerSecond * delta);

        if (EffectiveAoERadius > 0f)
        {
            _aoeGlowTimer += delta;
            _aoeTickTimer += delta;
            if (_aoeTickTimer >= AoETickInterval)
            {
                _aoeTickTimer -= AoETickInterval;
                AoETickThisFrame = true;
            }
        }
        else
        {
            _aoeTickTimer = 0f;
        }

        if (_stunTimer > 0f)
            _stunTimer = MathF.Max(0f, _stunTimer - delta);

        // Riding a moving platform - carry along whatever it moved this frame before applying this
        // frame's own input-driven movement below.
        if (_isGrounded)
            Position.X += level.GetCarryDeltaX(Position.X, Position.Y);

        if (input.AimDirection.HasValue)
        {
            AimDirection = input.AimDirection.Value;
            if (MathF.Abs(AimDirection.X) > 0.15f)
                FacingRight = AimDirection.X > 0f;
        }

        bool wasGrounded = _isGrounded;
        _isCrouching = wasGrounded && input.CrouchHeld;

        // Crouching while airborne (rather than the ground-only crouch above) slams straight down -
        // see the landing branch below for the impact itself (Game1 reads GroundPoundLandedThisFrame).
        // Edge-triggered on the crouch press itself (not just "currently held") - otherwise walking
        // or falling off a ledge while already holding crouch would instantly ground-pound and lock
        // out jumping (including Cloven Hooves' air jump) via actionLocked below.
        bool crouchPressed = input.CrouchHeld && !_crouchWasHeld;
        _crouchWasHeld = input.CrouchHeld;
        if (!wasGrounded && !_isGroundPounding && crouchPressed)
            _isGroundPounding = true;

        // Blocking works whether grounded or airborne (crouch-blocking doesn't, since crouching
        // itself is ground-only above) - lets a jumping player still guard against an incoming shot.
        // Stunned rules it out too - a stunned cow can't raise a guard.
        bool wantsToBlock = !_isCrouching && !_isGroundPounding && !IsStunned && input.BlockHeld && !_blockExhausted;
        _isBlocking = wantsToBlock && _blockStamina > 0f;
        UpdateBlockStamina(delta);

        UpdateWeapon(input, delta, level);

        bool actionLocked = _isCrouching || _isBlocking || _isGroundPounding || IsStunned;
        float moveDirection = actionLocked ? 0f : input.MoveDirection;

        if (_isBlocking)
        {
            // Blocking carries whatever horizontal momentum you had going into it, decaying over
            // time, instead of slamming to a dead stop - only crouching fully freezes movement.
            Position.X += _lastMoveVelocityX * delta;
            _lastMoveVelocityX *= MathF.Max(0f, 1f - BlockMomentumFriction * delta);
        }
        else
        {
            Position.X += moveDirection * EffectiveMoveSpeed * delta;
            _lastMoveVelocityX = moveDirection * EffectiveMoveSpeed;
        }

        Position.X = MathHelper.Clamp(Position.X, minX, maxX);

        var (_, _, totalHeight) = GetBodyMetrics();
        bool touchedWall = level.ResolveWallCollision(ref Position, BodyHalfWidth, totalHeight);

        // Touching a wall refreshes the jump, same as standing on the ground - lets you hop up a wall face.
        if (wasGrounded || touchedWall)
            _canJump = true;

        if (input.JumpPressed && _canJump && !actionLocked)
        {
            _verticalVelocity = -EffectiveJumpSpeed;
            _canJump = false;
            JumpedThisFrame = true;
        }
        else if (input.JumpPressed && !_canJump && !actionLocked && _remainingExtraJumps > 0)
        {
            // An air jump - doesn't need _canJump (that's for the grounded/wall jump above), just an
            // unspent charge from Cloven Hooves.
            _remainingExtraJumps--;
            _verticalVelocity = -EffectiveJumpSpeed;
            JumpedThisFrame = true;
        }

        float previousY = Position.Y;
        if (_isGroundPounding)
            _verticalVelocity = GroundPoundFallSpeed; // held constant, not accumulated - a true slam, not just a faster fall
        else
            _verticalVelocity += Gravity * delta;
        float candidateY = Position.Y + _verticalVelocity * delta;
        bool falling = _verticalVelocity >= 0f;

        if (!falling)
        {
            // Rising: a solid tier overhead stops the jump dead (bonk), instead of passing through it.
            // Position tracks the feet, but it's the HEAD (feet Y minus the full body height) that
            // needs to hit the platform's underside - checking the feet here would only notice the
            // ceiling once the head (and everything below it) had already punched through the platform.
            float headPreviousY = previousY - totalHeight;
            float headCandidateY = candidateY - totalHeight;
            float? ceiling = level.GetCeilingHeightAt(Position.X, headPreviousY, headCandidateY, true);
            if (ceiling.HasValue)
            {
                Position.Y = ceiling.Value + totalHeight;
                _verticalVelocity = 0f;
            }
            else
            {
                Position.Y = candidateY;
            }

            _isGrounded = false;
        }
        else
        {
            float landingHeight = level.GetLandingHeightAt(Position.X, previousY, candidateY, falling);

            // Walking downhill on the undulating ground moves you into lower terrain faster than one
            // frame of gravity can "catch up" to (gravity starts back at zero every time you land), so
            // without this the ground repeatedly appears to vanish out from under you for a single
            // frame - flipping between the standing and airborne poses every other frame on any slope.
            // Snap through small drops like this while still letting real ledges/gaps trigger a fall.
            if (wasGrounded && landingHeight - previousY <= MaxGroundSnapDistance)
                candidateY = MathF.Max(candidateY, landingHeight);

            if (candidateY >= landingHeight)
            {
                Position.Y = landingHeight;
                _verticalVelocity = 0f;
                if (!wasGrounded)
                    LandedThisFrame = true;
                if (_isGroundPounding)
                {
                    _isGroundPounding = false;
                    GroundPoundLandedThisFrame = true;
                    // A brief recovery window for the stomper themself - risk to weigh against the
                    // payoff of the slam, since they can't immediately move/act right after landing.
                    _stunTimer = MathF.Max(_stunTimer, GroundPoundSelfStunDuration);
                }
                _isGrounded = true;
                _canJump = true;
            }
            else
            {
                Position.Y = candidateY;
                _isGrounded = false;
            }
        }

        bool isWalking = moveDirection != 0f && _isGrounded;
        _walkCycleTime = isWalking ? _walkCycleTime + delta * WalkCycleSpeed : 0f;
    }

    private void UpdateBlockStamina(float delta)
    {
        float effectiveMax = EffectiveMaxBlockStamina;

        if (_isBlocking)
        {
            _blockStamina -= delta;
            if (_blockStamina <= 0f)
            {
                _blockStamina = 0f;
                _blockExhausted = true;
                _isBlocking = false;
            }
        }
        else
        {
            float rechargeRate = effectiveMax / BlockRechargeDuration;
            _blockStamina = MathHelper.Min(effectiveMax, _blockStamina + rechargeRate * delta);
            if (_blockExhausted && _blockStamina >= effectiveMax)
                _blockExhausted = false;
        }
    }

    // Drains health at the active poison's rate, capped to whatever's left of its duration this frame,
    // and drips blood on a steady timer while it advances the flash animation's phase.
    private void UpdatePoison(float delta)
    {
        float tick = MathHelper.Min(delta, _poisonRemainingDuration);
        Health = MathHelper.Clamp(Health - _poisonDamagePerSecond * tick, 0f, EffectiveMaxHealth);
        _poisonRemainingDuration -= tick;
        _poisonFlashTimer += delta;

        _poisonBleedTimer += delta;
        if (_poisonBleedTimer >= PoisonBleedInterval)
        {
            _poisonBleedTimer -= PoisonBleedInterval;
            PoisonBleedThisFrame = true;
        }

        if (Health <= 0f)
            HandlePossibleDeath(_poisonAttackerOnRight);
    }

    private void UpdateWeapon(PlayerInput input, float delta, Level level)
    {
        if (_isReloading)
        {
            _reloadElapsed += delta;
            if (_reloadElapsed >= EffectiveReloadDuration)
            {
                _isReloading = false;
                _ammoInMagazine = EffectiveMagazineSize;
            }
        }

        if (_fireCooldown > 0f)
            _fireCooldown -= delta;

        if (_recoilTimer > 0f)
            _recoilTimer -= delta;

        if (input.FireHeld && !_isBlocking && !IsStunned && !_isReloading && _fireCooldown <= 0f && _ammoInMagazine > 0)
        {
            Vector2 armPivot = GetArmPivot();
            Vector2 candidateMuzzle = armPivot + AimDirection * (ArmLength + GunLength);

            // If the barrel is currently poking through a wall, don't let it fire - otherwise a
            // player pressed against a wall could shoot through it at whoever's on the other side.
            if (IsGunClippingWall(armPivot, candidateMuzzle, level))
                return;

            _fireCooldown = EffectiveFireCooldown;
            _recoilTimer = RecoilDuration;
            FiredThisFrame = true;
            _ammoInMagazine--;

            MuzzlePosition = candidateMuzzle;

            if (_ammoInMagazine <= 0)
            {
                _isReloading = true;
                _reloadElapsed = 0f;
                ReloadStartedThisFrame = true;
            }
        }
    }

    private static bool IsGunClippingWall(Vector2 armPivot, Vector2 muzzle, Level level)
    {
        const int sampleCount = 4;
        for (int i = 1; i <= sampleCount; i++)
        {
            Vector2 point = Vector2.Lerp(armPivot, muzzle, i / (float)sampleCount);
            if (level.IsPointInsideAnyWall(point))
                return true;
        }

        return false;
    }

    private void UpdateDeath(float delta, Level level)
    {
        float previousY = Position.Y;
        _verticalVelocity += Gravity * delta;
        float candidateY = Position.Y + _verticalVelocity * delta;
        bool falling = _verticalVelocity >= 0f;

        if (!falling)
        {
            float? ceiling = level.GetCeilingHeightAt(Position.X, previousY, candidateY, true);
            Position.Y = ceiling ?? candidateY;
            if (ceiling.HasValue)
                _verticalVelocity = 0f;
        }
        else
        {
            float landingHeight = level.GetLandingHeightAt(Position.X, previousY, candidateY, falling);
            if (candidateY >= landingHeight)
            {
                Position.Y = landingHeight;
                _verticalVelocity = 0f;
            }
            else
            {
                Position.Y = candidateY;
            }
        }

        if (_deathAngle < MaxDeathAngle)
        {
            _deathAngularVelocity += DeathAngularAcceleration * delta;
            _deathAngle = MathHelper.Min(_deathAngle + _deathAngularVelocity * delta, MaxDeathAngle);
        }
    }

    public Rectangle GetHurtbox()
    {
        var (_, _, totalHeight) = GetBodyMetrics();
        return new Rectangle((int)(Position.X - BodyHalfWidth), (int)(Position.Y - totalHeight), (int)(BodyHalfWidth * 2f), (int)totalHeight);
    }

    // Called on the defender when a bullet (or ground pound/AoE aura hit) connects. attackerOnRight
    // is whether the attacker is to this stickman's right, damage is the attacker's EffectiveDamage
    // (or an override for non-gun sources), poisonDamagePerSecond is EffectivePoisonDamagePerSecond
    // (0 if none), and stunDuration - currently only ever passed by a ground pound - briefly locks
    // out movement/block/jump/fire if the hit lands. Returns true if the hit was blocked (no
    // damage, no blood, no poison, no stun).
    public bool ApplyHit(bool attackerOnRight, float damage, float poisonDamagePerSecond, float knockbackMultiplier = 1f, float stunDuration = 0f)
    {
        if (_isDead)
            return true;

        bool blocked = _isBlocking && FacingRight == attackerOnRight;
        if (blocked)
        {
            Position.X += FacingRight ? -BlockPushback * knockbackMultiplier : BlockPushback * knockbackMultiplier;
            return true;
        }

        Health = MathHelper.Clamp(Health - damage, 0f, EffectiveMaxHealth);
        Position.X += attackerOnRight ? -BulletPushback * knockbackMultiplier : BulletPushback * knockbackMultiplier;

        if (poisonDamagePerSecond > 0f)
        {
            _poisonDamagePerSecond = poisonDamagePerSecond;
            _poisonRemainingDuration = PoisonDuration;
            _poisonAttackerOnRight = attackerOnRight;
            _poisonBleedTimer = 0f;
            _poisonFlashTimer = 0f;
        }

        if (stunDuration > 0f)
            _stunTimer = MathF.Max(_stunTimer, stunDuration);

        if (Health <= 0f)
            HandlePossibleDeath(attackerOnRight);

        return false;
    }

    // Called on the attacker (not the defender) after a Life Steal hit lands - see the callers in
    // Game1 (ResolveBulletHits, TriggerSplashDamage, TriggerAoEAuraDamage), each of which already
    // knows the damage it just dealt and whether the hit was blocked.
    public void Heal(float amount)
    {
        if (!IsAlive)
            return;

        Health = MathHelper.Min(EffectiveMaxHealth, Health + amount);
    }

    // A fatal hit consumes a Second Wind (Extra Life) charge instead of killing, if one is still
    // unspent this round - see _remainingExtraLives, reset each round in ResetForNewRound.
    private void HandlePossibleDeath(bool attackerOnRight)
    {
        if (Health > 0f)
            return;

        if (_remainingExtraLives > 0)
        {
            _remainingExtraLives--;
            Health = EffectiveMaxHealth;
            return;
        }

        BeginRagdoll(attackerOnRight);
    }

    private void BeginRagdoll(bool attackerOnRight)
    {
        _isDead = true;
        DiedThisFrame = true;
        _isCrouching = false;
        _isBlocking = false;
        _isGroundPounding = false;
        _fallSign = attackerOnRight ? -1f : 1f;
        _deathAngle = 0f;
        _deathAngularVelocity = 1.5f;
    }

    public void ResetForNewRound(Vector2 startPosition)
    {
        Position = startPosition;
        Health = EffectiveMaxHealth;
        _remainingExtraLives = _bonusExtraLives;
        _remainingExtraJumps = _bonusExtraJumps;
        _lastMoveVelocityX = 0f;
        _isDead = false;
        _deathAngle = 0f;
        _deathAngularVelocity = 0f;
        _verticalVelocity = 0f;
        _isGrounded = true;
        _canJump = true;
        _isCrouching = false;
        _isBlocking = false;
        _isGroundPounding = false;
        _crouchWasHeld = false;
        _stunTimer = 0f;
        _fireCooldown = 0f;
        _recoilTimer = 0f;
        _ammoInMagazine = EffectiveMagazineSize;
        _isReloading = false;
        _reloadElapsed = 0f;
        _blockStamina = EffectiveMaxBlockStamina;
        _blockExhausted = false;
        _walkCycleTime = 0f;
        FacingRight = true;
        AimDirection = Vector2.UnitX;
        _poisonRemainingDuration = 0f;
        _poisonDamagePerSecond = 0f;
        _poisonBleedTimer = 0f;
        _poisonFlashTimer = 0f;
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Texture2D softCircle, Color color, float uiScale = 1f)
    {
        if (_isDead)
        {
            DrawRagdoll(spriteBatch, pixel, softCircle, color);
            return;
        }

        var (legLength, bodyLength, totalHeight) = GetBodyMetrics();
        float legSplay = MathHelper.Lerp(StanceSplay, CrouchSplay, _isCrouching ? 1f : 0f);
        float facing = FacingRight ? 1f : -1f;

        Vector2 headCenter = Position + new Vector2(0, -(legLength + bodyLength + HeadRadius));
        Vector2 neck = headCenter + new Vector2(0, HeadRadius);
        Vector2 hip = neck + new Vector2(0, bodyLength);
        Vector2 armPivot = neck + new Vector2(0, bodyLength * 0.25f);

        if (IsGrounded)
            Primitives2D.DrawGroundShadow(spriteBatch, softCircle, Position, BodyWidth * 1.6f);

        DrawTail(spriteBatch, pixel, softCircle, hip, facing, bodyLength);
        DrawTorso(spriteBatch, pixel, softCircle, neck, hip, color);

        if (_isBlocking)
        {
            // Arms crossed defensively in front of the chest - shown whether grounded or airborne,
            // since blocking now works in both.
            DrawHoofLimb(spriteBatch, pixel, softCircle, armPivot, armPivot + new Vector2(facing * ArmLength * 0.8f, -ArmLength * 0.3f), color);
            DrawHoofLimb(spriteBatch, pixel, softCircle, armPivot, armPivot + new Vector2(facing * ArmLength * 0.5f, ArmLength * 0.2f), color);
            DrawHoofLimb(spriteBatch, pixel, softCircle, hip, hip + Lean(-legSplay, legLength), color);
            DrawHoofLimb(spriteBatch, pixel, softCircle, hip, hip + Lean(legSplay, legLength), color);
        }
        else if (_isGroundPounding)
        {
            // Ground-pound slam pose: arms driven straight down, legs tucked tight beneath.
            DrawHoofLimb(spriteBatch, pixel, softCircle, armPivot, armPivot + new Vector2(facing * ArmLength * 0.3f, ArmLength * 0.9f), color);
            DrawHoofLimb(spriteBatch, pixel, softCircle, armPivot, armPivot + new Vector2(-facing * ArmLength * 0.3f, ArmLength * 0.9f), color);
            DrawHoofLimb(spriteBatch, pixel, softCircle, hip, hip + Lean(-legSplay * 0.6f, legLength * 0.5f), color);
            DrawHoofLimb(spriteBatch, pixel, softCircle, hip, hip + Lean(legSplay * 0.6f, legLength * 0.5f), color);
        }
        else if (IsStunned)
        {
            // Dazed pose: arms hanging loose, legs planted wide - makes the lockout visible at a
            // glance instead of looking identical to a normal idle stance.
            DrawHoofLimb(spriteBatch, pixel, softCircle, armPivot, armPivot + new Vector2(facing * ArmLength * 0.15f, ArmLength * 0.95f), color);
            DrawHoofLimb(spriteBatch, pixel, softCircle, armPivot, armPivot + new Vector2(-facing * ArmLength * 0.15f, ArmLength * 0.95f), color);
            DrawHoofLimb(spriteBatch, pixel, softCircle, hip, hip + Lean(-legSplay * 1.1f, legLength), color);
            DrawHoofLimb(spriteBatch, pixel, softCircle, hip, hip + Lean(legSplay * 1.1f, legLength), color);
        }
        else if (!IsGrounded)
        {
            // Tucked jump pose: knees bent up, arms raised for balance.
            DrawHoofLimb(spriteBatch, pixel, softCircle, armPivot, armPivot + Lean(-StanceSplay * 2f, ArmLength), color);
            DrawHoofLimb(spriteBatch, pixel, softCircle, armPivot, armPivot + Lean(StanceSplay * 2f, ArmLength), color);
            DrawHoofLimb(spriteBatch, pixel, softCircle, hip, hip + Lean(-legSplay * 1.5f, legLength * 0.7f), color);
            DrawHoofLimb(spriteBatch, pixel, softCircle, hip, hip + Lean(legSplay * 1.5f, legLength * 0.7f), color);
        }
        else
        {
            float swing = MathF.Sin(_walkCycleTime) * WalkSwingAngle;
            bool recoiling = _recoilTimer > 0f;
            float recoilPullback = recoiling ? (_recoilTimer / RecoilDuration) * 14f : 0f;

            Vector2 gunArmEnd = armPivot + AimDirection * (ArmLength + GunLength - recoilPullback);
            Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, armPivot, gunArmEnd, LimbThickness, color);
            float gunAngle = MathF.Atan2(AimDirection.Y, AimDirection.X);
            Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, gunArmEnd, new Vector2(16f, 9f), 3f, Color.Lerp(color, Color.Black, 0.35f), gunAngle);
            if (recoiling)
                Primitives2D.DrawBlob(spriteBatch, softCircle, gunArmEnd + AimDirection * 10f, new Vector2(9f, 9f), new Color(255, 235, 160));

            DrawHoofLimb(spriteBatch, pixel, softCircle, armPivot, armPivot - AimDirection * ArmLength * 0.5f, color);

            DrawHoofLimb(spriteBatch, pixel, softCircle, hip, hip + Lean(-legSplay + swing, legLength), color);
            DrawHoofLimb(spriteBatch, pixel, softCircle, hip, hip + Lean(legSplay - swing, legLength), color);
        }

        DrawCowHead(spriteBatch, pixel, softCircle, headCenter, facing, 0f);

        if (IsStunned)
        {
            // Three small stars orbiting above the head - a quick, unmistakable "dazed" read,
            // distinct from the poison/aura glows.
            Vector2 orbitCenter = headCenter + new Vector2(0f, -HeadRadius * 1.3f);
            for (int i = 0; i < 3; i++)
            {
                float angle = _stunTimer * StunOrbitSpeed + i * (MathHelper.TwoPi / 3f);
                Vector2 starPos = orbitCenter + new Vector2(MathF.Cos(angle) * 14f, MathF.Sin(angle) * 6f);
                Primitives2D.DrawCircle(spriteBatch, softCircle, starPos, 3.5f, new Color(255, 230, 90));
            }
        }

        if (IsPoisoned)
        {
            // A sickly green glow flashing over the whole body, fading out as the poison runs down.
            float pulse = 0.5f + 0.5f * MathF.Sin(_poisonFlashTimer * PoisonFlashSpeed);
            float alpha = MathHelper.Lerp(0.15f, 0.6f, pulse) * PoisonFraction;
            Vector2 auraCenter = Vector2.Lerp(neck, hip, 0.35f);
            Vector2 auraSize = new Vector2(BodyWidth * 1.5f, (legLength + bodyLength + HeadRadius * 2f) * 0.95f);
            Primitives2D.DrawBlob(spriteBatch, softCircle, auraCenter, auraSize, new Color(90, 210, 90) * alpha);
        }

        if (EffectiveAoERadius > 0f)
        {
            // A warm glowing aura, pulsing steadily rather than fading out like the timed poison
            // status above - it's a persistent stat, not a countdown - sized off the aura's own
            // radius so a bigger aura visibly reads bigger.
            float pulse = 0.5f + 0.5f * MathF.Sin(_aoeGlowTimer * AoEGlowPulseSpeed);
            float alpha = MathHelper.Lerp(0.15f, 0.5f, pulse);
            float auraScale = MathHelper.Clamp(EffectiveAoERadius / 60f, 0.8f, 2.5f);
            Vector2 auraCenter = Vector2.Lerp(neck, hip, 0.35f);
            Vector2 auraSize = new Vector2(BodyWidth * 1.5f, (legLength + bodyLength + HeadRadius * 2f) * 0.95f) * auraScale;
            Primitives2D.DrawBlob(spriteBatch, softCircle, auraCenter, auraSize, new Color(190, 90, 220) * alpha);
        }

        DrawAmmoHud(spriteBatch, pixel, softCircle, color, totalHeight, uiScale);
    }

    // The floating ammo/reload/block-stamina readout above the character - HUD, not the character
    // model itself, so it scales with uiScale (see Game1.UiScale) even though the body doesn't.
    private void DrawAmmoHud(SpriteBatch spriteBatch, Texture2D pixel, Texture2D softCircle, Color color, float totalHeight, float uiScale)
    {
        float dotSize = 7f * uiScale;
        float dotSpacing = 10f * uiScale;
        var emptyColor = new Color(55, 55, 55);

        int magazineSize = EffectiveMagazineSize;
        Vector2 hudAnchor = Position + new Vector2(0, -(totalHeight + 26f));
        float rowWidth = (magazineSize - 1) * dotSpacing;
        Vector2 firstDot = hudAnchor + new Vector2(-rowWidth / 2f, 0f);

        for (int i = 0; i < magazineSize; i++)
        {
            Vector2 dotCenter = firstDot + new Vector2(i * dotSpacing, 0f);
            Primitives2D.DrawCircle(spriteBatch, softCircle, dotCenter, dotSize, i < _ammoInMagazine ? color : emptyColor);
        }

        if (_isReloading)
        {
            Vector2 pieCenter = hudAnchor + new Vector2(0f, 14f * uiScale);
            DrawPieTimer(spriteBatch, pixel, softCircle, pieCenter, 8f * uiScale, _reloadElapsed / EffectiveReloadDuration, color, emptyColor);
        }

        // A small block-stamina bar above the ammo row, red while exhausted (can't block right now).
        float barWidth = 44f * uiScale;
        float barHeight = 5f * uiScale;
        Vector2 barCenter = hudAnchor + new Vector2(0f, -12f * uiScale + barHeight / 2f);
        Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, barCenter, new Vector2(barWidth, barHeight), barHeight * 0.5f, emptyColor);

        float staminaFraction = _blockStamina / EffectiveMaxBlockStamina;
        Color staminaColor = _blockExhausted ? new Color(200, 40, 40) : color;
        float fillWidth = barWidth * staminaFraction;
        if (fillWidth > 1f)
        {
            Vector2 fillCenter = hudAnchor + new Vector2(-barWidth / 2f + fillWidth / 2f, -12f * uiScale + barHeight / 2f);
            Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, fillCenter, new Vector2(fillWidth, barHeight), barHeight * 0.5f, staminaColor);
        }
    }

    private static void DrawPieTimer(SpriteBatch spriteBatch, Texture2D pixel, Texture2D softCircle, Vector2 center, float radius, float fraction, Color fillColor, Color trackColor)
    {
        Primitives2D.DrawCircle(spriteBatch, softCircle, center, radius * 2f, trackColor);

        const int segments = 20;
        int filledSegments = (int)MathF.Ceiling(segments * MathHelper.Clamp(fraction, 0f, 1f));
        float startAngle = -MathHelper.PiOver2;

        for (int i = 0; i <= filledSegments; i++)
        {
            float angle = startAngle + MathHelper.TwoPi * i / segments;
            Vector2 point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            DrawLine(spriteBatch, pixel, center, point, fillColor);
        }
    }

    private void DrawRagdoll(SpriteBatch spriteBatch, Texture2D pixel, Texture2D softCircle, Color color)
    {
        var (legLength, bodyLength, _) = GetBodyMetrics();
        float angle = _fallSign * _deathAngle;
        float facing = FacingRight ? 1f : -1f;

        Vector2 headLocal = new Vector2(0, -(legLength + bodyLength + HeadRadius));
        Vector2 neckLocal = new Vector2(0, -(legLength + bodyLength));
        Vector2 hipLocal = new Vector2(0, -legLength);
        Vector2 armPivotLocal = neckLocal + new Vector2(0, bodyLength * 0.25f);

        Vector2 headCenter = Position + Primitives2D.RotateVector(headLocal, angle);
        Vector2 neck = Position + Primitives2D.RotateVector(neckLocal, angle);
        Vector2 hip = Position + Primitives2D.RotateVector(hipLocal, angle);
        Vector2 armPivot = Position + Primitives2D.RotateVector(armPivotLocal, angle);
        Vector2 leftLeg = Position + Primitives2D.RotateVector(hipLocal + Lean(-StanceSplay, legLength), angle);
        Vector2 rightLeg = Position + Primitives2D.RotateVector(hipLocal + Lean(StanceSplay, legLength), angle);
        Vector2 leftArm = Position + Primitives2D.RotateVector(armPivotLocal + Lean(-StanceSplay, ArmLength), angle);
        Vector2 rightArm = Position + Primitives2D.RotateVector(armPivotLocal + Lean(StanceSplay, ArmLength), angle);

        DrawTorso(spriteBatch, pixel, softCircle, neck, hip, color, angle);
        DrawHoofLimb(spriteBatch, pixel, softCircle, hip, leftLeg, color);
        DrawHoofLimb(spriteBatch, pixel, softCircle, hip, rightLeg, color);
        DrawHoofLimb(spriteBatch, pixel, softCircle, armPivot, leftArm, color);
        DrawHoofLimb(spriteBatch, pixel, softCircle, armPivot, rightArm, color);
        DrawCowHead(spriteBatch, pixel, softCircle, headCenter, facing, angle);
    }

    private Vector2 GetArmPivot()
    {
        var (legLength, bodyLength, _) = GetBodyMetrics();
        Vector2 neck = Position + new Vector2(0, -(legLength + bodyLength));
        return neck + new Vector2(0, bodyLength * 0.25f);
    }

    private (float legLength, float bodyLength, float totalHeight) GetBodyMetrics()
    {
        float crouchAmount = _isCrouching ? 1f : 0f;
        float bodyLength = MathHelper.Lerp(BodyLength, BodyLength * CrouchScale, crouchAmount);
        float legLength = MathHelper.Lerp(LegLength, LegLength * CrouchScale, crouchAmount);
        return (legLength, bodyLength, legLength + bodyLength + HeadRadius * 2f);
    }

    // A vector of the given length pointing mostly downward, rotated by angle (0 = straight down).
    private static Vector2 Lean(float angle, float length) => new Vector2(MathF.Sin(angle), MathF.Cos(angle)) * length;

    // Thin radial lines for the reload pie-timer wedge - the one shape too fiddly to be worth a
    // rounded/capsule treatment, so it keeps the plain hard-edged line primitive.
    private static void DrawLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, Vector2 end, Color color)
    {
        Vector2 edge = end - start;
        float angle = MathF.Atan2(edge.Y, edge.X);
        float length = edge.Length();

        spriteBatch.Draw(
            pixel,
            start,
            null,
            color,
            angle,
            Vector2.Zero,
            new Vector2(length, LineThickness),
            SpriteEffects.None,
            0f);
    }

    // Cow-colored limb (not the player's accent color) with a small colored patch near where it
    // meets the body - keeps legs/arms reading as an actual cow's, while still carrying enough of
    // the player's color (alongside the torso spots and gun) to tell fighters apart at a glance.
    private static void DrawHoofLimb(SpriteBatch spriteBatch, Texture2D pixel, Texture2D softCircle, Vector2 start, Vector2 end, Color accentColor)
    {
        Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, start, end, LimbThickness, CowBodyColor);
        Primitives2D.DrawCircle(spriteBatch, softCircle, Vector2.Lerp(start, end, 0.35f), LimbSpotSize, accentColor);
        Primitives2D.DrawCircle(spriteBatch, softCircle, end, HoofSize, CowDarkColor);
    }

    private void DrawTail(SpriteBatch spriteBatch, Texture2D pixel, Texture2D softCircle, Vector2 hip, float facing, float bodyLength)
    {
        Vector2 tailBase = hip + new Vector2(-facing * BodyHalfWidth * 0.6f, -bodyLength * 0.15f);
        float wag = MathF.Sin(_walkCycleTime) * 6f;
        Vector2 tailEnd = tailBase + new Vector2(-facing * (TailLength * 0.5f + wag), TailLength * 0.7f);
        Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, tailBase, tailEnd, TailThickness, CowDarkColor);
        Primitives2D.DrawCircle(spriteBatch, softCircle, tailEnd, 7f, CowDarkColor);
    }

    private static void DrawTorso(SpriteBatch spriteBatch, Texture2D pixel, Texture2D softCircle, Vector2 neck, Vector2 hip, Color color, float rotation = 0f)
    {
        Vector2 center = Vector2.Lerp(neck, hip, 0.5f);
        float height = Vector2.Distance(neck, hip) + BodyHalfWidth * 0.4f;
        Vector2 Rot(Vector2 offset) => Primitives2D.RotateVector(offset, rotation);

        Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center, new Vector2(BodyWidth, height), BodyWidth * 0.5f, CowBodyColor, rotation);
        Primitives2D.DrawBlob(spriteBatch, softCircle, center + Rot(new Vector2(-BodyWidth * 0.22f, -height * 0.18f)), new Vector2(SpotSize, SpotSize * 0.85f), color, rotation);
        Primitives2D.DrawBlob(spriteBatch, softCircle, center + Rot(new Vector2(BodyWidth * 0.18f, height * 0.22f)), new Vector2(SpotSize * 0.8f, SpotSize * 0.7f), color, rotation);
    }

    private static void DrawCowHead(SpriteBatch spriteBatch, Texture2D pixel, Texture2D softCircle, Vector2 headCenter, float facing, float rotation)
    {
        Vector2 Rot(Vector2 offset) => Primitives2D.RotateVector(offset, rotation);

        Primitives2D.DrawBlob(spriteBatch, softCircle, headCenter, new Vector2(HeadSize, HeadSize * 0.95f), CowBodyColor, rotation);

        Primitives2D.DrawBlob(spriteBatch, softCircle, headCenter + Rot(new Vector2(-HeadSize * 0.55f, -HeadSize * 0.45f)), new Vector2(EarSize, EarSize * 0.8f), CowBodyColor, rotation);
        Primitives2D.DrawBlob(spriteBatch, softCircle, headCenter + Rot(new Vector2(HeadSize * 0.55f, -HeadSize * 0.45f)), new Vector2(EarSize, EarSize * 0.8f), CowBodyColor, rotation);

        Vector2 leftHornBase = headCenter + Rot(new Vector2(-HeadSize * 0.3f, -HeadSize * 0.55f));
        Vector2 rightHornBase = headCenter + Rot(new Vector2(HeadSize * 0.3f, -HeadSize * 0.55f));
        Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, leftHornBase, leftHornBase + Rot(new Vector2(0, -HornLength)), HornThickness, CowHornColor);
        Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, rightHornBase, rightHornBase + Rot(new Vector2(0, -HornLength)), HornThickness, CowHornColor);

        Primitives2D.DrawBlob(spriteBatch, softCircle, headCenter + Rot(new Vector2(-HeadSize * 0.25f, HeadSize * 0.15f)), new Vector2(SpotSize * 0.8f, SpotSize * 0.7f), CowDarkColor, rotation);

        Vector2 snoutCenter = headCenter + Rot(new Vector2(facing * HeadSize * 0.3f, HeadSize * 0.3f));
        Primitives2D.DrawBlob(spriteBatch, softCircle, snoutCenter, new Vector2(SnoutWidth, SnoutHeight), CowSnoutColor, rotation);

        Primitives2D.DrawCircle(spriteBatch, softCircle, snoutCenter + Rot(new Vector2(facing * SnoutWidth * 0.1f, -SnoutHeight * 0.1f)), 3.5f, CowDarkColor);
        Primitives2D.DrawCircle(spriteBatch, softCircle, snoutCenter + Rot(new Vector2(facing * SnoutWidth * 0.1f, SnoutHeight * 0.25f)), 3.5f, CowDarkColor);

        Vector2 eyeCenter = headCenter + Rot(new Vector2(facing * HeadSize * 0.32f, -HeadSize * 0.05f));
        Primitives2D.DrawCircle(spriteBatch, softCircle, eyeCenter, 8f, CowDarkColor);
        Primitives2D.DrawCircle(spriteBatch, softCircle, eyeCenter + Rot(new Vector2(facing * 1.6f, -1.6f)), 3.2f, Color.White);
    }
}
