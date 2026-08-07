using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace FightingGame;

public class Game1 : Game
{
    private enum RoundPhase { Fighting, RoundOver }

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

    private static readonly Color TerrainColor = new Color(70, 50, 35);
    private static readonly Color PlatformColor = new Color(95, 95, 105);
    private static readonly Color PlatformEdgeColor = new Color(140, 140, 150);
    private static readonly Color WallColor = new Color(60, 60, 68);
    private static readonly Color WallEdgeColor = new Color(100, 100, 112);

    private const float GroundMargin = 100f;
    private const float HealthBarWidth = 200f;
    private const float HealthBarHeight = 18f;
    private const float HealthBarMargin = 20f;
    private const float RoundOverDuration = 3f;
    private const float TerrainColumnWidth = 6f;
    private const int MinBoxCount = 3;
    private const int MaxBoxCount = 6;
    private const float BoxBulletImpulse = 220f;
    private const float BoxPlayerPushSpeed = 140f;
    private const int BloodParticlesPerHit = 8;
    private const float SpawnMargin = 60f;
    private const float MinSpawnSeparation = 150f;

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private Texture2D _pixel;
    private SpriteFont _font;
    private RenderTarget2D _terrainSurface;
    private readonly Random _random = new();
    private readonly List<Player> _players = new();
    private readonly List<Bullet> _bullets = new();
    private readonly List<Box> _boxes = new();
    private readonly List<BloodParticle> _bloodParticles = new();

