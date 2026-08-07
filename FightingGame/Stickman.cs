using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FightingGame;

public class Stickman
{
    private const float LineThickness = 4f;
    private const float MoveSpeed = 250f;

    private const float HeadRadius = 12f;
    private const float BodyLength = 40f;
    private const float ArmLength = 22f;
    private const float LegLength = 30f;
    private const float BodyHalfWidth = 16f;
    private const float GunLength = 16f;

    private const float JumpSpeed = 930f;
    private const float Gravity = 1600f;

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

    private const float DeathAngularAcceleration = 6f;
    private static readonly float MaxDeathAngle = MathHelper.PiOver2;

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

    private bool _isDead;
    private float _fallSign;
    private float _deathAngle;
    private float _deathAngularVelocity;

    public bool IsGrounded => _isGrounded;
    public bool FacingRight { get; private set; } = true;
    public Vector2 AimDirection { get; private set; } = Vector2.UnitX;
    public float Health { get; private set; } = MaxHealth;
    public float HealthFraction => Health / MaxHealth;
    public bool IsAlive => Health > 0f;

    // Set for the one frame a shot is fired, so the caller can spawn a bullet at MuzzlePosition.
    public bool FiredThisFrame { get; private set; }
    public Vector2 MuzzlePosition { get; private set; }

    // Feet position (bottom-center of the stickman).
    public Vector2 Position;

