using HEXBOUND.Simulation;

namespace HEXBOUND.App
{
    /// <summary>
    /// Training mode checklist: the basic controls, ticked off as the player actually does each one. It only reads the
    /// local fighter's state each frame, so it never changes the game. F3 hides or shows it.
    /// </summary>
    public sealed class TutorialChecklist
    {
        private enum Step
        {
            Run, Walk, Jump, Dodge, Light, Guard, Skill, Ultimate, Recharge, DropOrThrow, PickUp, Count,
        }

        private static readonly string[] Labels =
        {
            "Run  (W A S D)",
            "Walk  (hold Shift while moving)",
            "Jump  (Space)",
            "Dodge  (tap Shift)",
            "Attack  (left click)",
            "Block or guard  (Ctrl, or right click with a staff or launcher)",
            "Skill  (Q or E)",
            "Ultimate  (R)",
            "Recharge magic  (X)",
            "Drop your weapon  (tap G)  or throw it  (hold G)",
            "Pick it up again  (walk over it)",
        };

        private readonly bool[] _done = new bool[(int)Step.Count];
        private bool _hadDropped;
        private int _walkFrames;

        public bool Visible = true;

        public void Reset()
        {
            for (int i = 0; i < _done.Length; i++) _done[i] = false;
            _hadDropped = false;
        }

        public bool AllDone
        {
            get
            {
                for (int i = 0; i < _done.Length; i++) if (!_done[i]) return false;
                return true;
            }
        }

        /// <summary>Call once per frame with the current state during a training match.</summary>
        public void Observe(GameState s, int localIndex)
        {
            ref FighterState f = ref s.Fighters[localIndex];
            CharacterDef c = GameContent.GetCharacter(f.CharacterId);
            float speed = f.Velocity.HorizontalLength().ToFloat() * GameConfig.TickRate;
            float run = c.MoveSpeed.ToFloat() * GameConfig.TickRate;
            if (f.Action == FighterAction.Move && f.Grounded)
            {
                if (speed > run * 0.8f) _done[(int)Step.Run] = true;
                // Walking speed (half of running) held for a moment, not just a slowdown against a wall.
                _walkFrames = speed > run * 0.4f && speed < run * 0.6f ? _walkFrames + 1 : 0;
                if (_walkFrames > 20) _done[(int)Step.Walk] = true;
            }
            else _walkFrames = 0;
            if (f.Action == FighterAction.Air && f.Velocity.Y.ToFloat() > 0.05f) _done[(int)Step.Jump] = true;
            if (f.Action == FighterAction.Block || f.Action == FighterAction.CrouchBlock) _done[(int)Step.Guard] = true;
            if (f.Action == FighterAction.Attack)
            {
                int id = f.MoveId;
                if (Is(c.Dodge, id) || Is(c.AirDash, id)) _done[(int)Step.Dodge] = true;
                else if (Is(c.Skill1, id) || Is(c.Skill2, id)) _done[(int)Step.Skill] = true;
                else if (Is(c.Ultimate, id)) _done[(int)Step.Ultimate] = true;
                else if (Is(c.Recharge, id)) _done[(int)Step.Recharge] = true;
                else if (Is(c.Heal, id)) { }
                else _done[(int)Step.Light] = true;
            }
            if (f.WeaponsDropped != 0 || f.WeaponsThrown != 0)
            {
                _done[(int)Step.DropOrThrow] = true;
                _hadDropped = true;
            }
            else if (_hadDropped && f.WeaponsHeld == c.WeaponMask)
            {
                _done[(int)Step.PickUp] = true;
            }
            if (!c.CanThrowWeapon)
            {
                _done[(int)Step.DropOrThrow] = true;
                _done[(int)Step.PickUp] = true;
            }
        }

        private static bool Is(MoveDef m, int id)
        {
            return m != null && m.Id == id;
        }

        /// <summary>Rich text for the HUD (empty when hidden).</summary>
        public string Text()
        {
            if (!Visible) return string.Empty;
            var sb = new System.Text.StringBuilder();
            sb.Append("<b>HOW TO PLAY</b>   <color=#9aa0a8>F3 hide</color>\n");
            for (int i = 0; i < Labels.Length; i++)
            {
                if (_done[i]) sb.Append("<color=#7ee07e>[x]  ").Append(Labels[i]).Append("</color>\n");
                else sb.Append("<color=#e8e2d0>[  ]  ").Append(Labels[i]).Append("</color>\n");
            }
            if (AllDone) sb.Append("\n<color=#ffd76a>All done. You're ready for a real match!</color>");
            return sb.ToString();
        }
    }
}