    private Level _level;
    private RoundPhase _roundPhase = RoundPhase.Fighting;
    private float _roundOverTimer;
    private string _announcementText = "";
    private Color _announcementColor = Color.White;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
    }

    protected override void Initialize()
    {
        var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        _graphics.PreferredBackBufferWidth = displayMode.Width;
        _graphics.PreferredBackBufferHeight = displayMode.Height;
        _graphics.IsFullScreen = true;
        _graphics.ApplyChanges();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _font = Content.Load<SpriteFont>("GameFont");

        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        GenerateLevel();
    }

    private void GenerateLevel()
    {
        float width = GraphicsDevice.Viewport.Width;
        float baseHeight = GraphicsDevice.Viewport.Height - GroundMargin;
        _level = new Level(baseHeight, width, _random);

        _boxes.Clear();
        _bloodParticles.Clear();

        int boxCount = _random.Next(MinBoxCount, MaxBoxCount + 1);
        for (int i = 0; i < boxCount; i++)
        {
            float x = MathHelper.Lerp(80f, width - 80f, (float)_random.NextDouble());
            _boxes.Add(new Box(new Vector2(x, DropToSurfaceHeight(x))));
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
        _spriteBatch.Draw(
            _pixel,
            decal.Position,
            null,
            decal.Color,
            0f,
            new Vector2(0.5f, 0.5f),
            new Vector2(decal.Size, decal.Size),
            SpriteEffects.None,
            0f);
        _spriteBatch.End();

        GraphicsDevice.SetRenderTarget(null);
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();

        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboard.IsKeyDown(Keys.Escape))
            Exit();

        HandleJoins(keyboard);

        float minX = 40f;
        float maxX = GraphicsDevice.Viewport.Width - 40f;
        float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var playBounds = new Rectangle(-100, -100, GraphicsDevice.Viewport.Width + 200, GraphicsDevice.Viewport.Height + 200);

        if (_roundPhase == RoundPhase.RoundOver)
        {
            // Keep updating everything (with no player input) so a death mid-round-end still finishes its ragdoll fall.
            foreach (var player in _players)
                player.Stickman.Update(gameTime, default, minX, maxX, _level);

            foreach (var box in _boxes)
                box.Update(delta, _level, minX, maxX);

            UpdateBullets(delta, playBounds);
            ResolveBulletCollisions();
            ResolveBulletHits();
            UpdateBlood(delta);

            _roundOverTimer -= delta;
            if (_roundOverTimer <= 0f)
                StartNewRound();

            base.Update(gameTime);
            return;
        }

        var inputs = new PlayerInput[_players.Count];
        for (int i = 0; i < _players.Count; i++)
            inputs[i] = _players[i].GatherInput(keyboard);

        for (int i = 0; i < _players.Count; i++)
        {
            var stickman = _players[i].Stickman;
            stickman.Update(gameTime, inputs[i], minX, maxX, _level);

            if (stickman.FiredThisFrame)
                _bullets.Add(new Bullet(stickman.MuzzlePosition, stickman.AimDirection, _players[i].Color, stickman));
        }

        ResolvePlayerBoxCollisions(minX, maxX);

        foreach (var box in _boxes)
            box.Update(delta, _level, minX, maxX);

        UpdateBullets(delta, playBounds);
        ResolveBulletCollisions();
        ResolveBulletHits();
        UpdateBlood(delta);
        CheckRoundEnd();

        base.Update(gameTime);
    }

    private void HandleJoins(KeyboardState keyboard)
    {
        bool keyboardJoined = _players.Exists(p => p.UsesKeyboard);
        if (!keyboardJoined && keyboard.IsKeyDown(Keys.Space))
            _players.Add(new Player(null, usesKeyboard: true, PlayerColors[0], RandomSpawnPosition()));

        for (int slot = 0; slot < ControllerSlots.Length; slot++)
        {
            var controllerIndex = ControllerSlots[slot];
            bool alreadyJoined = _players.Exists(p => p.ControllerIndex == controllerIndex);
            if (alreadyJoined)
                continue;

            var padState = GamePad.GetState(controllerIndex);
            if (padState.IsConnected && padState.Buttons.A == ButtonState.Pressed)
                _players.Add(new Player(controllerIndex, usesKeyboard: false, PlayerColors[slot + 1], RandomSpawnPosition()));
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
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            _bullets[i].Update(delta, bounds, _level);
            if (!_bullets[i].IsAlive)
                _bullets.RemoveAt(i);
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

            foreach (var wall in _level.Walls)
            {
                if (bullet.GetBounds().Intersects(wall.GetBounds()))
                {
                    consumed = true;
                    break;
                }
            }

            if (!consumed)
            {
                foreach (var box in _boxes)
                {
                    if (bullet.GetBounds().Intersects(box.GetBounds()))
                    {
                        box.ApplyImpulse(MathF.Sign(bullet.Direction.X) * BoxBulletImpulse);
                        consumed = true;
                        break;
                    }
                }
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
                        bool blocked = player.Stickman.ApplyHit(attackerOnRight);
                        if (blocked)
                        {
                            bullet.Deflect(player.Stickman, player.Color);
                            break;
                        }

                        var hurtbox = player.Stickman.GetHurtbox();
                        SpawnBlood(new Vector2(hurtbox.Center.X, hurtbox.Center.Y), player.Color, bullet.Direction);
                        consumed = true;
                        break;
                    }
                }
            }

            if (consumed)
                _bullets.RemoveAt(i);
        }
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

    private void UpdateBlood(float delta)
    {
        for (int i = _bloodParticles.Count - 1; i >= 0; i--)
        {
            var particle = _bloodParticles[i];
            particle.Update(delta, _level);
            if (particle.Landed)
            {
                float size = 4f + (float)_random.NextDouble() * 5f;
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
        }
        else if (aliveCount == 0)
        {
            _roundPhase = RoundPhase.RoundOver;
            _roundOverTimer = RoundOverDuration;
            _announcementText = "DRAW!";
            _announcementColor = Color.White;
        }
    }

    private void StartNewRound()
    {
        _bullets.Clear();
        GenerateLevel();

        foreach (var player in _players)
            player.Stickman.ResetForNewRound(RandomSpawnPosition());

        _roundPhase = RoundPhase.Fighting;
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        _spriteBatch.Begin();

        _spriteBatch.Draw(_terrainSurface, Vector2.Zero, Color.White);

        foreach (var box in _boxes)
            box.Draw(_spriteBatch, _pixel);

        foreach (var player in _players)
            player.Stickman.Draw(_spriteBatch, _pixel, player.Color);

        foreach (var particle in _bloodParticles)
            _spriteBatch.Draw(_pixel, particle.Position, null, particle.Color, 0f, new Vector2(0.5f, 0.5f), new Vector2(4f, 4f), SpriteEffects.None, 0f);

        foreach (var bullet in _bullets)
            bullet.Draw(_spriteBatch, _pixel);

        DrawHealthBars();

        if (_roundPhase == RoundPhase.RoundOver)
            DrawAnnouncement();

        _spriteBatch.End();

        base.Draw(gameTime);
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
                _spriteBatch.Draw(_pixel, new Rectangle((int)x, top, (int)TerrainColumnWidth + 1, height - top), TerrainColor);
        }
    }

    private void DrawPlatforms()
    {
        foreach (var platform in _level.Platforms)
        {
            var bounds = new Rectangle(
                (int)platform.StartX,
                (int)platform.Height,
                (int)(platform.EndX - platform.StartX),
                (int)platform.Thickness);

            _spriteBatch.Draw(_pixel, bounds, PlatformColor);
            _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 3), PlatformEdgeColor);
        }
    }

    private void DrawWalls()
    {
        foreach (var wall in _level.Walls)
        {
            var bounds = wall.GetBounds();
            _spriteBatch.Draw(_pixel, bounds, WallColor);
            _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 3, bounds.Height), WallEdgeColor);
            _spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - 3, bounds.Y, 3, bounds.Height), WallEdgeColor);
        }
    }

    private void DrawHealthBars()
    {
        for (int i = 0; i < _players.Count; i++)
        {
            var player = _players[i];
            float x = HealthBarMargin + i * (HealthBarWidth + HealthBarMargin);
            const float y = HealthBarMargin;

            _spriteBatch.Draw(_pixel, new Rectangle((int)x, (int)y, (int)HealthBarWidth, (int)HealthBarHeight), Color.DimGray);

            float fillWidth = HealthBarWidth * player.Stickman.HealthFraction;
            _spriteBatch.Draw(_pixel, new Rectangle((int)x, (int)y, (int)fillWidth, (int)HealthBarHeight), player.Color);
        }
    }

    private void DrawAnnouncement()
    {
        Vector2 size = _font.MeasureString(_announcementText);
        Vector2 position = new Vector2(
            GraphicsDevice.Viewport.Width / 2f - size.X / 2f,
            GraphicsDevice.Viewport.Height / 2f - size.Y / 2f);

        _spriteBatch.DrawString(_font, _announcementText, position, _announcementColor);
    }

    // Drops a fighter at a random X and lands them on whatever surface is directly below - ground or a tier.
    private Vector2 RandomSpawnPosition()
    {
        float width = GraphicsDevice.Viewport.Width;
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
