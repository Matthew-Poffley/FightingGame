using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace FightingGame;

public class Game1 : Game
{
    private enum RoundPhase { Fighting, RoundOver, CardSelection }

    private class CardSelectionState
    {
        public Card[] Options;
        public int SelectedIndex;
        public bool Confirmed;
        public float PreviousMoveDirection;
    }

    // A join in progress but not yet finalized into a Player - the source (keyboard or one
    // controller slot) is picking which gun sound kit to use before it actually enters the match.
    private class GunKitSelectionState
    {
        public bool UsesKeyboard;
        public PlayerIndex? ControllerIndex;
        public Color Color;
        public int SelectedKitIndex;
        public float PreviousMoveDirection;

        // Starts true so the same button press that started the join doesn't also confirm it.
        public bool ConfirmWasDown = true;
    }

    // A brief expanding-and-fading flash left behind by an Explosive Rounds detonation - purely
    // visual, the actual splash damage is resolved once in TriggerSplashDamage.
    private class ExplosionEffect
    {
        private const float Duration = 0.35f;

        public Vector2 Position;
        public float Radius;
        public float Age;
        public bool Finished => Age >= Duration;

        public ExplosionEffect(Vector2 position, float radius)
        {
            Position = position;
            Radius = radius;
        }

        public void Update(float delta) => Age += delta;

        public void Draw(SpriteBatch spriteBatch, Texture2D softCircle)
        {
            float t = Age / Duration;
            float diameter = Radius * 2f * MathHelper.Lerp(0.35f, 1.15f, t);
            float alpha = MathHelper.Lerp(0.85f, 0f, t);
            Primitives2D.DrawCircle(spriteBatch, softCircle, Position, diameter, new Color(255, 170, 60) * alpha);
            Primitives2D.DrawCircle(spriteBatch, softCircle, Position, diameter * 0.55f, new Color(255, 235, 180) * (alpha * 0.9f));
        }
    }

    private static readonly PlayerIndex[] ControllerSlots =
    {
        PlayerIndex.One, PlayerIndex.Two, PlayerIndex.Three, PlayerIndex.Four
    };

    // Slot 0 is the keyboard; slots 1-4 map to controllers One-Four. Neon colors so nothing disappears against the black background.
    private static readonly Color[] PlayerColors =
    {
        new Color(57, 255, 20),   // neon green
        new Color(255, 20, 147),  // neon pink
        new Color(0, 255, 255),   // neon cyan
        new Color(255, 255, 0),   // neon yellow
        new Color(255, 95, 31)    // neon orange
    };

    private static readonly Color TerrainColor = new Color(35, 26, 20);
    private static readonly Color GrassColor = new Color(42, 68, 35);
    private static readonly Color GrassHighlightColor = new Color(70, 100, 65);
    private static readonly Color PlatformColor = new Color(95, 95, 105);
    private static readonly Color PlatformEdgeColor = new Color(140, 140, 150);
    private static readonly Color MovingPlatformColor = new Color(120, 90, 60);
    private static readonly Color MovingPlatformEdgeColor = new Color(210, 165, 90);
    private static readonly Color WallColor = new Color(60, 60, 68);
    private static readonly Color WallEdgeColor = new Color(100, 100, 112);

    // A dark, starlit night sky - keeps the arena dark like the original neon-on-black look (so
    // bullets/muzzle flashes read clearly) while still dressing it as a farm at night.
    private static readonly Color SkyTopColor = new Color(10, 14, 32);
    private static readonly Color SkyBottomColor = new Color(45, 42, 70);
    private static readonly Color StarColor = new Color(220, 225, 255);
    private static readonly Color MoonGlowColor = new Color(200, 210, 255);
    private static readonly Color MoonColor = new Color(235, 238, 245);
    private static readonly Color MoonCraterColor = new Color(205, 210, 220);
    private static readonly Color CloudColor = new Color(110, 120, 155);
    private static readonly Color FarHillColor = new Color(28, 42, 34);
    private static readonly Color NearHillColor = new Color(18, 30, 22);
    private static readonly Color BarnColor = new Color(90, 34, 30);
    private static readonly Color BarnRoofColor = new Color(30, 22, 24);
    private static readonly Color BarnTrimColor = new Color(120, 112, 98);
    private static readonly Color BarnWindowGlowColor = new Color(255, 205, 120);
    private static readonly Color FenceColor = new Color(55, 45, 38);
    private const float PlatformCornerRadius = 6f;
    private const float WallCornerRadius = 8f;
    private const float GrassDepth = 14f;

    private const float GroundMargin = 100f;
    private const float HealthBarWidth = 200f;
    private const float HealthBarHeight = 18f;
    private const float HealthBarMargin = 20f;
    private const float RoundOverDuration = 3f;
    private const float TerrainColumnWidth = 6f;
    private const int MinBoxCount = 3;
    private const int MaxBoxCount = 6;
    private const int MinBarrelCount = 0;
    private const int MaxBarrelCount = 2;
    private const float BoxBulletImpulse = 220f;
    private const float BoxPlayerPushSpeed = 140f;
    private const int BloodParticlesPerHit = 18;
    private const float SpawnMargin = 60f;
    private const float MinSpawnSeparation = 150f;
    private const int BoxDebrisParticleCount = 12;
    private const float ExplosionMinDamageFraction = 0.35f; // splash damage falloff floor at the edge of the blast radius
    private const float RoundStartGraceDuration = 2f; // no one can fire for the first couple of seconds of a round

    private static readonly Color[] BoxDebrisColors =
    {
        new Color(120, 85, 45), new Color(90, 60, 30), new Color(70, 45, 20)
    };

    private static readonly Color BarrelExplosionColor = new Color(255, 170, 60);

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private Texture2D _pixel;
    private Texture2D _softCircle;
    private Texture2D _skyGradient;
    private SpriteFont _font;
    private RenderTarget2D _terrainSurface;

    // Fallback for the round-win jingle only - "sounds/round win" currently holds an .m4a, which
    // SoundEffect can't decode (it only reads PCM .wav), so this plays until a .wav is dropped in
    // there too. See PlayWinSound.
    private SoundEffect _winSound;

    // Real recorded sound effects loaded from the "sounds" folder (see SoundBank) - a random clip is
    // picked from each list every time that action happens. Gun kits are handed out one-per-player at
    // join time (see HandleJoins) rather than played from a single shared list.
    private readonly List<SoundEffect> _jumpSounds = new();
    private readonly List<SoundEffect> _blockingSounds = new();
    private readonly List<SoundEffect> _cardSelectSounds = new();
    private readonly List<SoundEffect> _deathSounds = new();
    private readonly List<SoundEffect> _landingSounds = new();
    private readonly List<SoundEffect> _damageSounds = new();
    private readonly List<SoundEffect> _reloadSounds = new();
    private readonly List<SoundEffect> _bulletClashSounds = new();
    private readonly List<SoundEffect> _explosionSounds = new();
    private readonly List<SoundEffect> _roundWinSounds = new();
    private readonly List<List<SoundEffect>> _gunSoundKits = new();
    private readonly string[] _gunKitNames = { "Kit A", "Kit B", "Kit C" };

    private readonly Random _random = new();
    private readonly List<Player> _players = new();
    private readonly List<GunKitSelectionState> _gunKitSelections = new();
    private readonly List<Bullet> _bullets = new();
    private readonly List<Box> _boxes = new();
    private readonly List<Barrel> _barrels = new();
    private readonly List<BloodParticle> _bloodParticles = new();
    private readonly List<Stickman> _homingTargetsBuffer = new();
    private readonly List<ExplosionEffect> _explosions = new();

    private Level _level;

    // This round's usable arena size - randomized each round in GenerateLevel (see item 8: map size
    // variance) so rounds feel cramped or spacious rather than always exactly matching the screen.
    // Background/terrain rendering still spans the full viewport regardless - only the playable
    // bounds (spawn/box/barrel placement, movement clamp, bullet culling) and the elevated
    // platforms/interior walls Level generates respect these.
    private float _arenaWidth;
    private float _roundGroundMargin = GroundMargin;

