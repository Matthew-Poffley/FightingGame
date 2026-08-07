using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace FightingGame;

public readonly struct PlayerInput
{
    public float MoveDirection { get; init; }
    public bool JumpPressed { get; init; }
    public bool CrouchHeld { get; init; }
    public bool FireHeld { get; init; }
    public bool BlockHeld { get; init; }
    public Vector2? AimDirection { get; init; }
}

public class Player
{
    private const float TriggerThreshold = 0.4f;
    private const float AimStickDeadzone = 0.2f;
    private const float MinMouseAimDistance = 12f;

    public PlayerIndex? ControllerIndex { get; }
    public bool UsesKeyboard { get; }
    public Color Color { get; }
    public Stickman Stickman { get; } = new();

    // Starts true so the same button press that joins the player doesn't also register as a jump.
    private bool _jumpWasDown = true;

    public Player(PlayerIndex? controllerIndex, bool usesKeyboard, Color color, Vector2 startPosition)
    {
        ControllerIndex = controllerIndex;
        UsesKeyboard = usesKeyboard;
        Color = color;
        Stickman.Position = startPosition;
    }

    public PlayerInput GatherInput(KeyboardState keyboard)
    {
        float moveDirection = 0f;
        bool crouchHeld = false;
        bool jumpDown = false;
        bool fireHeld = false;
        bool blockHeld = false;
        Vector2? aimDirection = null;

        if (UsesKeyboard)
        {
            if (keyboard.IsKeyDown(Keys.Left) || keyboard.IsKeyDown(Keys.A))
                moveDirection -= 1f;
            if (keyboard.IsKeyDown(Keys.Right) || keyboard.IsKeyDown(Keys.D))
                moveDirection += 1f;

            crouchHeld |= keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down);
            jumpDown |= keyboard.IsKeyDown(Keys.Space);
            fireHeld |= keyboard.IsKeyDown(Keys.J);
            blockHeld |= keyboard.IsKeyDown(Keys.K);

            var mouseState = Mouse.GetState();
            Vector2 toMouse = new Vector2(mouseState.X, mouseState.Y) - Stickman.Position;
            if (toMouse.LengthSquared() > MinMouseAimDistance * MinMouseAimDistance)
                aimDirection = Vector2.Normalize(toMouse);
        }

        if (ControllerIndex.HasValue)
        {
            var gamePad = GamePad.GetState(ControllerIndex.Value);
            if (gamePad.IsConnected)
            {
                if (gamePad.DPad.Left == ButtonState.Pressed || gamePad.ThumbSticks.Left.X < -0.25f)
                    moveDirection -= 1f;
                if (gamePad.DPad.Right == ButtonState.Pressed || gamePad.ThumbSticks.Left.X > 0.25f)
                    moveDirection += 1f;

                crouchHeld |= gamePad.DPad.Down == ButtonState.Pressed || gamePad.ThumbSticks.Left.Y < -0.5f;
                jumpDown |= gamePad.Buttons.A == ButtonState.Pressed;
                fireHeld |= gamePad.Triggers.Right > TriggerThreshold;
                blockHeld |= gamePad.Triggers.Left > TriggerThreshold;

                Vector2 rightStick = gamePad.ThumbSticks.Right;
                if (rightStick.LengthSquared() > AimStickDeadzone * AimStickDeadzone)
                    aimDirection = Vector2.Normalize(new Vector2(rightStick.X, -rightStick.Y));
            }
        }

        bool jumpPressed = jumpDown && !_jumpWasDown;
        _jumpWasDown = jumpDown;

        return new PlayerInput
        {
            MoveDirection = MathHelper.Clamp(moveDirection, -1f, 1f),
            JumpPressed = jumpPressed,
            CrouchHeld = crouchHeld,
            FireHeld = fireHeld,
            BlockHeld = blockHeld,
            AimDirection = aimDirection
        };
    }
}
