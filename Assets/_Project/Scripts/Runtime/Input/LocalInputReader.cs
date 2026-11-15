using HEXBOUND.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HEXBOUND.Input
{
    /// <summary>
    /// Reads keyboard, mouse and gamepad, and turns them into one InputFrame per simulation tick.
    /// Presses that happen between ticks are remembered, so quick taps are never lost.
    /// Movement is camera-relative: the camera yaw is applied here and the result is a world direction.
    ///
    /// Keyboard and mouse: WASD move, mouse camera, LMB light, RMB heavy, Q/E skills, R ultimate,
    /// Shift tap dodge / hold walk (run otherwise), Space jump, Left Ctrl block (tap just before a hit to parry), F grab, X recharge,
    /// Z heal, C crouch, mouse wheel switch weapon, G throw or pick up weapon,
    /// Tab or middle mouse lock-on.
    /// Gamepad: left stick move, right stick camera, X light, Y heavy, A jump, B tap dodge / hold walk, LB block,
    /// RB skill 1, RT skill 2, LT ultimate, D-pad down grab, D-pad up recharge, right stick click lock-on.
    /// </summary>
    public sealed class LocalInputReader
    {
        private InputButtons _pressedSinceTick;
        private float _dodgeHeldFor = -1f;

        /// <summary>Shift (gamepad B) released sooner than this is a dodge; held longer, the fighter walks.</summary>
        public const float DodgeTapSeconds = 0.18f;

        /// <summary>Set false while menus are open so clicks don't attack.</summary>
        public bool GameplayEnabled = true;

        /// <summary>Call every rendered frame.</summary>
        public void Update()
        {
            if (!GameplayEnabled) { _dodgeHeldFor = -1f; return; }
            _pressedSinceTick |= ReadPressedThisFrame();

            // Shift / B: a quick tap dodges (on release), holding it walks.
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            bool down = (kb != null && kb.leftShiftKey.isPressed) || (pad != null && pad.buttonEast.isPressed);
            if (down) _dodgeHeldFor = _dodgeHeldFor < 0f ? 0f : _dodgeHeldFor + Time.unscaledDeltaTime;
            else if (_dodgeHeldFor >= 0f)
            {
                if (_dodgeHeldFor < DodgeTapSeconds) _pressedSinceTick |= InputButtons.Dodge;
                _dodgeHeldFor = -1f;
            }
        }

        /// <summary>
        /// Crosshair aim from the camera. The camera looks down at the fighter (about 14 degrees at rest), so its
        /// pitch is mapped: resting camera = level shot, camera tilted down = shooting down, tilted up = shooting up.
        /// </summary>
        public static void AimFromCamera(float cameraYawDegrees, float cameraPitchDegrees, out float yawDegrees, out float pitchDegrees)
        {
            yawDegrees = Mathf.Repeat(cameraYawDegrees, 360f);
            pitchDegrees = Mathf.Clamp((14f - cameraPitchDegrees) * 1.5f, -85f, 70f);
        }

        /// <summary>Builds the input for one tick from the local player's camera yaw and pitch.</summary>
        public InputFrame Sample(float cameraYawDegrees, float cameraPitchDegrees = 14f)
        {
            var frame = new InputFrame();
            if (!GameplayEnabled)
            {
                _pressedSinceTick = InputButtons.None;
                return frame;
            }

            InputButtons held = ReadHeld();
            // Running is the default; holding Shift (gamepad B) walks.
            if (_dodgeHeldFor < DodgeTapSeconds) held |= InputButtons.Run;
            frame.Buttons = held | _pressedSinceTick;
            _pressedSinceTick = InputButtons.None;

            Vector2 move = ReadMove();
            if (move.sqrMagnitude > 0.04f)
            {
                float localAngle = Mathf.Atan2(move.x, move.y) * Mathf.Rad2Deg;
                float worldAngle = cameraYawDegrees + localAngle;
                int simAngle = Mathf.RoundToInt(Mathf.Repeat(worldAngle, 360f) / 360f * FixMath.FullTurn);
                frame.HasMove = true;
                frame.MoveAngle = InputFrame.QuantizeAngle(simAngle);
            }

            float aimYaw, aimPitch;
            AimFromCamera(cameraYawDegrees, cameraPitchDegrees, out aimYaw, out aimPitch);
            frame.HasAim = true;
            frame.AimYaw = InputFrame.QuantizeAngle(Mathf.RoundToInt(aimYaw / 360f * FixMath.FullTurn));
            frame.AimPitch = (sbyte)Mathf.Clamp(Mathf.RoundToInt(aimPitch / InputFrame.AimPitchStep), -127, 127);
            return frame;
        }

        /// <summary>Camera rotation input in degrees for this frame (mouse delta or right stick).</summary>
        public Vector2 ReadLook(float mouseSensitivity, float deltaTime)
        {
            Vector2 look = Vector2.zero;
            Mouse mouse = Mouse.current;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                look += mouse.delta.ReadValue() * mouseSensitivity;
            }
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                look += pad.rightStick.ReadValue() * 180f * deltaTime;
            }
            return look;
        }

        private static Vector2 ReadMove()
        {
            Vector2 move = Vector2.zero;
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed) move.y += 1;
                if (kb.sKey.isPressed) move.y -= 1;
                if (kb.dKey.isPressed) move.x += 1;
                if (kb.aKey.isPressed) move.x -= 1;
            }
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > move.sqrMagnitude) move = stick;
            }
            return move;
        }

        private static InputButtons ReadHeld()
        {
            InputButtons b = InputButtons.None;
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;

            if (kb != null)
            {
                if (kb.leftCtrlKey.isPressed) b |= InputButtons.Block;
                if (kb.spaceKey.isPressed) b |= InputButtons.Jump;
                if (kb.qKey.isPressed) b |= InputButtons.Skill1;
                if (kb.eKey.isPressed) b |= InputButtons.Skill2;
                if (kb.rKey.isPressed) b |= InputButtons.Ultimate;
                if (kb.fKey.isPressed) b |= InputButtons.Grab;
                if (kb.xKey.isPressed) b |= InputButtons.Recharge;
                if (kb.zKey.isPressed) b |= InputButtons.Heal;
                if (kb.cKey.isPressed) b |= InputButtons.Crouch;
                if (kb.tabKey.isPressed) b |= InputButtons.LockOn;
                if (kb.gKey.isPressed) b |= InputButtons.Throw;
            }
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                if (mouse.leftButton.isPressed) b |= InputButtons.Light;
                if (mouse.rightButton.isPressed) b |= InputButtons.Heavy;
                if (mouse.middleButton.isPressed) b |= InputButtons.LockOn;
            }
            if (pad != null)
            {
                if (pad.buttonWest.isPressed) b |= InputButtons.Light;
                if (pad.buttonNorth.isPressed) b |= InputButtons.Heavy;
                if (pad.buttonSouth.isPressed) b |= InputButtons.Jump;
                if (pad.leftShoulder.isPressed) b |= InputButtons.Block;
                if (pad.rightShoulder.isPressed) b |= InputButtons.Skill1;
                if (pad.rightTrigger.isPressed) b |= InputButtons.Skill2;
                if (pad.leftTrigger.isPressed) b |= InputButtons.Ultimate;
                if (pad.dpad.down.isPressed) b |= InputButtons.Grab;
                if (pad.dpad.up.isPressed) b |= InputButtons.Recharge;
                if (pad.dpad.left.isPressed) b |= InputButtons.Heal;
                if (pad.leftStickButton.isPressed) b |= InputButtons.Crouch;
                if (pad.rightStickButton.isPressed) b |= InputButtons.LockOn;
                if (pad.selectButton.isPressed) b |= InputButtons.Throw;
            }
            return b;
        }

        private static InputButtons ReadPressedThisFrame()
        {
            InputButtons b = InputButtons.None;
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;

            if (kb != null)
            {
                if (kb.leftCtrlKey.wasPressedThisFrame) b |= InputButtons.Block;
                if (kb.spaceKey.wasPressedThisFrame) b |= InputButtons.Jump;
                if (kb.qKey.wasPressedThisFrame) b |= InputButtons.Skill1;
                if (kb.eKey.wasPressedThisFrame) b |= InputButtons.Skill2;
                if (kb.rKey.wasPressedThisFrame) b |= InputButtons.Ultimate;
                if (kb.fKey.wasPressedThisFrame) b |= InputButtons.Grab;
                if (kb.xKey.wasPressedThisFrame) b |= InputButtons.Recharge;
                if (kb.zKey.wasPressedThisFrame) b |= InputButtons.Heal;
                if (kb.cKey.wasPressedThisFrame) b |= InputButtons.Crouch;
                if (kb.tabKey.wasPressedThisFrame) b |= InputButtons.LockOn;
                if (kb.gKey.wasPressedThisFrame) b |= InputButtons.Throw;
            }
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                if (mouse.leftButton.wasPressedThisFrame) b |= InputButtons.Light;
                if (mouse.rightButton.wasPressedThisFrame) b |= InputButtons.Heavy;
                if (mouse.middleButton.wasPressedThisFrame) b |= InputButtons.LockOn;
                if (Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f) b |= InputButtons.SwitchWeapon;
            }
            if (pad != null)
            {
                if (pad.buttonWest.wasPressedThisFrame) b |= InputButtons.Light;
                if (pad.buttonNorth.wasPressedThisFrame) b |= InputButtons.Heavy;
                if (pad.buttonSouth.wasPressedThisFrame) b |= InputButtons.Jump;
                if (pad.leftShoulder.wasPressedThisFrame) b |= InputButtons.Block;
                if (pad.rightShoulder.wasPressedThisFrame) b |= InputButtons.Skill1;
                if (pad.rightTrigger.wasPressedThisFrame) b |= InputButtons.Skill2;
                if (pad.leftTrigger.wasPressedThisFrame) b |= InputButtons.Ultimate;
                if (pad.dpad.down.wasPressedThisFrame) b |= InputButtons.Grab;
                if (pad.dpad.up.wasPressedThisFrame) b |= InputButtons.Recharge;
                if (pad.dpad.left.wasPressedThisFrame) b |= InputButtons.Heal;
                if (pad.leftStickButton.wasPressedThisFrame) b |= InputButtons.Crouch;
                if (pad.dpad.right.wasPressedThisFrame) b |= InputButtons.SwitchWeapon;
                if (pad.selectButton.wasPressedThisFrame) b |= InputButtons.Throw;
                if (pad.rightStickButton.wasPressedThisFrame) b |= InputButtons.LockOn;
            }
            return b;
        }

        public static bool PausePressed()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            return (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
        }

        public static bool KeyPressed(Key key)
        {
            Keyboard kb = Keyboard.current;
            return kb != null && kb[key].wasPressedThisFrame;
        }
    }
}