    private RoundPhase _roundPhase = RoundPhase.Fighting;
    private float _roundOverTimer;
    private float _roundStartGraceTimer = RoundStartGraceDuration;
    private string _announcementText = "";
    private Color _announcementColor = Color.White;
    private Player _roundWinner;
    private readonly Dictionary<Player, CardSelectionState> _cardSelections = new();

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
    }

    protected override void Initialize()
    {
        ApplyDisplaySettings();

        base.Initialize();
    }

    // Exclusive fullscreen at the desktop resolution fails with a SharpDX E_INVALIDARG on some
    // GPU/driver/multi-monitor combinations. Fall back to a safe windowed mode rather than crashing.
    private void ApplyDisplaySettings()
    {
        try
        {
            var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            _graphics.PreferredBackBufferWidth = displayMode.Width;
            _graphics.PreferredBackBufferHeight = displayMode.Height;
            _graphics.IsFullScreen = true;
            _graphics.HardwareModeSwitch = false;
            _graphics.ApplyChanges();
        }
        catch (Exception)
        {
            _graphics.IsFullScreen = false;
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            _graphics.ApplyChanges();
        }
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _font = Content.Load<SpriteFont>("GameFont");

        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _softCircle = Primitives2D.CreateSoftCircleTexture(GraphicsDevice);

        _skyGradient = new Texture2D(GraphicsDevice, 1, 2);
        _skyGradient.SetData(new[] { SkyTopColor, SkyBottomColor });

        CreateSounds();
        LoadSoundBanks();
        GenerateLevel();
    }

    // Only the round-win jingle still has no usable recording (see the field comment above), so it's
    // the only effect left synthesized as a raw waveform.
    private void CreateSounds()
    {
        _winSound = SoundSynth.CreateArpeggio(new[] { 523f, 659f, 784f, 1046f }, 0.12f, 0.4f);
    }

    // Loads the real recorded effects from the "sounds" folder next to the exe - see SoundBank and
    // the csproj's CopyToOutputDirectory entry for that folder. The three "gun *" folders are kept as
    // separate kits so each player can be handed a distinct-sounding gun when they join.
    private void LoadSoundBanks()
    {
        _jumpSounds.AddRange(SoundBank.LoadFolder("jump sounds"));
        _blockingSounds.AddRange(SoundBank.LoadFolder("blocking"));
        _cardSelectSounds.AddRange(SoundBank.LoadFolder("card select"));
        _deathSounds.AddRange(SoundBank.LoadFolder("death sounds"));
        _landingSounds.AddRange(SoundBank.LoadFolder("landing sound"));
        _damageSounds.AddRange(SoundBank.LoadFolder("damage sounds"));
        _reloadSounds.AddRange(SoundBank.LoadFolder("reload"));
        _bulletClashSounds.AddRange(SoundBank.LoadFolder("bullet collides"));
        _explosionSounds.AddRange(SoundBank.LoadFolder("explosion"));
        _roundWinSounds.AddRange(SoundBank.LoadFolder("round win"));

        _gunSoundKits.Add(SoundBank.LoadFolder("gun sounds"));
        _gunSoundKits.Add(SoundBank.LoadFolder("gun 2"));
        _gunSoundKits.Add(SoundBank.LoadFolder("gun 3"));
    }

    // "sounds/round win" only has an .m4a in it right now, which SoundEffect can't decode, so
    // _roundWinSounds loads empty and this falls back to the synthesized jingle until a .wav lands
    // in that folder too.
    private void PlayWinSound()
    {
        if (_roundWinSounds.Count > 0)
            SoundBank.PlayRandom(_roundWinSounds, _random, 0.5f);
        else
            _winSound.Play(0.5f, 0f, 0f);
    }

    private const float MinArenaWidthFraction = 0.55f;
    private const float MinRoundGroundMargin = 60f;
    private const float MaxRoundGroundMargin = 220f;

    private void GenerateLevel()
    {
        float viewportWidth = GraphicsDevice.Viewport.Width;

        // Map size variance: usable arena width and vertical headroom both re-roll every round -
        // background/terrain still render full-screen (see RenderTerrainSurface/DrawTerrain), only
        // the playable bounds and Level's own generation shrink/grow.
        _arenaWidth = MathHelper.Lerp(viewportWidth * MinArenaWidthFraction, viewportWidth, (float)_random.NextDouble());
        _roundGroundMargin = MathHelper.Lerp(MinRoundGroundMargin, MaxRoundGroundMargin, (float)_random.NextDouble());

        float baseHeight = GraphicsDevice.Viewport.Height - _roundGroundMargin;
        _level = new Level(baseHeight, _arenaWidth, _random);

        _boxes.Clear();
        _barrels.Clear();
        _bloodParticles.Clear();

        int boxCount = _random.Next(MinBoxCount, MaxBoxCount + 1);
        for (int i = 0; i < boxCount; i++)
        {
            float x = MathHelper.Lerp(80f, _arenaWidth - 80f, (float)_random.NextDouble());
            _boxes.Add(new Box(new Vector2(x, DropToSurfaceHeight(x))));
        }

        int barrelCount = _random.Next(MinBarrelCount, MaxBarrelCount + 1);
        for (int i = 0; i < barrelCount; i++)
        {
            float x = MathHelper.Lerp(80f, _arenaWidth - 80f, (float)_random.NextDouble());
            _barrels.Add(new Barrel(new Vector2(x, DropToSurfaceHeight(x))));
        }

        RenderTerrainSurface();
    }

    // Simulates dropping from above the screen so the topmost tier (or the ground) covering this X is found.
    private float DropToSurfaceHeight(float x)
    {
        float groundHeight = _level.GetGroundHeightAt(x);
        return _level.GetLandingHeightAt(x, -10000f, groundHeight, falling: true);
    }

    // Terrain, platforms, walls, and blood decals never move once placed, so they're baked into a single
    // texture instead of being redrawn with hundreds of individual sprite calls every frame.
    private void RenderTerrainSurface()
    {
        int width = GraphicsDevice.Viewport.Width;
        int height = GraphicsDevice.Viewport.Height;

        _terrainSurface?.Dispose();
        // PreserveContents is required here - the default DiscardContents usage lets the GPU wipe
        // this target's contents each time it's reactivated, which was erasing the baked terrain
        // (and everything stamped onto it) whenever a new blood decal was drawn.
        _terrainSurface = new RenderTarget2D(
            GraphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

        GraphicsDevice.SetRenderTarget(_terrainSurface);
        GraphicsDevice.Clear(Color.Transparent);

        _spriteBatch.Begin();
        DrawFarmBackground();
        DrawTerrain();
        DrawPlatforms();
        DrawWalls();
        _spriteBatch.End();

        GraphicsDevice.SetRenderTarget(null);
    }

    private void StampBloodDecal(BloodDecal decal)
    {
        GraphicsDevice.SetRenderTarget(_terrainSurface);

        _spriteBatch.Begin();
        Primitives2D.DrawBlob(_spriteBatch, _softCircle, decal.Position, new Vector2(decal.Size, decal.Size), decal.Color);
        _spriteBatch.End();

        GraphicsDevice.SetRenderTarget(null);
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();

        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboard.IsKeyDown(Keys.Escape))
            Exit();

        UpdateGunKitSelections(keyboard);

        if (_roundPhase == RoundPhase.CardSelection)
        {
            UpdateCardSelection(keyboard);
            base.Update(gameTime);
            return;
        }

        HandleJoins(keyboard);

        float minX = 40f;
        float maxX = _arenaWidth - 40f;
        float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var playBounds = new Rectangle(-100, -100, (int)_arenaWidth + 200, GraphicsDevice.Viewport.Height + 200);

        if (_roundPhase == RoundPhase.RoundOver)
        {
            _level.Update(delta);

            // Keep updating everything (with no player input) so a death mid-round-end still finishes its ragdoll fall.
            foreach (var player in _players)
            {
                player.Stickman.Update(gameTime, default, minX, maxX, _level);
                PlayStickmanSounds(player);

                if (player.Stickman.PoisonBleedThisFrame)
                    SpawnPoisonBleed(player.Stickman.GetHurtbox(), player.Color);

                if (player.Stickman.GroundPoundLandedThisFrame)
                    TriggerGroundPoundImpact(player);

                if (player.Stickman.AoETickThisFrame)
                    TriggerAoEAuraDamage(player);
            }

            foreach (var box in _boxes)
                box.Update(delta, _level, minX, maxX);

            UpdateBullets(delta, playBounds);
            ResolveBulletCollisions();
            ResolveBulletHits();
            UpdateBlood(delta);
            UpdateExplosions(delta);

            _roundOverTimer -= delta;
            if (_roundOverTimer <= 0f)
            {
                if (_roundWinner != null && _players.Count > 1)
                    BeginCardSelection(_roundWinner);
                else
                    StartNewRound();
            }

            base.Update(gameTime);
            return;
        }

        if (_roundStartGraceTimer > 0f)
            _roundStartGraceTimer = MathF.Max(0f, _roundStartGraceTimer - delta);

        _level.Update(delta);

        var inputs = new PlayerInput[_players.Count];
        for (int i = 0; i < _players.Count; i++)
        {
            var input = _players[i].GatherInput(keyboard);

            // Weapons stay cold for the first couple of seconds of a round so everyone gets a chance
            // to scatter and take position before anyone can be shot.
            if (_roundStartGraceTimer > 0f && input.FireHeld)
            {
                input = new PlayerInput
                {
                    MoveDirection = input.MoveDirection,
                    JumpPressed = input.JumpPressed,
                    CrouchHeld = input.CrouchHeld,
                    FireHeld = false,
                    BlockHeld = input.BlockHeld,
                    AimDirection = input.AimDirection
                };
            }

            inputs[i] = input;
        }

        for (int i = 0; i < _players.Count; i++)
        {
            var stickman = _players[i].Stickman;
            stickman.Update(gameTime, inputs[i], minX, maxX, _level);
            PlayStickmanSounds(_players[i]);

            if (stickman.FiredThisFrame)
                SpawnBullets(stickman, _players[i].Color);

            if (stickman.PoisonBleedThisFrame)
                SpawnPoisonBleed(stickman.GetHurtbox(), _players[i].Color);

            if (stickman.GroundPoundLandedThisFrame)
                TriggerGroundPoundImpact(_players[i]);

            if (stickman.AoETickThisFrame)
                TriggerAoEAuraDamage(_players[i]);
        }

        ResolvePlayerBoxCollisions(minX, maxX);

        foreach (var box in _boxes)
            box.Update(delta, _level, minX, maxX);

        UpdateBullets(delta, playBounds);
        ResolveBulletCollisions();
        ResolveBulletHits();
        UpdateBlood(delta);
        UpdateExplosions(delta);
        CheckRoundEnd();

        base.Update(gameTime);
    }

    private void PlayStickmanSounds(Player player)
    {
        var stickman = player.Stickman;

        if (stickman.FiredThisFrame)
            SoundBank.PlayRandom(player.GunSounds, _random, 0.55f, (float)(_random.NextDouble() * 0.2 - 0.1));

        if (stickman.JumpedThisFrame)
            SoundBank.PlayRandom(_jumpSounds, _random, 0.5f);

        if (stickman.LandedThisFrame)
            SoundBank.PlayRandom(_landingSounds, _random, 0.4f);

        if (stickman.ReloadStartedThisFrame)
            SoundBank.PlayRandom(_reloadSounds, _random, 0.45f);

        if (stickman.DiedThisFrame)
            SoundBank.PlayRandom(_deathSounds, _random, 0.5f);
    }

    // A join press no longer drops you straight into the match - it opens a gun-sound-kit picker
    // (see UpdateGunKitSelections/DrawGunKitSelections) which finalizes into a Player on confirm.
    private void HandleJoins(KeyboardState keyboard)
    {
        bool keyboardJoined = _players.Exists(p => p.UsesKeyboard) || _gunKitSelections.Exists(g => g.UsesKeyboard);
        if (!keyboardJoined && keyboard.IsKeyDown(Keys.Space))
            _gunKitSelections.Add(new GunKitSelectionState { UsesKeyboard = true, Color = PlayerColors[0], SelectedKitIndex = _random.Next(Math.Max(1, _gunSoundKits.Count)) });

        for (int slot = 0; slot < ControllerSlots.Length; slot++)
        {
            var controllerIndex = ControllerSlots[slot];
            bool alreadyJoined = _players.Exists(p => p.ControllerIndex == controllerIndex) || _gunKitSelections.Exists(g => g.ControllerIndex == controllerIndex);
            if (alreadyJoined)
                continue;

            var padState = GamePad.GetState(controllerIndex);
            if (padState.IsConnected && padState.Buttons.A == ButtonState.Pressed)
                _gunKitSelections.Add(new GunKitSelectionState { ControllerIndex = controllerIndex, Color = PlayerColors[slot + 1], SelectedKitIndex = _random.Next(Math.Max(1, _gunSoundKits.Count)) });
        }
    }

    // Drives every in-progress gun-kit picker: left/right previews a kit (plays a sample shot),
    // confirm finalizes it into an actual Player. Runs every frame regardless of round phase so a
    // pending picker never gets stuck mid-round or mid-card-selection.
    private void UpdateGunKitSelections(KeyboardState keyboard)
    {
        for (int i = _gunKitSelections.Count - 1; i >= 0; i--)
        {
            var state = _gunKitSelections[i];
            float moveDirection = 0f;
            bool confirmDown = false;

            if (state.UsesKeyboard)
            {
                if (keyboard.IsKeyDown(Keys.Left) || keyboard.IsKeyDown(Keys.A))
                    moveDirection -= 1f;
                if (keyboard.IsKeyDown(Keys.Right) || keyboard.IsKeyDown(Keys.D))
                    moveDirection += 1f;
                confirmDown |= keyboard.IsKeyDown(Keys.Space);
            }

            if (state.ControllerIndex.HasValue)
            {
                var gamePad = GamePad.GetState(state.ControllerIndex.Value);
                if (gamePad.IsConnected)
                {
                    if (gamePad.DPad.Left == ButtonState.Pressed || gamePad.ThumbSticks.Left.X < -0.25f)
                        moveDirection -= 1f;
                    if (gamePad.DPad.Right == ButtonState.Pressed || gamePad.ThumbSticks.Left.X > 0.25f)
                        moveDirection += 1f;
                    confirmDown |= gamePad.Buttons.A == ButtonState.Pressed;
                }
            }

            bool pressedLeft = moveDirection < -0.5f && state.PreviousMoveDirection >= -0.5f;
            bool pressedRight = moveDirection > 0.5f && state.PreviousMoveDirection <= 0.5f;
            state.PreviousMoveDirection = moveDirection;

            int kitCount = _gunSoundKits.Count;
            if (kitCount > 0 && (pressedLeft || pressedRight))
            {
                state.SelectedKitIndex = (state.SelectedKitIndex + (pressedRight ? 1 : -1) + kitCount) % kitCount;
                SoundBank.PlayRandom(_gunSoundKits[state.SelectedKitIndex], _random, 0.5f);
            }

            bool confirmPressed = confirmDown && !state.ConfirmWasDown;
            state.ConfirmWasDown = confirmDown;

            if (confirmPressed)
            {
                var kit = kitCount > 0 ? _gunSoundKits[state.SelectedKitIndex] : new List<SoundEffect>();
                _players.Add(new Player(state.ControllerIndex, state.UsesKeyboard, state.Color, RandomSpawnPosition(), kit));
                _gunKitSelections.RemoveAt(i);
            }
        }
    }

    // Walking into a box shoves it out of the way and sets it sliding.
    private void ResolvePlayerBoxCollisions(float minX, float maxX)
    {
        foreach (var player in _players)
        {
            if (!player.Stickman.IsAlive)
                continue;

            var hurtbox = player.Stickman.GetHurtbox();
            foreach (var box in _boxes)
            {
                var boxBounds = box.GetBounds();
                if (!hurtbox.Intersects(boxBounds))
                    continue;

                float overlapLeft = hurtbox.Right - boxBounds.Left;
                float overlapRight = boxBounds.Right - hurtbox.Left;
                float pushDir = overlapLeft < overlapRight ? 1f : -1f;
                float pushDistance = MathF.Min(overlapLeft, overlapRight);

                box.Position.X += pushDir * pushDistance;
                box.Position.X = MathHelper.Clamp(box.Position.X, minX, maxX);
                box.VelocityX = pushDir * BoxPlayerPushSpeed;
            }
        }
    }

    private void UpdateBullets(float delta, Rectangle bounds)
    {
        _homingTargetsBuffer.Clear();
        foreach (var player in _players)
            _homingTargetsBuffer.Add(player.Stickman);

        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            var bullet = _bullets[i];
            bullet.Update(delta, bounds, _level, _homingTargetsBuffer);

            // Explosive Rounds detonates at every bounce point (Ricochet Rounds), not just the
            // bullet's final impact - the bullet survives a bounce, so this doesn't remove it.
            if (bullet.BouncedThisFrame && bullet.ExplosionRadius > 0f)
                TriggerSplashDamage(bullet.Position, bullet.ExplosionRadius, bullet.Owner, bullet.Color);

            if (!bullet.IsAlive)
            {
                if (bullet.HitSolid && bullet.ExplosionRadius > 0f)
                    TriggerSplashDamage(bullet.Position, bullet.ExplosionRadius, bullet.Owner, bullet.Color);

                _bullets.RemoveAt(i);
            }
        }
    }

    // Bullets fired by different players cancel each other out on contact.
    private void ResolveBulletCollisions()
    {
        for (int i = 0; i < _bullets.Count; i++)
        {
            for (int j = i + 1; j < _bullets.Count; j++)
            {
                var a = _bullets[i];
                var b = _bullets[j];
                if (a.Owner == b.Owner || !a.IsAlive || !b.IsAlive)
                    continue;

                if (a.GetBounds().Intersects(b.GetBounds()))
                {
                    a.IsAlive = false;
                    b.IsAlive = false;
                    SoundBank.PlayRandom(_bulletClashSounds, _random, 0.4f);
                }
            }
        }

        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            if (!_bullets[i].IsAlive)
                _bullets.RemoveAt(i);
        }
    }

    private void ResolveBulletHits()
    {
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            var bullet = _bullets[i];
            bool consumed = false;

            // Wall/floor/platform collisions (including any Ricochet Rounds bounce) are resolved
            // inside Bullet.Update, since it also owns the Level reference and previous-position
            // tracking a bounce needs.
            // Box/barrel hits wait for HasClearedSpawn too - a big BiggerBullets round can otherwise
            // spawn already overlapping a nearby prop's bounding box and vanish on the first frame.
            Box hitBox = null;
            if (bullet.HasClearedSpawn)
            {
                foreach (var box in _boxes)
                {
                    if (bullet.GetBounds().Intersects(box.GetBounds()))
                    {
                        hitBox = box;
                        break;
                    }
                }
            }

            if (hitBox != null)
            {
                hitBox.ApplyImpulse(MathF.Sign(bullet.Direction.X) * BoxBulletImpulse);
                hitBox.ApplyDamage(bullet.Owner.EffectiveDamage);
                consumed = true;

                if (bullet.ExplosionRadius > 0f)
                {
                    var boxCenter = hitBox.GetBounds().Center;
                    TriggerSplashDamage(new Vector2(boxCenter.X, boxCenter.Y), bullet.ExplosionRadius, bullet.Owner, bullet.Color, excludeBox: hitBox);
                }

                if (hitBox.IsBroken)
                {
                    SpawnBoxDebris(hitBox);
                    _boxes.Remove(hitBox);
                }
            }

            Barrel hitBarrel = null;
            if (!consumed && bullet.HasClearedSpawn)
            {
                foreach (var barrel in _barrels)
                {
                    if (bullet.GetBounds().Intersects(barrel.GetBounds()))
                    {
                        hitBarrel = barrel;
                        break;
                    }
                }
            }

            if (hitBarrel != null)
            {
                hitBarrel.ApplyDamage(bullet.Owner.EffectiveDamage);
                consumed = true;

                if (bullet.ExplosionRadius > 0f)
                {
                    var barrelCenter = hitBarrel.GetBounds().Center;
                    TriggerSplashDamage(new Vector2(barrelCenter.X, barrelCenter.Y), bullet.ExplosionRadius, bullet.Owner, bullet.Color, excludeBarrel: hitBarrel);
                }

                if (hitBarrel.IsBroken)
                    DetonateBarrel(hitBarrel);
            }

            if (!consumed)
            {
                foreach (var player in _players)
                {
                    bool isOwner = player.Stickman == bullet.Owner;
                    if ((isOwner && !bullet.CanHitOwner) || !player.Stickman.IsAlive)
                        continue;

                    if (bullet.GetBounds().Intersects(player.Stickman.GetHurtbox()))
                    {
                        bool attackerOnRight = bullet.Owner.Position.X > player.Stickman.Position.X;
                        bool blocked = player.Stickman.ApplyHit(attackerOnRight, bullet.Owner.EffectiveDamage, bullet.Owner.EffectivePoisonDamagePerSecond, bullet.Owner.EffectiveKnockbackMultiplier);
                        var hurtbox = player.Stickman.GetHurtbox();
                        Vector2 impactPoint = new Vector2(hurtbox.Center.X, hurtbox.Center.Y);

                        if (blocked)
                        {
                            SoundBank.PlayRandom(_blockingSounds, _random, 0.5f);

                            // A clean block deflects the bullet back the way it came - explosive
                            // rounds included, same as any other shot - rather than still detonating.
                            bullet.Deflect(player.Stickman, player.Color);
                            break;
                        }

                        SpawnBlood(impactPoint, player.Color, bullet.Direction);
                        SoundBank.PlayRandom(_damageSounds, _random, 0.5f, (float)(_random.NextDouble() * 0.2 - 0.1));
                        ApplyLifeSteal(bullet.Owner, bullet.Owner.EffectiveDamage);

                        if (bullet.ExplosionRadius > 0f)
                            TriggerSplashDamage(impactPoint, bullet.ExplosionRadius, bullet.Owner, bullet.Color, excludePlayer: player.Stickman);

                        consumed = true;
                        break;
                    }
                }
            }

            if (consumed)
                _bullets.RemoveAt(i);
        }
    }

    // Fires one bullet, or several fanned out symmetrically around the aim direction when the
    // shooter has picked up Buckshot Rounds (a shotgun-style scatter effect).
    private void SpawnBullets(Stickman stickman, Color color)
    {
        int count = stickman.EffectiveBulletCount;
        const float spreadPerExtraBullet = 0.09f;
        float baseAngle = MathF.Atan2(stickman.AimDirection.Y, stickman.AimDirection.X);
        float startAngle = baseAngle - spreadPerExtraBullet * (count - 1) / 2f;

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + i * spreadPerExtraBullet;
            Vector2 direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            _bullets.Add(new Bullet(
                stickman.MuzzlePosition, direction, color, stickman,
                stickman.BulletSpeedMultiplier, stickman.BulletRadiusMultiplier, stickman.EffectiveHomingTurnRate, stickman.EffectiveBounceCount,
                stickman.EffectiveExplosionRadius, stickman.BulletGravityMultiplier));
        }
    }

    // How long a ground pound stuns anyone caught in its blast (the stomper's own brief self-stun on
    // landing is handled separately, inside Stickman itself - see GroundPoundSelfStunDuration).
    private const float GroundPoundVictimStunDuration = 1f;

    // A ground-pound landing (see Stickman.GroundPoundLandedThisFrame) hits everyone else in radius,
    // but never the stomper themself.
    private void TriggerGroundPoundImpact(Player player)
    {
        var stickman = player.Stickman;
        TriggerSplashDamage(stickman.Position, stickman.EffectiveGroundPoundRadius, stickman, player.Color, excludePlayer: stickman, damageOverride: stickman.EffectiveGroundPoundDamage, stunDuration: GroundPoundVictimStunDuration);
    }

    // One tick of the AoE aura (see Stickman.AoETickThisFrame) - flat damage in range, no falloff,
    // no explosion visual (it's a persistent aura, not a one-off blast), never hits its own owner.
    private void TriggerAoEAuraDamage(Player player)
    {
        var stickman = player.Stickman;
        float radius = stickman.EffectiveAoERadius;
        float damage = stickman.EffectiveAoEDamagePerTick;
        Vector2 center = stickman.Position;

        foreach (var other in _players)
        {
            if (other.Stickman == stickman || !other.Stickman.IsAlive)
                continue;

            var hurtbox = other.Stickman.GetHurtbox();
            Vector2 targetCenter = new Vector2(hurtbox.Center.X, hurtbox.Center.Y);
            if (Vector2.Distance(center, targetCenter) > radius)
                continue;

            if (!_level.HasLineOfSight(center, targetCenter))
                continue;

            bool attackerOnRight = center.X > targetCenter.X;
            bool blocked = other.Stickman.ApplyHit(attackerOnRight, damage, 0f, 1f);
            if (!blocked)
            {
                SpawnBlood(targetCenter, player.Color, targetCenter - center);
                ApplyLifeSteal(stickman, damage);
            }
        }
    }

    // Heals attacker for a fraction of the damage they just dealt, if they have any Vampiric Bite
    // stacks - a no-op for barrels (attacker null) or anyone without the stat. Called from every
    // spot that lands unblocked damage: ResolveBulletHits' direct hit, TriggerSplashDamage, and
    // TriggerAoEAuraDamage above.
    private void ApplyLifeSteal(Stickman attacker, float damageDealt)
    {
        float fraction = attacker?.EffectiveLifeStealFraction ?? 0f;
        if (fraction > 0f)
            attacker.Heal(damageDealt * fraction);
    }

    // Splash damage from an Explosive Rounds detonation, a ground pound landing, or an exploding
    // barrel (owner is null for barrels - see DetonateBarrel - so poison/knockback/damage all fall
    // back to neutral defaults instead of reading from a shooter that doesn't exist), falling off
    // linearly with distance. Whoever (or whatever box/barrel) was already hit directly is passed
    // in to exclude so they aren't hit twice.
    private void TriggerSplashDamage(Vector2 center, float radius, Stickman owner, Color color, Stickman excludePlayer = null, Box excludeBox = null, Barrel excludeBarrel = null, float? damageOverride = null, float stunDuration = 0f)
    {
        float baseDamage = damageOverride ?? owner?.EffectiveDamage ?? 0f;
        float poisonDamagePerSecond = owner?.EffectivePoisonDamagePerSecond ?? 0f;
        float knockbackMultiplier = owner?.EffectiveKnockbackMultiplier ?? 1f;

        SoundBank.PlayRandom(_explosionSounds, _random, 0.6f, (float)(_random.NextDouble() * 0.1 - 0.05));
        _explosions.Add(new ExplosionEffect(center, radius));

        foreach (var player in _players)
        {
            if (player.Stickman == excludePlayer || !player.Stickman.IsAlive)
                continue;

            var hurtbox = player.Stickman.GetHurtbox();
            Vector2 targetCenter = new Vector2(hurtbox.Center.X, hurtbox.Center.Y);
            float distance = Vector2.Distance(center, targetCenter);
            if (distance > radius)
                continue;

            // A wall between the blast and the target shields them, even though they're in range.
            if (!_level.HasLineOfSight(center, targetCenter))
                continue;

            float falloff = MathHelper.Lerp(1f, ExplosionMinDamageFraction, distance / radius);
            bool attackerOnRight = center.X > targetCenter.X;
            bool blocked = player.Stickman.ApplyHit(attackerOnRight, baseDamage * falloff, poisonDamagePerSecond, knockbackMultiplier, stunDuration);
            if (!blocked)
            {
                SpawnBlood(targetCenter, color, targetCenter - center);
                ApplyLifeSteal(owner, baseDamage * falloff);
            }
        }

        foreach (var box in _boxes.ToArray())
        {
            if (box == excludeBox)
                continue;

            var boxBounds = box.GetBounds();
            Vector2 boxCenter = new Vector2(boxBounds.Center.X, boxBounds.Center.Y);
            float distance = Vector2.Distance(center, boxCenter);
            if (distance > radius)
                continue;

            if (!_level.HasLineOfSight(center, boxCenter))
                continue;

            float falloff = MathHelper.Lerp(1f, ExplosionMinDamageFraction, distance / radius);
            box.ApplyImpulse(MathF.Sign(boxCenter.X - center.X) * BoxBulletImpulse);
            box.ApplyDamage(baseDamage * falloff);
            if (box.IsBroken)
            {
                SpawnBoxDebris(box);
                _boxes.Remove(box);
            }
        }

        // Snapshotted up front since DetonateBarrel below can remove entries from _barrels
        // (including ones later in this same snapshot, on a chain reaction) mid-loop.
        foreach (var barrel in _barrels.ToArray())
        {
            if (barrel == excludeBarrel || !_barrels.Contains(barrel))
                continue;

            var barrelBounds = barrel.GetBounds();
            Vector2 barrelCenter = new Vector2(barrelBounds.Center.X, barrelBounds.Center.Y);
            float distance = Vector2.Distance(center, barrelCenter);
            if (distance > radius)
                continue;

            if (!_level.HasLineOfSight(center, barrelCenter))
                continue;

            float falloff = MathHelper.Lerp(1f, ExplosionMinDamageFraction, distance / radius);
            barrel.ApplyDamage(baseDamage * falloff);
            if (barrel.IsBroken)
                DetonateBarrel(barrel);
        }
    }

    // Removes the barrel and sets off its own blast - catching another barrel in that blast chains
    // into another DetonateBarrel call, which is what gives exploding barrels their chain reaction.
    private void DetonateBarrel(Barrel barrel)
    {
        if (!_barrels.Remove(barrel))
            return; // already detonated earlier in the same pass (e.g. two overlapping blasts)

        var bounds = barrel.GetBounds();
        Vector2 center = new Vector2(bounds.Center.X, bounds.Center.Y);
        TriggerSplashDamage(center, Barrel.ExplosionRadius, owner: null, color: BarrelExplosionColor, damageOverride: Barrel.ExplosionDamage);
    }

    private void UpdateExplosions(float delta)
    {
        for (int i = _explosions.Count - 1; i >= 0; i--)
        {
            _explosions[i].Update(delta);
            if (_explosions[i].Finished)
                _explosions.RemoveAt(i);
        }
    }

    // A burst of wood-colored debris (reusing the blood particle system for its arc/landing physics)
    // when a box takes enough damage to break.
    private void SpawnBoxDebris(Box box)
    {
        var bounds = box.GetBounds();
        Vector2 origin = new Vector2(bounds.Center.X, bounds.Center.Y);

        for (int i = 0; i < BoxDebrisParticleCount; i++)
        {
            float angle = (float)_random.NextDouble() * MathHelper.TwoPi;
            float speed = 100f + (float)_random.NextDouble() * 220f;
            Vector2 velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed + new Vector2(0f, -120f - (float)_random.NextDouble() * 120f);
            Color color = BoxDebrisColors[_random.Next(BoxDebrisColors.Length)];
            _bloodParticles.Add(new BloodParticle(origin, velocity, color));
        }

        SoundBank.PlayRandom(_damageSounds, _random, 0.55f, -0.15f);
    }

    private void SpawnBlood(Vector2 origin, Color color, Vector2 bulletDirection)
    {
        Vector2 baseDirection = bulletDirection.LengthSquared() > 0.01f ? bulletDirection : Vector2.UnitX;

        for (int i = 0; i < BloodParticlesPerHit; i++)
        {
            float angleJitter = ((float)_random.NextDouble() - 0.5f) * 1.4f;
            float cos = MathF.Cos(angleJitter);
            float sin = MathF.Sin(angleJitter);
            Vector2 direction = new Vector2(
                baseDirection.X * cos - baseDirection.Y * sin,
                baseDirection.X * sin + baseDirection.Y * cos);

            float speed = 150f + (float)_random.NextDouble() * 250f;
            Vector2 velocity = direction * speed + new Vector2(0f, -150f - (float)_random.NextDouble() * 150f);

            _bloodParticles.Add(new BloodParticle(origin, velocity, color));
        }
    }

    // A few light droplets welling up from a poisoned stickman - much gentler than a bullet's spray,
    // since this isn't a fresh wound but a steady ooze.
    private const int PoisonBleedDropletCount = 3;

    private void SpawnPoisonBleed(Rectangle hurtbox, Color color)
    {
        for (int i = 0; i < PoisonBleedDropletCount; i++)
        {
            Vector2 origin = new Vector2(
                hurtbox.X + (float)_random.NextDouble() * hurtbox.Width,
                hurtbox.Y + (float)_random.NextDouble() * hurtbox.Height);

            Vector2 velocity = new Vector2((float)(_random.NextDouble() - 0.5) * 40f, -20f - (float)_random.NextDouble() * 30f);
            _bloodParticles.Add(new BloodParticle(origin, velocity, color));
        }
    }

    private void UpdateBlood(float delta)
    {
        for (int i = _bloodParticles.Count - 1; i >= 0; i--)
        {
            var particle = _bloodParticles[i];
            particle.Update(delta, _level);
            if (particle.Landed)
            {
                float size = 6f + (float)_random.NextDouble() * 10f;
                StampBloodDecal(new BloodDecal(particle.Position, size, particle.Color));
                _bloodParticles.RemoveAt(i);
            }
        }
    }

    private void CheckRoundEnd()
    {
        if (_players.Count < 2)
            return;

        Player lastAlive = null;
        int aliveCount = 0;
        foreach (var player in _players)
        {
            if (player.Stickman.IsAlive)
            {
                aliveCount++;
                lastAlive = player;
            }
        }

        if (aliveCount == 1)
        {
            _roundPhase = RoundPhase.RoundOver;
            _roundOverTimer = RoundOverDuration;
            _announcementText = "WINNER!";
            _announcementColor = lastAlive.Color;
            _roundWinner = lastAlive;
            lastAlive.Wins++;
            PlayWinSound();
        }
        else if (aliveCount == 0)
        {
            _roundPhase = RoundPhase.RoundOver;
            _roundOverTimer = RoundOverDuration;
            _announcementText = "DRAW!";
            _announcementColor = Color.White;
            _roundWinner = null;
        }
    }

    private const int MaxCardChoices = 6;

    // Everyone but the winner picks 1-of-N random permanent upgrades before the next round starts -
    // N is normally 3, but stacking Keen Eye raises it (capped) for whoever picked it.
    private void BeginCardSelection(Player winner)
    {
        _cardSelections.Clear();
        foreach (var player in _players)
        {
            if (player == winner)
                continue;

            // The further behind the winner's win count a player is, the better their loot odds -
            // see Upgrades.RollRarity - so a losing player has a real shot at catching back up.
            int winsBehind = Math.Max(0, winner.Wins - player.Wins);
            int choiceCount = Math.Min(MaxCardChoices, 3 + player.Stickman.EffectiveBonusCardChoices);
            _cardSelections[player] = new CardSelectionState { Options = PickRandomUpgrades(choiceCount, winsBehind) };
        }

        if (_cardSelections.Count == 0)
        {
            StartNewRound();
            return;
        }

        _roundPhase = RoundPhase.CardSelection;
    }

    // Any slot that happens to roll Legendary has a small chance of being a Mega Curse instead of a
    // normal upgrade - keeps them rare and always-Legendary without touching the normal pool/rarity
    // scaling (see Upgrades.RollMegaCurse). Needs at least one other player to have a "leader" to hex.
    private const double MegaCurseChance = 0.2;

    private Card[] PickRandomUpgrades(int count, int winsBehind)
    {
        var pool = new List<UpgradeType>(Upgrades.AllTypes);
        var picked = new Card[count];

        for (int i = 0; i < count; i++)
        {
            var rarity = Upgrades.RollRarity(_random, winsBehind);

            if (rarity == Rarity.Legendary && _players.Count > 1 && _random.NextDouble() < MegaCurseChance)
            {
                picked[i] = Upgrades.RollMegaCurse(_random);
                continue; // Mega Curses aren't drawn from `pool` at all - no slot to remove
            }

            // Curses only show up at Rare rarity or better, so they stop cluttering every single
            // Common-tier pick - pick from a curse-free pool whenever this slot rolled Common.
            var candidates = rarity == Rarity.Common ? pool.FindAll(t => !Upgrades.IsCurse(t)) : pool;
            if (candidates.Count == 0)
                candidates = pool; // pool has been picked down to only curses - extremely unlikely, but fall back rather than crash

            int index = _random.Next(candidates.Count);
            var type = candidates[index];
            picked[i] = Upgrades.Roll(type, rarity, _random);
            pool.Remove(type);
        }

        return picked;
    }

    private void UpdateCardSelection(KeyboardState keyboard)
    {
        foreach (var kvp in _cardSelections)
        {
            var state = kvp.Value;
            if (state.Confirmed)
                continue;

            var input = kvp.Key.GatherInput(keyboard);

            bool pressedLeft = input.MoveDirection < -0.5f && state.PreviousMoveDirection >= -0.5f;
            bool pressedRight = input.MoveDirection > 0.5f && state.PreviousMoveDirection <= 0.5f;
            state.PreviousMoveDirection = input.MoveDirection;

            if (pressedLeft)
                state.SelectedIndex = Math.Max(0, state.SelectedIndex - 1);
            if (pressedRight)
                state.SelectedIndex = Math.Min(state.Options.Length - 1, state.SelectedIndex + 1);

            if (input.JumpPressed)
            {
                state.Confirmed = true;
                SoundBank.PlayRandom(_cardSelectSounds, _random, 0.4f);
            }
        }

        foreach (var state in _cardSelections.Values)
        {
            if (!state.Confirmed)
                return;
        }

        foreach (var kvp in _cardSelections)
        {
            var card = kvp.Value.Options[kvp.Value.SelectedIndex];

            // Mega Curses target the round leader specifically (resetting one of their stats),
            // rather than the picker or "every other player" like a normal curse.
            if (Upgrades.IsMegaCurse(card.Type))
            {
                Player leader = null;
                foreach (var candidate in _players)
                {
                    if (leader == null || candidate.Wins > leader.Wins)
                        leader = candidate;
                }

                leader?.Stickman.ResetUpgrade((UpgradeType)(int)card.Amount);
            }
            // Curse cards hex every other cow instead of buffing the picker - lets a player behind
            // in the match sabotage the field rather than only ever catching up on their own stats.
            else if (Upgrades.IsCurse(card.Type))
            {
                foreach (var other in _players)
                {
                    if (other != kvp.Key)
                        other.Stickman.ApplyUpgrade(card);
                }
            }
            else
            {
                kvp.Key.Stickman.ApplyUpgrade(card);
            }
        }

        StartNewRound();
    }

    private void StartNewRound()
    {
        _bullets.Clear();
        _explosions.Clear();
        GenerateLevel();

        foreach (var player in _players)
            player.Stickman.ResetForNewRound(RandomSpawnPosition());

        _roundPhase = RoundPhase.Fighting;
        _roundStartGraceTimer = RoundStartGraceDuration;
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(SkyTopColor);

        _spriteBatch.Begin();

        var viewport = GraphicsDevice.Viewport;
        _spriteBatch.Draw(_skyGradient, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.White);

        _spriteBatch.Draw(_terrainSurface, Vector2.Zero, Color.White);

        DrawMovingPlatforms();

        foreach (var box in _boxes)
            box.Draw(_spriteBatch, _pixel, _softCircle);

        foreach (var barrel in _barrels)
            barrel.Draw(_spriteBatch, _pixel, _softCircle);

        foreach (var player in _players)
            player.Stickman.Draw(_spriteBatch, _pixel, _softCircle, player.Color, UiScale);

        foreach (var particle in _bloodParticles)
            Primitives2D.DrawBlob(_spriteBatch, _softCircle, particle.Position, new Vector2(5f, 5f), particle.Color);

        foreach (var bullet in _bullets)
            bullet.Draw(_spriteBatch, _pixel, _softCircle);

        foreach (var explosion in _explosions)
            explosion.Draw(_spriteBatch, _softCircle);

        DrawHealthBars();
        DrawScoreboard();

        if (_roundPhase == RoundPhase.RoundOver)
            DrawAnnouncement();
        else if (_roundPhase == RoundPhase.CardSelection)
            DrawCardSelection();
        else if (_roundPhase == RoundPhase.Fighting && _roundStartGraceTimer > 0f)
            DrawRoundStartCountdown();

        DrawGunKitSelections();

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    // One small overlay panel per in-progress gun-kit picker (see UpdateGunKitSelections), stacked
    // top-center so multiple simultaneous joins don't overlap.
    private void DrawGunKitSelections()
    {
        if (_gunKitSelections.Count == 0)
            return;

        const float panelWidth = 380f;
        const float panelHeight = 90f;
        const float margin = 20f;
        const float spacing = 10f;
        const float titleScale = 0.4f;
        const float nameScale = 0.6f;
        const float hintScale = 0.32f;

        for (int i = 0; i < _gunKitSelections.Count; i++)
        {
            var state = _gunKitSelections[i];
            float top = margin + i * (panelHeight + spacing);
            Vector2 center = new Vector2(GraphicsDevice.Viewport.Width / 2f, top + panelHeight / 2f);

            Primitives2D.DrawRoundedRect(_spriteBatch, _pixel, _softCircle, center, new Vector2(panelWidth, panelHeight), 10f, state.Color);
            Primitives2D.DrawRoundedRect(_spriteBatch, _pixel, _softCircle, center, new Vector2(panelWidth - 6f, panelHeight - 6f), 8f, new Color(20, 20, 24));

            const string title = "CHOOSE YOUR GUN SOUND";
            Vector2 titleSize = _font.MeasureString(title) * titleScale;
            _spriteBatch.DrawString(_font, title, new Vector2(center.X - titleSize.X / 2f, top + 8f), state.Color, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);

            string kitName = _gunKitNames.Length > 0 ? $"< {_gunKitNames[state.SelectedKitIndex % _gunKitNames.Length]} >" : "< NO KITS FOUND >";
            Vector2 nameSize = _font.MeasureString(kitName) * nameScale;
            _spriteBatch.DrawString(_font, kitName, new Vector2(center.X - nameSize.X / 2f, top + 30f), Color.White, 0f, Vector2.Zero, nameScale, SpriteEffects.None, 0f);

            string hint = state.UsesKeyboard ? "<-/-> change   SPACE confirm" : "D-PAD change   A confirm";
            Vector2 hintSize = _font.MeasureString(hint) * hintScale;
            _spriteBatch.DrawString(_font, hint, new Vector2(center.X - hintSize.X / 2f, top + panelHeight - 22f), new Color(190, 190, 195), 0f, Vector2.Zero, hintScale, SpriteEffects.None, 0f);
        }
    }

    private void DrawCardSelection()
    {
        int count = _cardSelections.Count;
        if (count == 0)
            return;

        float bandHeight = GraphicsDevice.Viewport.Height / (float)count;
        int index = 0;

        foreach (var kvp in _cardSelections)
        {
            DrawPlayerCards(kvp.Key, kvp.Value, index * bandHeight, bandHeight);
            index++;
        }
    }

    // A compact readout of a player's current effective stats, shown above their cards during card
    // selection so it's clear what an upgrade would actually be building on. Core stats always show;
    // stats from one-off upgrades (explosive rounds, knockback, poison, homing, ricochet, buckshot)
    // only show once the player actually has them, to avoid cluttering the line with a wall of zeros.
    // Wrapped to fit maxWidth - individual stats are never split across lines - instead of one
    // ever-widening line, since enough stats/upgrades will otherwise run off both sides of the screen.
    private List<string> BuildWrappedPlayerStats(Player player, float maxWidth, float scale)
    {
        var s = player.Stickman;
        var stats = new List<string>
        {
            $"HP {s.EffectiveMaxHealth:0}",
            $"DMG {s.EffectiveDamage:0.#}",
            $"MAG {s.EffectiveMagazineSize}",
            $"SPD {s.EffectiveMoveSpeed:0}",
            $"JUMP {s.EffectiveJumpSpeed:0}",
            $"SIZE {s.BulletRadiusMultiplier * 100f:0}%",
            $"BSPD {s.BulletSpeedMultiplier * 100f:0}%",
            $"GRAV {s.BulletGravityMultiplier * 100f:0}%",
            $"BLOCK {s.EffectiveMaxBlockStamina:0.#}s",
            $"RATE {1f / s.EffectiveFireCooldown:0.#}/s",
            $"RELOAD {s.EffectiveReloadDuration:0.##}s",
            $"POUND {s.EffectiveGroundPoundDamage:0}dmg/{s.EffectiveGroundPoundRadius:0}r"
        };

        if (s.EffectiveBulletCount > 1)
            stats.Add($"PELLETS x{s.EffectiveBulletCount}");
        if (s.EffectiveKnockbackMultiplier > 1.001f)
            stats.Add($"KNOCK +{(s.EffectiveKnockbackMultiplier - 1f) * 100f:0}%");
        if (s.EffectiveExplosionRadius > 0f)
            stats.Add($"BLAST {s.EffectiveExplosionRadius:0}");
        if (s.EffectivePoisonDamagePerSecond > 0f)
            stats.Add($"POISON {s.EffectivePoisonDamagePerSecond:0.#}/s");
        if (s.EffectiveHomingTurnRate > 0f)
            stats.Add($"HOMING {MathHelper.ToDegrees(s.EffectiveHomingTurnRate):0}deg/s");
        if (s.EffectiveBounceCount > 0)
            stats.Add($"BOUNCE x{s.EffectiveBounceCount}");
        if (s.EffectiveHealthRegenPerSecond > 0f)
            stats.Add($"REGEN {s.EffectiveHealthRegenPerSecond:0.#}/s");
        if (s.EffectiveExtraLives > 0)
            stats.Add($"LIVES +{s.EffectiveExtraLives}");
        if (s.EffectiveExtraJumps > 0)
            stats.Add($"JUMPS +{s.EffectiveExtraJumps}");
        if (s.EffectiveAoERadius > 0f)
            stats.Add($"AURA {s.EffectiveAoERadius:0}r/{s.EffectiveAoEDamagePerSecond:0.#}dps");
        if (s.EffectiveLifeStealFraction > 0f)
            stats.Add($"LIFESTEAL {s.EffectiveLifeStealFraction * 100f:0}%");

        var lines = new List<string>();
        string current = "";
        foreach (var stat in stats)
        {
            string candidate = current.Length == 0 ? stat : current + "   " + stat;
            float width = _font.MeasureString(candidate).X * scale;
            if (width > maxWidth && current.Length > 0)
            {
                lines.Add(current);
                current = stat;
            }
            else
            {
                current = candidate;
            }
        }
        if (current.Length > 0)
            lines.Add(current);

        return lines;
    }

    // Draws each already-wrapped line centered horizontally, stacked downward from top.
    private void DrawCenteredTextLines(List<string> lines, float top, float scale, float lineHeight, Color color)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            Vector2 size = _font.MeasureString(lines[i]) * scale;
            Vector2 position = new Vector2(GraphicsDevice.Viewport.Width / 2f - size.X / 2f, top + i * lineHeight);
            _spriteBatch.DrawString(_font, lines[i], position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }

    private void DrawPlayerCards(Player player, CardSelectionState state, float bandTop, float bandHeight)
    {
        float uiScale = UiScale;
        const float cardSpacing = 30f;
        const float sideMargin = 60f;
        const float heightToWidthRatio = 1.3f; // portrait trading-card proportions
        float statsScale = 0.4f * uiScale;
        float titleScale = 0.75f * uiScale;

        int cardCount = state.Options.Length;

        // Wrap the stats block first (never split a single stat across lines) so promptReserve
        // below can size itself to however many lines it actually took, instead of a fixed guess
        // that used to run off both sides of the screen once enough stats/upgrades piled up.
        float statsMaxWidth = GraphicsDevice.Viewport.Width - sideMargin * 2f;
        var statsLines = BuildWrappedPlayerStats(player, statsMaxWidth, statsScale);
        float statsLineHeight = _font.MeasureString("A").Y * statsScale + 4f * uiScale;
        float statsBlockHeight = statsLines.Count * statsLineHeight;

        const string prompt = "CHOOSE AN UPGRADE";
        Vector2 promptSize = _font.MeasureString(prompt) * titleScale;

        // Vertical room for the title line plus the (variable-height) stats block above the cards.
        float promptReserve = promptSize.Y + 10f + statsBlockHeight + 20f;

        // Cards scale up to fill the available space - generous when only one player is picking,
        // shrinking automatically if several players are choosing at once and share the screen, or
        // if there are simply more cards to fit (Keen Eye can offer up to 6). No lower floor on
        // cardWidth - forcing a minimum regardless of available space is what pushed cards off the
        // sides of the screen; a small sanity floor guards only against degenerate near-zero sizes.
        // CardSizeScale then shrinks the result further - cards were still too big at 3+ players,
        // squeezing out the room the stats block above them needs.
        const float CardSizeScale = 2f / 3f;
        float maxWidthFromScreen = (GraphicsDevice.Viewport.Width - sideMargin * 2f - cardSpacing * (cardCount - 1)) / cardCount;
        float maxHeightFromBand = MathHelper.Max(120f, bandHeight - promptReserve - 20f);
        float cardWidth = MathHelper.Max(60f, MathHelper.Min(MathHelper.Min(maxWidthFromScreen, maxHeightFromBand / heightToWidthRatio), 320f * uiScale)) * CardSizeScale;
        float cardHeight = cardWidth * heightToWidthRatio;

        float cardCornerRadius = MathHelper.Clamp(cardWidth * 0.06f, 6f, 20f * uiScale);
        float scale = cardWidth / 240f; // relative to the original, smaller card size
        float nameScale = MathHelper.Clamp(0.55f * scale, 0.3f, 0.85f * uiScale);
        float descriptionScale = MathHelper.Clamp(0.44f * scale, 0.26f, 0.62f * uiScale);
        float lineHeight = 21f * MathHelper.Clamp(scale, 0.6f, 1.3f * uiScale);
        float padding = 16f * scale;

        float headerHeight = cardHeight * 0.13f;
        float artHeight = cardHeight * 0.4f;

        float totalWidth = cardCount * cardWidth + (cardCount - 1) * cardSpacing;
        float startX = GraphicsDevice.Viewport.Width / 2f - totalWidth / 2f;

        // Center the WHOLE block (stats + title + cards) within this player's band, rather than
        // just centering the cards and letting the header spill upward - that was the actual bug:
        // for the topmost band (bandTop = 0) there's nowhere for it to spill to, so the stats/title
        // ran off the top of the screen entirely instead of just looking cramped.
        float totalBlockHeight = promptReserve + cardHeight;
        float blockTop = bandTop + MathHelper.Max(0f, (bandHeight - totalBlockHeight) / 2f);
        float cardTop = blockTop + promptReserve;

        float promptY = cardTop - promptSize.Y - 10f;
        _spriteBatch.DrawString(
            _font, prompt,
            new Vector2(GraphicsDevice.Viewport.Width / 2f - promptSize.X / 2f, promptY),
            player.Color, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);

        float statsTop = promptY - 10f - statsBlockHeight;
        DrawCenteredTextLines(statsLines, statsTop, statsScale, statsLineHeight, Color.Lerp(player.Color, Color.White, 0.4f));

        for (int i = 0; i < cardCount; i++)
        {
            var card = state.Options[i];
            Color rarityColor = Upgrades.GetRarityColor(card.Rarity);

            float cardX = startX + i * (cardWidth + cardSpacing);
            var bounds = new Rectangle((int)cardX, (int)cardTop, (int)cardWidth, (int)cardHeight);
            bool selected = i == state.SelectedIndex;
            Vector2 cardCenter = new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);

            // Unselected cards show their rarity tier as the border color; the selected card
            // switches to the player's own color so the current pick still reads clearly.
            Color border = selected ? player.Color : Color.Lerp(rarityColor, Color.Black, 0.25f);
            float thickness = selected ? 5f : 2f;

            // An outer rounded-rect ring in the border color, with the fill inset just inside it,
            // instead of four separate straight border strips with hard corners.
            Primitives2D.DrawRoundedRect(_spriteBatch, _pixel, _softCircle, cardCenter, new Vector2(bounds.Width, bounds.Height), cardCornerRadius, border);
            Primitives2D.DrawRoundedRect(_spriteBatch, _pixel, _softCircle, cardCenter, new Vector2(bounds.Width - thickness * 2f, bounds.Height - thickness * 2f), cardCornerRadius - thickness, new Color(22, 22, 26));

            float headerRadius = MathHelper.Min(cardCornerRadius - thickness, headerHeight * 0.5f);
            Vector2 headerCenter = new Vector2(cardCenter.X, bounds.Y + thickness + headerHeight / 2f);
            bool isCurse = Upgrades.IsCurse(card.Type) || Upgrades.IsMegaCurse(card.Type);
            Color headerColor = isCurse
                ? new Color(120, 30, 130)
                : selected ? player.Color : Color.Lerp(player.Color, Color.Black, 0.55f);
            Vector2 headerSize = new Vector2(bounds.Width - thickness * 2f, headerHeight);

            // Rounded top corners (matching the card), squared-off bottom edge (an internal seam).
            Primitives2D.DrawRoundedRect(_spriteBatch, _pixel, _softCircle, headerCenter, headerSize, headerRadius, headerColor);
            Primitives2D.DrawRect(_spriteBatch, _pixel, new Vector2(headerCenter.X, headerCenter.Y + headerSize.Y / 2f - headerRadius / 2f), new Vector2(headerSize.X, headerRadius), headerColor);

            string name = Upgrades.GetName(card.Type);
            Vector2 nameSize = _font.MeasureString(name) * nameScale;
            _spriteBatch.DrawString(
                _font, name,
                new Vector2(cardCenter.X - nameSize.X / 2f, headerCenter.Y - nameSize.Y / 2f),
                Color.Black, 0f, Vector2.Zero, nameScale, SpriteEffects.None, 0f);

            // Art panel: a tinted backdrop behind a procedurally-drawn icon for this upgrade.
            float artTop = headerCenter.Y + headerSize.Y / 2f;
            Vector2 artCenter = new Vector2(cardCenter.X, artTop + artHeight / 2f);
            Primitives2D.DrawRoundedRect(
                _spriteBatch, _pixel, _softCircle, artCenter,
                new Vector2(bounds.Width - thickness * 2f - padding, artHeight - 8f),
                cardCornerRadius * 0.6f, Color.Lerp(player.Color, Color.Black, 0.8f));
            Upgrades.DrawIcon(_spriteBatch, _pixel, _softCircle, card.Type, artCenter, artHeight * 0.32f, player.Color);

            string description = $"{Upgrades.GetRarityLabel(card.Rarity)} - {Upgrades.GetDescription(card)}";
            float maxTextWidth = bounds.Width - padding * 2f;
            var lines = WrapText(description, maxTextWidth, descriptionScale);
            Color descriptionColor = Color.Lerp(rarityColor, Color.White, 0.35f);

            // Center the description vertically in whatever room is left below the art panel.
            float descriptionAreaTop = artTop + artHeight + 10f;
            float descriptionAreaHeight = (bounds.Y + bounds.Height - thickness) - descriptionAreaTop;
            float textY = descriptionAreaTop + MathHelper.Max(0f, (descriptionAreaHeight - lines.Count * lineHeight) / 2f);
            foreach (var line in lines)
            {
                Vector2 lineSize = _font.MeasureString(line) * descriptionScale;
                _spriteBatch.DrawString(
                    _font, line,
                    new Vector2(cardCenter.X - lineSize.X / 2f, textY),
                    descriptionColor, 0f, Vector2.Zero, descriptionScale, SpriteEffects.None, 0f);
                textY += lineHeight;
            }
        }
    }

    private List<string> WrapText(string text, float maxWidth, float scale)
    {
        var words = text.Split(' ');
        var lines = new List<string>();
        string current = "";

        foreach (var word in words)
        {
            string candidate = current.Length == 0 ? word : current + " " + word;
            float width = _font.MeasureString(candidate).X * scale;
            if (width > maxWidth && current.Length > 0)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = candidate;
            }
        }

        if (current.Length > 0)
            lines.Add(current);

        return lines;
    }

    private void DrawTerrain()
    {
        int width = GraphicsDevice.Viewport.Width;
        int height = GraphicsDevice.Viewport.Height;

        for (float x = 0; x < width; x += TerrainColumnWidth)
        {
            float surfaceY = _level.GetGroundHeightAt(x + TerrainColumnWidth * 0.5f);
            int top = (int)surfaceY;
            if (top < height)
            {
                _spriteBatch.Draw(_pixel, new Rectangle((int)x, top, (int)TerrainColumnWidth + 1, height - top), TerrainColor);

                int grassBand = (int)MathF.Min(GrassDepth, height - top);
                _spriteBatch.Draw(_pixel, new Rectangle((int)x, top, (int)TerrainColumnWidth + 1, grassBand), GrassColor);
                _spriteBatch.Draw(_pixel, new Rectangle((int)x, top, (int)TerrainColumnWidth + 1, 3), GrassHighlightColor);
            }
        }
    }

    // A farm backdrop behind the terrain, at night - a moon, stars, dim moonlit clouds, rolling
    // silhouetted hills, a barn with a lit window, and a fence line along the horizon - re-rolled
    // each round alongside the rest of the level. Keeping the sky dark (rather than a full daytime
    // scene) is deliberate: it's what makes neon player colors and bullet glows read clearly.
    private void DrawFarmBackground()
    {
        int width = GraphicsDevice.Viewport.Width;
        float horizonY = GraphicsDevice.Viewport.Height - GroundMargin;

        DrawStars(horizonY);

        Vector2 moonCenter = new Vector2(width * 0.85f, horizonY * 0.18f);
        Primitives2D.DrawCircle(_spriteBatch, _softCircle, moonCenter, 130f, MoonGlowColor * 0.35f);
        Primitives2D.DrawCircle(_spriteBatch, _softCircle, moonCenter, 70f, MoonColor);
        Primitives2D.DrawCircle(_spriteBatch, _softCircle, moonCenter + new Vector2(-18f, 10f), 16f, MoonCraterColor);
        Primitives2D.DrawCircle(_spriteBatch, _softCircle, moonCenter + new Vector2(16f, -12f), 11f, MoonCraterColor);
        Primitives2D.DrawCircle(_spriteBatch, _softCircle, moonCenter + new Vector2(4f, 20f), 9f, MoonCraterColor);

        int cloudCount = _random.Next(5, 9);
        for (int i = 0; i < cloudCount; i++)
        {
            float cx = (float)_random.NextDouble() * width;
            float cy = horizonY * (0.12f + (float)_random.NextDouble() * 0.35f);
            float scale = 0.7f + (float)_random.NextDouble() * 0.8f;
            DrawCloud(new Vector2(cx, cy), scale);
        }

        DrawHillRow(horizonY - 20f, FarHillColor, 3);
        DrawHillRow(horizonY + 10f, NearHillColor, 4);

        DrawBarn(new Vector2(width * 0.12f, horizonY + 6f));
        DrawFenceLine(horizonY - 6f);
    }

    private void DrawStars(float horizonY)
    {
        int width = GraphicsDevice.Viewport.Width;
        int starCount = _random.Next(50, 90);

        for (int i = 0; i < starCount; i++)
        {
            float sx = (float)_random.NextDouble() * width;
            float sy = (float)_random.NextDouble() * horizonY * 0.85f;
            float starSize = 1.5f + (float)_random.NextDouble() * 2.5f;
            float twinkle = 0.35f + (float)_random.NextDouble() * 0.65f;
            Primitives2D.DrawCircle(_spriteBatch, _softCircle, new Vector2(sx, sy), starSize, StarColor * twinkle);
        }
    }

    private void DrawCloud(Vector2 center, float scale)
    {
        Color color = CloudColor * 0.5f;
        Primitives2D.DrawBlob(_spriteBatch, _softCircle, center, new Vector2(90f, 34f) * scale, color);
        Primitives2D.DrawBlob(_spriteBatch, _softCircle, center + new Vector2(-40f, 6f) * scale, new Vector2(55f, 30f) * scale, color);
        Primitives2D.DrawBlob(_spriteBatch, _softCircle, center + new Vector2(45f, 4f) * scale, new Vector2(60f, 32f) * scale, color);
        Primitives2D.DrawBlob(_spriteBatch, _softCircle, center + new Vector2(10f, -18f) * scale, new Vector2(50f, 40f) * scale, color);
    }

    // A row of soft, overlapping hill mounds straddling the horizon line - half hidden below it once
    // DrawTerrain paints the actual ground over the lower half, leaving just a rolling silhouette.
    private void DrawHillRow(float baseY, Color color, int bumpCount)
    {
        int width = GraphicsDevice.Viewport.Width;
        float bumpWidth = width / (float)bumpCount * 1.5f;

        for (int i = 0; i < bumpCount; i++)
        {
            float cx = width * (i + 0.5f) / bumpCount + ((float)_random.NextDouble() - 0.5f) * bumpWidth * 0.25f;
            float bumpHeight = 90f + (float)_random.NextDouble() * 70f;
            Primitives2D.DrawBlob(_spriteBatch, _softCircle, new Vector2(cx, baseY), new Vector2(bumpWidth, bumpHeight), color);
        }
    }

    private void DrawBarn(Vector2 groundAnchor)
    {
        const float barnWidth = 150f;
        const float barnHeight = 110f;
        const float roofHeight = 55f;

        Vector2 bodyCenter = groundAnchor + new Vector2(0f, -barnHeight / 2f);
        Primitives2D.DrawRoundedRect(_spriteBatch, _pixel, _softCircle, bodyCenter, new Vector2(barnWidth, barnHeight), 6f, BarnColor);

        Vector2 apex = groundAnchor + new Vector2(0f, -barnHeight - roofHeight);
        Vector2 left = groundAnchor + new Vector2(-barnWidth * 0.62f, -barnHeight);
        Vector2 right = groundAnchor + new Vector2(barnWidth * 0.62f, -barnHeight);
        Primitives2D.DrawCapsule(_spriteBatch, _pixel, _softCircle, left, apex, 22f, BarnRoofColor);
        Primitives2D.DrawCapsule(_spriteBatch, _pixel, _softCircle, apex, right, 22f, BarnRoofColor);

        Vector2 doorCenter = bodyCenter + new Vector2(0f, barnHeight * 0.22f);
        Primitives2D.DrawRoundedRect(_spriteBatch, _pixel, _softCircle, doorCenter, new Vector2(36f, 60f), 4f, BarnTrimColor);
        Primitives2D.DrawCapsule(_spriteBatch, _pixel, _softCircle, doorCenter + new Vector2(-16f, -28f), doorCenter + new Vector2(16f, 28f), 3f, BarnColor);
        Primitives2D.DrawCapsule(_spriteBatch, _pixel, _softCircle, doorCenter + new Vector2(16f, -28f), doorCenter + new Vector2(-16f, 28f), 3f, BarnColor);

        // A lit hayloft window - a warm glow against the otherwise dark, silhouetted farm at night.
        Vector2 windowCenter = bodyCenter + new Vector2(0f, -barnHeight * 0.28f);
        Primitives2D.DrawCircle(_spriteBatch, _softCircle, windowCenter, 60f, BarnWindowGlowColor * 0.3f);
        Primitives2D.DrawRoundedRect(_spriteBatch, _pixel, _softCircle, windowCenter, new Vector2(26f, 26f), 13f, BarnWindowGlowColor);
    }

    private void DrawFenceLine(float y)
    {
        int width = GraphicsDevice.Viewport.Width;
        const float postSpacing = 90f;
        const float postHeight = 34f;
        const float postWidth = 8f;
        const float railHeight = 6f;

        for (float x = postSpacing * 0.5f; x < width; x += postSpacing)
            Primitives2D.DrawRoundedRect(_spriteBatch, _pixel, _softCircle, new Vector2(x, y - postHeight / 2f), new Vector2(postWidth, postHeight), 3f, FenceColor);

        Primitives2D.DrawRoundedRect(_spriteBatch, _pixel, _softCircle, new Vector2(width / 2f, y - postHeight * 0.7f), new Vector2(width, railHeight), 3f, FenceColor);
        Primitives2D.DrawRoundedRect(_spriteBatch, _pixel, _softCircle, new Vector2(width / 2f, y - postHeight * 0.25f), new Vector2(width, railHeight), 3f, FenceColor);
    }

    private void DrawPlatforms()
    {
        foreach (var platform in _level.Platforms)
            DrawPlatformSpan(platform.StartX, platform.EndX, platform.Height, platform.Thickness, PlatformColor, PlatformEdgeColor);
    }

    // Unlike static platforms/terrain, these move every frame, so they can't be baked into the
    // terrain surface texture - they're redrawn here each frame instead, same as boxes/bullets.
    private void DrawMovingPlatforms()
    {
        foreach (var platform in _level.MovingPlatforms)
            DrawPlatformSpan(platform.StartX, platform.EndX, platform.Height, platform.Thickness, MovingPlatformColor, MovingPlatformEdgeColor);
    }

    private void DrawPlatformSpan(float startX, float endX, float height, float thickness, Color fillColor, Color edgeColor)
    {
        var bounds = new Rectangle((int)startX, (int)height, (int)(endX - startX), (int)thickness);

        Vector2 center = new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
        Primitives2D.DrawRoundedRect(_spriteBatch, _pixel, _softCircle, center, new Vector2(bounds.Width, bounds.Height), PlatformCornerRadius, fillColor);

        Vector2 highlightCenter = new Vector2(center.X, bounds.Y + 2f);
        Primitives2D.DrawRoundedRect(_spriteBatch, _pixel, _softCircle, highlightCenter, new Vector2(MathF.Max(0f, bounds.Width - PlatformCornerRadius), 3f), 1.5f, edgeColor);
    }

    private void DrawWalls()
    {
        foreach (var wall in _level.Walls)
        {
            var bounds = wall.GetBounds();
            Vector2 center = new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
            Primitives2D.DrawRoundedRect(_spriteBatch, _pixel, _softCircle, center, new Vector2(bounds.Width, bounds.Height), WallCornerRadius, WallColor);

            float edgeHeight = MathF.Max(0f, bounds.Height - WallCornerRadius);
            Primitives2D.DrawRoundedRect(_spriteBatch, _pixel, _softCircle, new Vector2(bounds.X + 2f, center.Y), new Vector2(3f, edgeHeight), 1.5f, WallEdgeColor);
            Primitives2D.DrawRoundedRect(_spriteBatch, _pixel, _softCircle, new Vector2(bounds.Right - 2f, center.Y), new Vector2(3f, edgeHeight), 1.5f, WallEdgeColor);
        }
    }

    // HUD/UI elements (health bars, scoreboard, card-select panels, stats line, ammo HUD) scale up
    // on higher resolutions so they aren't tiny on 4K - never below 1x, so today's 1080p-and-under
    // look is unchanged. Gameplay-world entities (player/bullet/platform sizes) deliberately don't
    // scale with this, to keep gameplay feel/balance identical across resolutions.
    private float UiScale => MathHelper.Max(1f, GraphicsDevice.Viewport.Height / 1080f);

    private void DrawHealthBars()
    {
        const float cornerRadius = 6f;
        var trackColor = new Color(40, 40, 48);
        float uiScale = UiScale;
        float barWidth = HealthBarWidth * uiScale;
        float barHeight = HealthBarHeight * uiScale;
        float barMargin = HealthBarMargin * uiScale;

        for (int i = 0; i < _players.Count; i++)
        {
            var player = _players[i];
            float x = barMargin + i * (barWidth + barMargin);
            float y = barMargin;

            Vector2 trackCenter = new Vector2(x + barWidth / 2f, y + barHeight / 2f);
            Primitives2D.DrawRoundedRect(_spriteBatch, _pixel, _softCircle, trackCenter, new Vector2(barWidth, barHeight), cornerRadius * uiScale, trackColor);

            float fillWidth = barWidth * player.Stickman.HealthFraction;
            if (fillWidth > 1f)
            {
                Vector2 fillCenter = new Vector2(x + fillWidth / 2f, y + barHeight / 2f);
                Primitives2D.DrawRoundedRect(_spriteBatch, _pixel, _softCircle, fillCenter, new Vector2(fillWidth, barHeight), cornerRadius * uiScale, player.Color);
            }

            // Spare Second Wind (Extra Life) charges still available this round, as a row of small
            // pips under the bar - only shown once a player has actually picked the card.
            if (player.Stickman.EffectiveExtraLives > 0)
            {
                float pipRadius = 5f * uiScale;
                float pipSpacing = 14f * uiScale;
                float pipY = y + barHeight + 10f * uiScale;

                for (int lifeIndex = 0; lifeIndex < player.Stickman.RemainingExtraLives; lifeIndex++)
                {
                    float pipX = x + pipRadius + lifeIndex * pipSpacing;
                    Primitives2D.DrawCircle(_spriteBatch, _softCircle, new Vector2(pipX, pipY), pipRadius, player.Color);
                }
            }
        }
    }

    // Round wins per player, stacked in the top-right corner.
    private void DrawScoreboard()
    {
        float uiScale = UiScale;
        float swatchSize = 18f * uiScale;
        float rowHeight = 26f * uiScale;
        float margin = 20f * uiScale;
        float scale = 0.55f * uiScale;

        for (int i = 0; i < _players.Count; i++)
        {
            var player = _players[i];
            float y = margin + i * rowHeight;
            float swatchX = GraphicsDevice.Viewport.Width - margin - swatchSize;

            Vector2 swatchCenter = new Vector2(swatchX + swatchSize / 2f, y + swatchSize / 2f);
            Primitives2D.DrawRoundedRect(_spriteBatch, _pixel, _softCircle, swatchCenter, new Vector2(swatchSize, swatchSize), 5f * uiScale, player.Color);

            string text = player.Wins.ToString();
            Vector2 textSize = _font.MeasureString(text) * scale;
            _spriteBatch.DrawString(
                _font, text,
                new Vector2(swatchX - 8f * uiScale - textSize.X, y + swatchSize / 2f - textSize.Y / 2f),
                Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }

    private void DrawRoundStartCountdown()
    {
        int secondsLeft = (int)MathF.Ceiling(_roundStartGraceTimer);
        string text = $"RUN! WEAPONS LIVE IN {secondsLeft}";
        float scale = 0.6f * UiScale;
        Vector2 size = _font.MeasureString(text) * scale;
        Vector2 position = new Vector2(GraphicsDevice.Viewport.Width / 2f - size.X / 2f, 60f * UiScale);
        _spriteBatch.DrawString(_font, text, position, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawAnnouncement()
    {
        float scale = UiScale;
        Vector2 size = _font.MeasureString(_announcementText) * scale;
        Vector2 position = new Vector2(
            GraphicsDevice.Viewport.Width / 2f - size.X / 2f,
            GraphicsDevice.Viewport.Height / 2f - size.Y / 2f);

        _spriteBatch.DrawString(_font, _announcementText, position, _announcementColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    // Drops a fighter at a random X and lands them on whatever surface is directly below - ground or a tier.
    private Vector2 RandomSpawnPosition()
    {
        float width = _arenaWidth;
        Vector2 best = default;
        float bestSeparation = -1f;

        for (int attempt = 0; attempt < 10; attempt++)
        {
            float x = MathHelper.Lerp(SpawnMargin, width - SpawnMargin, (float)_random.NextDouble());
            var candidate = new Vector2(x, DropToSurfaceHeight(x));

            float separation = float.MaxValue;
            foreach (var player in _players)
                separation = MathF.Min(separation, Vector2.Distance(candidate, player.Stickman.Position));

            if (_players.Count == 0 || separation >= MinSpawnSeparation)
                return candidate;

            if (separation > bestSeparation)
            {
                bestSeparation = separation;
                best = candidate;
            }
        }

        return best;
    }
}
