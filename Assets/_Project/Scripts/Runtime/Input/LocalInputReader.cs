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
    /// Shift dodge, Space jump, Left Ctrl block (tap just before a hit to parry), F grab, M recharge,
    /// Tab or middle mouse lock-on.
    /// Gamepad: left stick move, right stick camera, X light, Y heavy, A jump, B dodge, LB block,
    /// RB skill 1, RT skill 2, LT ultimate, D-pad down grab, D-pad up recharge, right stick click lock-on.
    /// </summary>
    public sealed class LocalInputReader
    {
        private InputButtons _pressedSinceTick;

        /// <summary>Set false while menus are open so clicks don't attack.</summary>
        public bool GameplayEnabled = true;

        /// <summary>Call every rendered frame.</summary>
        public void Update()
        {
            if (!GameplayEnabled) return;
            _pressedSinceTick |= ReadPressedThisFrame();
        }

        /// <summary>Builds the input for one tick. cameraYawDegrees is the yaw of the local player's camera.</summary>
        public InputFrame Sample(float cameraYawDegrees)
        {
            var frame = new InputFrame();
            if (!GameplayEnabled)
            {
                _pressedSinceTick = InputButtons.None;
                return frame;
            }

            InputButtons held = ReadHeld();
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
                if (kb.leftShiftKey.isPressed) b |= InputButtons.Dodge;
                if (kb.qKey.isPressed) b |= InputButtons.Skill1;
                if (kb.eKey.isPressed) b |= InputButtons.Skill2;
                if (kb.rKey.isPressed) b |= InputButtons.Ultimate;
                if (kb.fKey.isPressed) b |= InputButtons.Grab;
                if (kb.mKey.isPressed) b |= InputButtons.Recharge;
                if (kb.zKey.isPressed) b |= InputButtons.Heal;
                if (kb.tabKey.isPressed) b |= InputButtons.LockOn;
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
                if (pad.buttonEast.isPressed) b |= InputButtons.Dodge;
                if (pad.leftShoulder.isPressed) b |= InputButtons.Block;
                if (pad.rightShoulder.isPressed) b |= InputButtons.Skill1;
                if (pad.rightTrigger.isPressed) b |= InputButtons.Skill2;
                if (pad.leftTrigger.isPressed) b |= InputButtons.Ultimate;
                if (pad.dpad.down.isPressed) b |= InputButtons.Grab;
                if (pad.dpad.up.isPressed) b |= InputButtons.Recharge;
                if (pad.dpad.left.isPressed) b |= InputButtons.Heal;
                if (pad.rightStickButton.isPressed) b |= InputButtons.LockOn;
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
                if (kb.leftShiftKey.wasPressedThisFrame) b |= InputButtons.Dodge;
                if (kb.qKey.wasPressedThisFrame) b |= InputButtons.Skill1;
                if (kb.eKey.wasPressedThisFrame) b |= InputButtons.Skill2;
                if (kb.rKey.wasPressedThisFrame) b |= InputButtons.Ultimate;
                if (kb.fKey.wasPressedThisFrame) b |= InputButtons.Grab;
                if (kb.mKey.wasPressedThisFrame) b |= InputButtons.Recharge;
                if (kb.zKey.wasPressedThisFrame) b |= InputButtons.Heal;
                if (kb.tabKey.wasPressedThisFrame) b |= InputButtons.LockOn;
            }
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                if (mouse.leftButton.wasPressedThisFrame) b |= InputButtons.Light;
                if (mouse.rightButton.wasPressedThisFrame) b |= InputButtons.Heavy;
                if (mouse.middleButton.wasPressedThisFrame) b |= InputButtons.LockOn;
            }
            if (pad != null)
            {
                if (pad.buttonWest.wasPressedThisFrame) b |= InputButtons.Light;
                if (pad.buttonNorth.wasPressedThisFrame) b |= InputButtons.Heavy;
                if (pad.buttonSouth.wasPressedThisFrame) b |= InputButtons.Jump;
                if (pad.buttonEast.wasPressedThisFrame) b |= InputButtons.Dodge;
                if (pad.leftShoulder.wasPressedThisFrame) b |= InputButtons.Block;
                if (pad.rightShoulder.wasPressedThisFrame) b |= InputButtons.Skill1;
                if (pad.rightTrigger.wasPressedThisFrame) b |= InputButtons.Skill2;
                if (pad.leftTrigger.wasPressedThisFrame) b |= InputButtons.Ultimate;
                if (pad.dpad.down.wasPressedThisFrame) b |= InputButtons.Grab;
                if (pad.dpad.up.wasPressedThisFrame) b |= InputButtons.Recharge;
                if (pad.dpad.left.wasPressedThisFrame) b |= InputButtons.Heal;
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
