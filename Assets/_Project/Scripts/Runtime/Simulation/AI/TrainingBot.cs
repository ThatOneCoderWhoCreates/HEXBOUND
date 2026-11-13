namespace HEXBOUND.Simulation
{
    public enum BotMode : byte
    {
        /// <summary>Stands still.</summary>
        Idle,
        /// <summary>Holds block and turns toward the player.</summary>
        Block,
        /// <summary>Fights back.</summary>
        Fight,
    }

    /// <summary>
    /// Simple opponent for training and offline play. It reads the game state and produces an InputFrame,
    /// exactly like a human player. It is only used offline, so it keeps its own random state.
    /// </summary>
    public sealed class TrainingBot
    {
        public BotMode Mode = BotMode.Fight;

        /// <summary>Ticks between decisions. Lower is harder.</summary>
        public int ReactionTicks = 9;

        private uint _rng;
        private int _decisionTimer;
        private InputButtons _held;
        private InputButtons _pulse;
        private int _holdTicks;
        private int _strafeSign = 1;

        private static readonly Fix Close = Fix.Milli(2600);
        private static readonly Fix Mid = Fix.FromInt(7);
        private static readonly Fix EdgeMargin = Fix.Milli(2500);

        public TrainingBot(uint seed)
        {
            _rng = seed == 0 ? 12345u : seed;
        }

        public InputFrame Think(GameState s, int self)
        {
            ref FighterState me = ref s.Fighters[self];
            ref FighterState opp = ref s.Fighters[1 - self];
            var frame = new InputFrame();

            if (Mode == BotMode.Idle || s.Phase != MatchPhase.Fighting)
            {
                return frame;
            }

            if (Mode == BotMode.Block)
            {
                frame.Buttons = InputButtons.Block;
                return frame;
            }

            CharacterDef c = GameContent.GetCharacter(me.CharacterId);
            FixVec3 toOpp = (opp.Position - me.Position).Horizontal();
            Fix dist = toOpp.HorizontalLength();
            int angleToOpp = toOpp.ToAngle();
            bool oppThreat = opp.Action == FighterAction.Attack && dist < Mid;
            bool nearEdge = !Arena.IsInside(me.Position, EdgeMargin);

            InputButtons pulse = _pulse;
            _pulse = InputButtons.None;

            if (_holdTicks > 0)
            {
                _holdTicks--;
            }
            else
            {
                _held = InputButtons.None;
            }

            _decisionTimer--;
            if (_decisionTimer <= 0)
            {
                _decisionTimer = ReactionTicks + Roll(0, 5);
                Decide(ref me, ref opp, c, dist, oppThreat);
                if (Roll(0, 100) < 15) _strafeSign = -_strafeSign;
            }

            frame.Buttons = _held | pulse;

            int moveAngle = angleToOpp;
            bool wantsMove = true;
            if (nearEdge)
            {
                moveAngle = (FixVec3.Zero - me.Position).Horizontal().ToAngle();
            }
            else if ((_held & InputButtons.Block) != 0)
            {
                wantsMove = false;
            }
            else if (dist < Close)
            {
                moveAngle = FixMath.NormalizeAngle(angleToOpp + _strafeSign * FixMath.QuarterTurn);
                wantsMove = Roll(0, 100) < 30;
            }
            else if (c.Weapon == "Wand" && dist < Mid)
            {
                moveAngle = FixMath.NormalizeAngle(angleToOpp + _strafeSign * FixMath.QuarterTurn);
            }

            frame.HasMove = wantsMove;
            frame.MoveAngle = InputFrame.QuantizeAngle(moveAngle);
            return frame;
        }

        private void Decide(ref FighterState me, ref FighterState opp, CharacterDef c, Fix dist, bool oppThreat)
        {
            if (oppThreat)
            {
                int r = Roll(0, 100);
                if (r < 40)
                {
                    Hold(InputButtons.Block, 20);
                    return;
                }
                if (r < 55 && me.CooldownDodge == 0)
                {
                    _pulse = InputButtons.Dodge;
                    return;
                }
            }

            if (me.Health < c.MaxHealth * 2 / 5 && dist > Mid && me.CooldownHeal == 0)
            {
                _pulse = InputButtons.Heal;
                return;
            }

            if (me.Magic < 160 && dist > Mid && me.CooldownRecharge == 0)
            {
                _pulse = InputButtons.Recharge;
                return;
            }

            if (c.Ultimate != null && me.CooldownUltimate == 0 && me.Magic >= c.Ultimate.MagicCost && dist < Mid && Roll(0, 100) < 12)
            {
                _pulse = InputButtons.Ultimate;
                return;
            }

            if (dist < Close)
            {
                int r = Roll(0, 100);
                if (r < 50) _pulse = InputButtons.Light;
                else if (r < 65) _pulse = InputButtons.Heavy;
                else if (r < 75) _pulse = InputButtons.Grab;
                else if (r < 85 && CanUse(ref me, c.Skill1, me.CooldownSkill1)) _pulse = InputButtons.Skill1;
                else if (r < 92 && CanUse(ref me, c.Skill2, me.CooldownSkill2)) _pulse = InputButtons.Skill2;
                else _pulse = InputButtons.Jump;
                return;
            }

            if (dist < Mid)
            {
                int r = Roll(0, 100);
                if (r < 25 && CanUse(ref me, c.Skill1, me.CooldownSkill1)) _pulse = InputButtons.Skill1;
                else if (r < 40 && CanUse(ref me, c.Skill2, me.CooldownSkill2)) _pulse = InputButtons.Skill2;
                else if (r < 50 && c.Weapon == "Wand") _pulse = InputButtons.Heavy;
                return;
            }

            if (CanUse(ref me, c.Skill2, me.CooldownSkill2) && c.Skill2.Spawns.Length > 0 && Roll(0, 100) < 30)
            {
                _pulse = InputButtons.Skill2;
            }
            else if (CanUse(ref me, c.Skill1, me.CooldownSkill1) && c.Skill1.Spawns.Length > 0 && Roll(0, 100) < 30)
            {
                _pulse = InputButtons.Skill1;
            }
        }

        private static bool CanUse(ref FighterState me, MoveDef move, int cooldown)
        {
            return move != null && cooldown == 0 && me.Magic >= move.MagicCost;
        }

        private void Hold(InputButtons button, int ticks)
        {
            _held = button;
            _holdTicks = ticks;
        }

        private int Roll(int min, int max)
        {
            return DeterministicRandom.Range(ref _rng, min, max);
        }
    }
}