    public void Update(GameTime gameTime, PlayerInput input, float minX, float maxX, Level level)
    {
        float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
        FiredThisFrame = false;

        if (_isDead)
        {
            UpdateDeath(delta, level);
            return;
        }

        if (input.AimDirection.HasValue)
        {
            AimDirection = input.AimDirection.Value;
            if (MathF.Abs(AimDirection.X) > 0.15f)
                FacingRight = AimDirection.X > 0f;
        }

        bool wasGrounded = _isGrounded;
        _isCrouching = wasGrounded && input.CrouchHeld;

        bool wantsToBlock = wasGrounded && !_isCrouching && input.BlockHeld && !_blockExhausted;
        _isBlocking = wantsToBlock && _blockStamina > 0f;
        UpdateBlockStamina(delta);

        UpdateWeapon(input, delta);

        bool actionLocked = _isCrouching || _isBlocking;
        float moveDirection = actionLocked ? 0f : input.MoveDirection;

        Position.X += moveDirection * MoveSpeed * delta;
        Position.X = MathHelper.Clamp(Position.X, minX, maxX);

        var (_, _, totalHeight) = GetBodyMetrics();
        bool touchedWall = level.ResolveWallCollision(ref Position, BodyHalfWidth, totalHeight);

        // Touching a wall refreshes the jump, same as standing on the ground - lets you hop up a wall face.
        if (wasGrounded || touchedWall)
            _canJump = true;

        if (input.JumpPressed && _canJump && !actionLocked)
        {
            _verticalVelocity = -JumpSpeed;
            _canJump = false;
        }

        float previousY = Position.Y;
        _verticalVelocity += Gravity * delta;
        float candidateY = Position.Y + _verticalVelocity * delta;
        bool falling = _verticalVelocity >= 0f;

        if (!falling)
        {
            // Rising: a solid tier overhead stops the jump dead (bonk), instead of passing through it.
            float? ceiling = level.GetCeilingHeightAt(Position.X, previousY, candidateY, true);
            if (ceiling.HasValue)
            {
                Position.Y = ceiling.Value;
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
            if (candidateY >= landingHeight)
            {
                Position.Y = landingHeight;
                _verticalVelocity = 0f;
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
            float rechargeRate = MaxBlockStamina / BlockRechargeDuration;
            _blockStamina = MathHelper.Min(MaxBlockStamina, _blockStamina + rechargeRate * delta);
            if (_blockExhausted && _blockStamina >= MaxBlockStamina)
                _blockExhausted = false;
        }
    }

    private void UpdateWeapon(PlayerInput input, float delta)
    {
        if (_isReloading)
        {
            _reloadElapsed += delta;
            if (_reloadElapsed >= ReloadDuration)
            {
                _isReloading = false;
                _ammoInMagazine = MagazineSize;
            }
        }

        if (_fireCooldown > 0f)
            _fireCooldown -= delta;

        if (_recoilTimer > 0f)
            _recoilTimer -= delta;

        if (input.FireHeld && !_isBlocking && !_isReloading && _fireCooldown <= 0f && _ammoInMagazine > 0)
        {
            _fireCooldown = FireCooldownDuration;
            _recoilTimer = RecoilDuration;
            FiredThisFrame = true;
            _ammoInMagazine--;

            MuzzlePosition = GetArmPivot() + AimDirection * (ArmLength + GunLength);

            if (_ammoInMagazine <= 0)
            {
                _isReloading = true;
                _reloadElapsed = 0f;
            }
        }
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

    // Called on the defender when a bullet connects. attackerOnRight is whether the shooter is to this stickman's right.
    // Returns true if the hit was blocked (no damage, no blood).
    public bool ApplyHit(bool attackerOnRight)
    {
        if (_isDead)
            return true;

        bool blocked = _isBlocking && FacingRight == attackerOnRight;
        if (blocked)
        {
            Position.X += FacingRight ? -BlockPushback : BlockPushback;
            return true;
        }

        Health = MathHelper.Clamp(Health - GunDamage, 0f, MaxHealth);
        Position.X += attackerOnRight ? -BulletPushback : BulletPushback;

        if (Health <= 0f)
            BeginRagdoll(attackerOnRight);

        return false;
    }

    private void BeginRagdoll(bool attackerOnRight)
    {
        _isDead = true;
        _isCrouching = false;
        _isBlocking = false;
        _fallSign = attackerOnRight ? -1f : 1f;
        _deathAngle = 0f;
        _deathAngularVelocity = 1.5f;
    }

    public void ResetForNewRound(Vector2 startPosition)
    {
        Position = startPosition;
        Health = MaxHealth;
        _isDead = false;
        _deathAngle = 0f;
        _deathAngularVelocity = 0f;
        _verticalVelocity = 0f;
        _isGrounded = true;
        _canJump = true;
        _isCrouching = false;
        _isBlocking = false;
        _fireCooldown = 0f;
        _recoilTimer = 0f;
        _ammoInMagazine = MagazineSize;
        _isReloading = false;
        _reloadElapsed = 0f;
        _blockStamina = MaxBlockStamina;
        _blockExhausted = false;
        _walkCycleTime = 0f;
        FacingRight = true;
        AimDirection = Vector2.UnitX;
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Color color)
    {
        if (_isDead)
        {
            DrawRagdoll(spriteBatch, pixel, color);
            return;
        }

        var (legLength, bodyLength, totalHeight) = GetBodyMetrics();
        float legSplay = MathHelper.Lerp(StanceSplay, CrouchSplay, _isCrouching ? 1f : 0f);
        float facing = FacingRight ? 1f : -1f;

        Vector2 headCenter = Position + new Vector2(0, -(legLength + bodyLength + HeadRadius));
        Vector2 neck = headCenter + new Vector2(0, HeadRadius);
        Vector2 hip = neck + new Vector2(0, bodyLength);
        Vector2 armPivot = neck + new Vector2(0, bodyLength * 0.25f);

        DrawCircle(spriteBatch, pixel, headCenter, HeadRadius, color);
        DrawLine(spriteBatch, pixel, neck, hip, color);

        if (!IsGrounded)
        {
            // Tucked jump pose: knees bent up, arms raised for balance.
            DrawLine(spriteBatch, pixel, armPivot, armPivot + Lean(-StanceSplay * 2f, ArmLength), color);
            DrawLine(spriteBatch, pixel, armPivot, armPivot + Lean(StanceSplay * 2f, ArmLength), color);
            DrawLine(spriteBatch, pixel, hip, hip + Lean(-legSplay * 1.5f, legLength * 0.7f), color);
            DrawLine(spriteBatch, pixel, hip, hip + Lean(legSplay * 1.5f, legLength * 0.7f), color);
        }
        else if (_isBlocking)
        {
            // Arms crossed defensively in front of the chest.
            DrawLine(spriteBatch, pixel, armPivot, armPivot + new Vector2(facing * ArmLength * 0.8f, -ArmLength * 0.3f), color);
            DrawLine(spriteBatch, pixel, armPivot, armPivot + new Vector2(facing * ArmLength * 0.5f, ArmLength * 0.2f), color);
            DrawLine(spriteBatch, pixel, hip, hip + Lean(-legSplay, legLength), color);
            DrawLine(spriteBatch, pixel, hip, hip + Lean(legSplay, legLength), color);
        }
        else
        {
            float swing = MathF.Sin(_walkCycleTime) * WalkSwingAngle;
            bool recoiling = _recoilTimer > 0f;
            float recoilPullback = recoiling ? (_recoilTimer / RecoilDuration) * 14f : 0f;

            Vector2 gunArmEnd = armPivot + AimDirection * (ArmLength + GunLength - recoilPullback);
            DrawLine(spriteBatch, pixel, armPivot, gunArmEnd, color);
            DrawSquare(spriteBatch, pixel, gunArmEnd, 7f, color);
            if (recoiling)
                DrawSquare(spriteBatch, pixel, gunArmEnd + AimDirection * 10f, 5f, color);

            DrawLine(spriteBatch, pixel, armPivot, armPivot - AimDirection * ArmLength * 0.5f, color);

            DrawLine(spriteBatch, pixel, hip, hip + Lean(-legSplay + swing, legLength), color);
            DrawLine(spriteBatch, pixel, hip, hip + Lean(legSplay - swing, legLength), color);
        }

        DrawAmmoHud(spriteBatch, pixel, color, totalHeight);
    }

    private void DrawAmmoHud(SpriteBatch spriteBatch, Texture2D pixel, Color color, float totalHeight)
    {
        const float dotSize = 6f;
        const float dotSpacing = 10f;
        var emptyColor = new Color(55, 55, 55);

        Vector2 hudAnchor = Position + new Vector2(0, -(totalHeight + 26f));
        float rowWidth = (MagazineSize - 1) * dotSpacing;
        Vector2 firstDot = hudAnchor + new Vector2(-rowWidth / 2f, 0f);

        for (int i = 0; i < MagazineSize; i++)
        {
            Vector2 dotCenter = firstDot + new Vector2(i * dotSpacing, 0f);
            DrawSquare(spriteBatch, pixel, dotCenter, dotSize, i < _ammoInMagazine ? color : emptyColor);
        }

        if (_isReloading)
        {
            Vector2 pieCenter = hudAnchor + new Vector2(0f, 14f);
            DrawPieTimer(spriteBatch, pixel, pieCenter, 8f, _reloadElapsed / ReloadDuration, color, emptyColor);
        }

        // A small block-stamina bar above the ammo row, red while exhausted (can't block right now).
        const float barWidth = 44f;
        const float barHeight = 4f;
        Vector2 barTopLeft = hudAnchor + new Vector2(-barWidth / 2f, -12f);
        spriteBatch.Draw(pixel, new Rectangle((int)barTopLeft.X, (int)barTopLeft.Y, (int)barWidth, (int)barHeight), emptyColor);

        float staminaFraction = _blockStamina / MaxBlockStamina;
        Color staminaColor = _blockExhausted ? new Color(200, 40, 40) : color;
        spriteBatch.Draw(pixel, new Rectangle((int)barTopLeft.X, (int)barTopLeft.Y, (int)(barWidth * staminaFraction), (int)barHeight), staminaColor);
    }

    private static void DrawPieTimer(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, float fraction, Color fillColor, Color trackColor)
    {
        DrawCircle(spriteBatch, pixel, center, radius, trackColor);

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

    private void DrawRagdoll(SpriteBatch spriteBatch, Texture2D pixel, Color color)
    {
        var (legLength, bodyLength, _) = GetBodyMetrics();
        float angle = _fallSign * _deathAngle;

        Vector2 headLocal = new Vector2(0, -(legLength + bodyLength + HeadRadius));
        Vector2 neckLocal = new Vector2(0, -(legLength + bodyLength));
        Vector2 hipLocal = new Vector2(0, -legLength);
        Vector2 armPivotLocal = neckLocal + new Vector2(0, bodyLength * 0.25f);

        Vector2 headCenter = Position + RotateVector(headLocal, angle);
        Vector2 neck = Position + RotateVector(neckLocal, angle);
        Vector2 hip = Position + RotateVector(hipLocal, angle);
        Vector2 armPivot = Position + RotateVector(armPivotLocal, angle);
        Vector2 leftLeg = Position + RotateVector(hipLocal + Lean(-StanceSplay, legLength), angle);
        Vector2 rightLeg = Position + RotateVector(hipLocal + Lean(StanceSplay, legLength), angle);
        Vector2 leftArm = Position + RotateVector(armPivotLocal + Lean(-StanceSplay, ArmLength), angle);
        Vector2 rightArm = Position + RotateVector(armPivotLocal + Lean(StanceSplay, ArmLength), angle);

        DrawCircle(spriteBatch, pixel, headCenter, HeadRadius, color);
        DrawLine(spriteBatch, pixel, neck, hip, color);
        DrawLine(spriteBatch, pixel, hip, leftLeg, color);
        DrawLine(spriteBatch, pixel, hip, rightLeg, color);
        DrawLine(spriteBatch, pixel, armPivot, leftArm, color);
        DrawLine(spriteBatch, pixel, armPivot, rightArm, color);
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

    private static Vector2 RotateVector(Vector2 v, float angle)
    {
        float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

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

    private static void DrawSquare(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float size, Color color)
    {
        spriteBatch.Draw(pixel, center, null, color, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size), SpriteEffects.None, 0f);
    }

    private static void DrawCircle(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, Color color)
    {
        const int segments = 16;
        Vector2 previous = center + new Vector2(radius, 0);
        for (int i = 1; i <= segments; i++)
        {
            float theta = MathHelper.TwoPi * i / segments;
            Vector2 next = center + new Vector2(MathF.Cos(theta), MathF.Sin(theta)) * radius;
            DrawLine(spriteBatch, pixel, previous, next, color);
            previous = next;
        }
    }
}
