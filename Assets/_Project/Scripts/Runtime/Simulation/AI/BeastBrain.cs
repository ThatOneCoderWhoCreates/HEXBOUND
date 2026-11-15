using System;

namespace HEXBOUND.Simulation
{
    /// <summary>
    /// The beast's mind. It plays through an InputFrame like a human, sees the player only after a reaction
    /// delay, and learns the player's habits into a <see cref="BeastMemory"/>:
    /// - Defending: once it has seen a move start it blocks, parries (block just before the hit), uses the low
    ///   guard against sweeps, or dodges grabs. Moves the player uses predictably (spamming Q or E, the same
    ///   combo, firing a skill the moment its cooldown ends) are expected, so it reacts to them almost at once
    ///   and deflects them.
    /// - Attacking: it picks attacks that beat the player's usual defense (grab or heavy against blockers,
    ///   sweeps against standing guards, slams against low guards and dodgers, baits against button mashers),
    ///   and punishes openings such as a parried or whiffed move.
    /// Its level (saved in memory) sets reaction time, parry skill and how much it trusts what it learned.
    /// Offline only, so it can use floats and keeps its own random state.
    /// </summary>
    public sealed class BeastBrain
    {
        private struct MoveThreat
        {
            public bool Valid;
            public int First;
            public int Last;
            public float Reach;
            public bool Low;
            public bool Grab;
            public bool Unblockable;
            public bool Ranged;
            public float ProjectileSpeed;
            public float SpawnForward;
        }

        public readonly BeastMemory Memory;

        /// <summary>Optional debug output (used by tests).</summary>
        public Action<string> Trace;

        private uint _rng;
        private readonly MoveThreat[] _threats;

        // Learning state.
        private FighterState _prevOpp;
        private bool _hasPrev;
        private PlayerAct _lastAct = PlayerAct.None;
        private int _lastBeastAttackTick = -1000;
        private readonly bool[] _available = new bool[BeastMemory.Acts];

        // Reaction state.
        private int _oppMoveSerial;
        private int _handledSerial = -1;
        private int _defendPlan;
        private int _defendUntilTick;
        private bool _parryReset;
        private readonly int[] _projectileHandled = new int[16];

        // Output state.
        private InputButtons _held;
        private int _holdUntilTick;
        private InputButtons _pulse;
        private bool _moveAway;
        private int _nextDecisionTick;
        private int _strafeSign = 1;
        private int _comboGoal;

        private static readonly Fix EdgeMargin = Fix.Milli(3000);

        private const int PlanNone = 0;
        private const int PlanParry = 1;
        private const int PlanBlock = 2;
        private const int PlanLowGuard = 3;
        private const int PlanEscape = 4;

        public BeastBrain(BeastMemory memory, uint seed)
        {
            Memory = memory ?? new BeastMemory();
            _rng = seed == 0 ? 777u : seed;
            _threats = new MoveThreat[GameContent.Moves.Length];
            for (int i = 0; i < _threats.Length; i++) _threats[i] = BuildThreat(GameContent.Moves[i]);
            for (int i = 0; i < _projectileHandled.Length; i++) _projectileHandled[i] = -1;
        }

        // ------------------------------------------------------------------ Difficulty

        private float T
        {
            get { return Memory.Level / (float)BeastMemory.MaxLevel; }
        }

        /// <summary>Ticks before the beast notices something it did not expect.</summary>
        private int ReactionDelay
        {
            get { return (int)Math.Round(Lerp(20f, 7f, T)); }
        }

        private float ParrySkill
        {
            get { return Lerp(0.1f, 0.85f, T); }
        }

        private float BlockSkill
        {
            get { return Lerp(0.3f, 0.92f, T); }
        }

        /// <summary>How much a confident prediction shortens the reaction delay (1 = instant when certain).</summary>
        private float Anticipation
        {
            get { return Lerp(0.55f, 1f, T); }
        }

        /// <summary>How much the beast trusts what it learned when picking attacks (vs. picking at random).</summary>
        private float Trust
        {
            get { return Lerp(0.35f, 1f, T); }
        }

        // ------------------------------------------------------------------ Main

        public InputFrame Think(GameState s, int self)
        {
            ref FighterState me = ref s.Fighters[self];
            ref FighterState opp = ref s.Fighters[1 - self];
            var frame = new InputFrame();
            if (s.Phase != MatchPhase.Fighting)
            {
                _hasPrev = false;
                _held = InputButtons.None;
                return frame;
            }

            CharacterDef c = GameContent.GetCharacter(me.CharacterId);
            CharacterDef oc = GameContent.GetCharacter(opp.CharacterId);
            FixVec3 toOpp = (opp.Position - me.Position).Horizontal();
            float dist = toOpp.HorizontalLength().ToFloat();
            int angleToOpp = toOpp.ToAngle();

            Learn(s, ref me, ref opp, oc, dist);

            if (s.Tick >= _holdUntilTick) _held = InputButtons.None;
            _pulse = InputButtons.None;

            bool defending = Defend(s, self, ref me, ref opp, oc, dist);
            if (!defending && s.Tick >= _nextDecisionTick && CanAct(ref me))
            {
                Decide(s, ref me, ref opp, c, oc, dist);
                if (Roll(0, 100) < 12) _strafeSign = -_strafeSign;
            }
            ContinueCombo(ref me, c);

            frame.Buttons = _held | _pulse;

            // Movement.
            int moveAngle = angleToOpp;
            bool wantsMove = (_held & (InputButtons.Block | InputButtons.Crouch)) == 0;
            float reach = c.Radius.ToFloat() + oc.Radius.ToFloat() + 1.1f;
            if (!Arena.IsInside(me.Position, EdgeMargin))
            {
                moveAngle = (FixVec3.Zero - me.Position).Horizontal().ToAngle();
            }
            else if (_moveAway)
            {
                moveAngle = FixMath.NormalizeAngle(angleToOpp + FixMath.HalfTurn);
            }
            else if (dist < reach * 0.85f)
            {
                moveAngle = FixMath.NormalizeAngle(angleToOpp + _strafeSign * FixMath.QuarterTurn);
                wantsMove = wantsMove && Roll(0, 100) < 20;
            }
            frame.HasMove = wantsMove;
            if (wantsMove) frame.Buttons |= InputButtons.Run;
            frame.MoveAngle = InputFrame.QuantizeAngle(moveAngle);
            return frame;
        }

        private static bool CanAct(ref FighterState f)
        {
            switch (f.Action)
            {
                case FighterAction.Idle:
                case FighterAction.Move:
                case FighterAction.Block:
                case FighterAction.Crouch:
                case FighterAction.CrouchBlock:
                case FighterAction.Air:
                    return true;
                default:
                    return false;
            }
        }

        // ------------------------------------------------------------------ Learning

        private void Learn(GameState s, ref FighterState me, ref FighterState opp, CharacterDef oc, float dist)
        {
            if (me.Action == FighterAction.Attack && me.MoveTick == 0) _lastBeastAttackTick = s.Tick;
            if (!_hasPrev)
            {
                _prevOpp = opp;
                _hasPrev = true;
                return;
            }

            if (IsFree(ref _prevOpp))
            {
                // Count the ticks each action was available, to learn how eagerly the player uses it.
                int ctxNow = Context(ref me, dist);
                Availability(ref _prevOpp, oc, _available);
                for (int k = 1; k < BeastMemory.Acts; k++)
                {
                    if (_available[k]) Memory.Available[ctxNow * BeastMemory.Acts + k] += 1f;
                }
            }

            PlayerAct act = DetectAct(ref _prevOpp, ref opp, oc);
            if (opp.Action == FighterAction.Attack && (_prevOpp.Action != FighterAction.Attack || _prevOpp.MoveId != opp.MoveId || opp.MoveTick < _prevOpp.MoveTick))
            {
                _oppMoveSerial++;
            }

            if (act != PlayerAct.None)
            {
                int ctx = Context(ref me, dist);
                Memory.Situational[ctx * BeastMemory.Acts + (int)act] += 1f;
                Memory.Sequence[(int)_lastAct * BeastMemory.Acts + (int)act] += 1f;
                if (s.Tick - _lastBeastAttackTick < 40) Memory.Defense[(int)act] += 1f;
                _lastAct = act;
            }
            else if (s.Tick - _lastBeastAttackTick == 40 && opp.Action != FighterAction.HitStun && opp.Action != FighterAction.Launched)
            {
                // The player did nothing special against the beast's attack.
                Memory.Defense[(int)PlayerAct.None] += 0.5f;
            }

            _prevOpp = opp;
        }

        private static bool IsFree(ref FighterState f)
        {
            switch (f.Action)
            {
                case FighterAction.Idle:
                case FighterAction.Move:
                case FighterAction.Block:
                case FighterAction.Crouch:
                case FighterAction.CrouchBlock:
                case FighterAction.Air:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Which actions the player could start right now.</summary>
        private static void Availability(ref FighterState f, CharacterDef c, bool[] a)
        {
            bool ground = f.Grounded;
            a[(int)PlayerAct.Light] = ground;
            a[(int)PlayerAct.Heavy] = ground;
            a[(int)PlayerAct.Grab] = ground && c.Grab != null;
            a[(int)PlayerAct.CrouchAttack] = ground && c.CrouchAttack != null;
            a[(int)PlayerAct.Block] = ground;
            a[(int)PlayerAct.CrouchGuard] = ground;
            a[(int)PlayerAct.Jump] = ground;
            a[(int)PlayerAct.AirAttack] = !ground;
            a[(int)PlayerAct.Dodge] = f.CooldownDodge == 0;
            a[(int)PlayerAct.Skill1] = Ready(c.Skill1, f.CooldownSkill1, f.Magic);
            a[(int)PlayerAct.Skill2] = Ready(c.Skill2, f.CooldownSkill2, f.Magic);
            a[(int)PlayerAct.Ultimate] = Ready(c.Ultimate, f.CooldownUltimate, f.Magic);
            a[(int)PlayerAct.Heal] = c.Heal != null && f.CooldownHeal == 0 && f.Health < c.MaxHealth;
            a[(int)PlayerAct.Recharge] = c.Recharge != null && f.CooldownRecharge == 0 && f.Magic < c.MaxMagic;
        }

        private static bool Ready(MoveDef m, int cooldown, int magic)
        {
            return m != null && cooldown == 0 && magic >= m.MagicCost;
        }

        private static PlayerAct DetectAct(ref FighterState prev, ref FighterState now, CharacterDef c)
        {
            bool newMove = now.Action == FighterAction.Attack && (prev.Action != FighterAction.Attack || prev.MoveId != now.MoveId || now.MoveTick < prev.MoveTick);
            if (newMove) return Classify(c, now.MoveId);
            if (now.Action == FighterAction.Block && prev.Action != FighterAction.Block && prev.Action != FighterAction.BlockStun) return PlayerAct.Block;
            bool lowNow = now.Action == FighterAction.CrouchBlock || now.Action == FighterAction.Crouch;
            bool lowBefore = prev.Action == FighterAction.CrouchBlock || prev.Action == FighterAction.Crouch || prev.Action == FighterAction.BlockStun;
            if (lowNow && !lowBefore) return PlayerAct.CrouchGuard;
            if (now.Action == FighterAction.Air && prev.Grounded && now.Velocity.Y > Fix.Zero && prev.Action != FighterAction.Attack) return PlayerAct.Jump;
            return PlayerAct.None;
        }

        private static PlayerAct Classify(CharacterDef c, int moveId)
        {
            if (Is(c.Skill1, moveId)) return PlayerAct.Skill1;
            if (Is(c.Skill2, moveId)) return PlayerAct.Skill2;
            if (Is(c.Ultimate, moveId)) return PlayerAct.Ultimate;
            if (Is(c.Grab, moveId)) return PlayerAct.Grab;
            if (Is(c.Dodge, moveId) || Is(c.AirDash, moveId)) return PlayerAct.Dodge;
            if (Is(c.CrouchAttack, moveId)) return PlayerAct.CrouchAttack;
            if (Is(c.Heal, moveId)) return PlayerAct.Heal;
            if (Is(c.Recharge, moveId)) return PlayerAct.Recharge;
            if (Is(c.Heavy, moveId) || Is(c.BuffHeavy, moveId)) return PlayerAct.Heavy;
            if (Is(c.AirLight, moveId) || Is(c.AirHeavy, moveId) || Is(c.BuffAirLight, moveId) || Is(c.BuffAirHeavy, moveId)) return PlayerAct.AirAttack;
            return PlayerAct.Light;
        }

        private static bool Is(MoveDef m, int id)
        {
            return m != null && m.Id == id;
        }

        private static int Context(ref FighterState me, float dist)
        {
            int band = dist < 3.2f ? 0 : dist < 7f ? 1 : 2;
            int state;
            switch (me.Action)
            {
                case FighterAction.Attack:
                    state = 1;
                    break;
                case FighterAction.HitStun:
                case FighterAction.BlockStun:
                case FighterAction.Launched:
                case FighterAction.Knockdown:
                case FighterAction.GetUp:
                case FighterAction.Parried:
                case FighterAction.GuardBroken:
                case FighterAction.Landing:
                    state = 2;
                    break;
                default:
                    state = 0;
                    break;
            }
            return band * 3 + state;
        }

        /// <summary>
        /// How likely the player's next action is <paramref name="act"/>. Each action's learned eagerness
        /// (uses per tick it was available) is compared with the others the player could pick right now,
        /// nudged by what the player usually does after their last action.
        /// </summary>
        public float Predict(PlayerAct act, ref FighterState me, float dist)
        {
            if (_hasPrev) Availability(ref _prevOpp, GameContent.GetCharacter(_prevOpp.CharacterId), _available);
            int ctx = Context(ref me, dist);
            float rowTotal = 0f;
            for (int k = 1; k < BeastMemory.Acts; k++) rowTotal += Memory.Sequence[(int)_lastAct * BeastMemory.Acts + k];
            float total = 0f;
            float mine = 0f;
            for (int k = 1; k < BeastMemory.Acts; k++)
            {
                if (_hasPrev && !_available[k] && k != (int)act) continue;
                int i = ctx * BeastMemory.Acts + k;
                float rate = (Memory.Situational[i] + 0.2f) / (Memory.Available[i] + 30f);
                float follow = 1f + 2f * Memory.Sequence[(int)_lastAct * BeastMemory.Acts + k] / (rowTotal + 4f);
                float n = rate * follow;
                total += n;
                if (k == (int)act) mine = n;
            }
            return total > 0f ? mine / total : 0f;
        }

        // ------------------------------------------------------------------ Defense

        private bool Defend(GameState s, int self, ref FighterState me, ref FighterState opp, CharacterDef oc, float dist)
        {
            if (DefendProjectiles(s, self, ref me))
            {
                return true;
            }

            if (opp.Action != FighterAction.Attack)
            {
                _defendPlan = PlanNone;
                return false;
            }

            MoveThreat th = opp.MoveId >= 0 && opp.MoveId < _threats.Length ? _threats[opp.MoveId] : default(MoveThreat);
            if (!th.Valid || opp.MoveTick > th.Last + (th.Ranged ? 30 : 0)) return false;
            if (!th.Ranged && dist > th.Reach + 0.6f) return false;

            if (_handledSerial != _oppMoveSerial)
            {
                // Noticing: unexpected moves take the full reaction delay, expected ones far less.
                PlayerAct act = Classify(oc, opp.MoveId);
                float p = Predict(act, ref me, dist);
                int delay = Math.Max(1, (int)Math.Round(ReactionDelay * (1f - Anticipation * Math.Min(1f, p * 1.6f))));
                if (opp.MoveTick < delay)
                {
                    return false;
                }

                _handledSerial = _oppMoveSerial;
                _parryReset = false;
                float confident = Math.Min(1f, 0.55f + p);
                if (th.Grab || th.Unblockable)
                {
                    _defendPlan = Chance(BlockSkill * confident) ? PlanEscape : PlanNone;
                }
                else if (th.Low)
                {
                    _defendPlan = Chance(BlockSkill * confident) ? PlanLowGuard : PlanNone;
                }
                else if (Chance(ParrySkill * confident))
                {
                    _defendPlan = PlanParry;
                }
                else
                {
                    _defendPlan = Chance(BlockSkill) ? PlanBlock : PlanNone;
                }
                _defendUntilTick = s.Tick + (th.Last - opp.MoveTick) + (th.Ranged ? 30 : 6);
                if (Trace != null) Trace("t" + s.Tick + " saw " + act + " p=" + p.ToString("0.00") + " delay=" + delay + " moveTick=" + opp.MoveTick + " first=" + th.First + " plan=" + _defendPlan + " me=" + me.Action);
            }

            if (_defendPlan == PlanNone || s.Tick > _defendUntilTick || !CanDefendNow(ref me)) return false;

            int first = th.First;
            if (th.Ranged) first += (int)Math.Max(0f, (dist - th.SpawnForward) / Math.Max(0.05f, th.ProjectileSpeed));
            int ticksToHit = first - opp.MoveTick;
            switch (_defendPlan)
            {
                case PlanParry:
                {
                    // Start the block 1-4 ticks before the hit, inside the parry window. A block held since
                    // earlier is released for one tick first, because only a fresh block parries.
                    bool staleBlock = me.Action == FighterAction.Block && me.BlockTicks > GameConfig.ParryWindow - 3;
                    if (ticksToHit > 4)
                    {
                        if (staleBlock) Release(s);
                    }
                    else if (staleBlock && !_parryReset && ticksToHit >= 2)
                    {
                        Release(s);
                        _parryReset = true;
                    }
                    else
                    {
                        Hold(s, InputButtons.Block, 6);
                    }
                    break;
                }
                case PlanBlock:
                    Hold(s, InputButtons.Block, 8);
                    break;
                case PlanLowGuard:
                    Hold(s, InputButtons.Crouch | InputButtons.Heavy, 8);
                    break;
                case PlanEscape:
                    if (me.Action != FighterAction.Attack && (me.CooldownDodge == 0 || me.Grounded))
                    {
                        _pulse = me.CooldownDodge == 0 ? InputButtons.Dodge : InputButtons.Jump;
                        _moveAway = true;
                        _defendPlan = PlanNone;
                    }
                    break;
            }
            return true;
        }

        private bool DefendProjectiles(GameState s, int self, ref FighterState me)
        {
            for (int i = 0; i < s.Projectiles.Length; i++)
            {
                ref ProjectileState p = ref s.Projectiles[i];
                if (!p.Active || p.Owner == self) continue;
                FixVec3 to = me.Position - p.Position;
                float d = to.HorizontalLength().ToFloat();
                float speed = p.Velocity.HorizontalLength().ToFloat();
                if (speed < 0.01f || d > 9f) continue;
                float closing = (to.X * p.Velocity.X + to.Z * p.Velocity.Z).ToFloat();
                if (closing <= 0f) continue;
                int ticksToHit = (int)(d / speed);
                if (i >= _projectileHandled.Length) continue;
                int ignored = -2 - p.SerialId;
                if (_projectileHandled[i] != p.SerialId && _projectileHandled[i] != ignored)
                {
                    // Projectiles from a skill the player spams are expected and deflected more often.
                    float expected = _lastAct == PlayerAct.Skill1 || _lastAct == PlayerAct.Skill2 ? Predict(_lastAct, ref me, d) : 0f;
                    bool react = Chance(Math.Max(BlockSkill * 0.75f, Math.Min(0.97f, expected * 2f)));
                    _projectileHandled[i] = react ? p.SerialId : ignored;
                }
                if (_projectileHandled[i] != p.SerialId) continue;
                if (ticksToHit <= 5 && CanDefendNow(ref me))
                {
                    Hold(s, InputButtons.Block, 8);
                    return true;
                }
            }
            return false;
        }

        private static bool CanDefendNow(ref FighterState me)
        {
            switch (me.Action)
            {
                case FighterAction.Idle:
                case FighterAction.Move:
                case FighterAction.Block:
                case FighterAction.BlockStun:
                case FighterAction.Crouch:
                case FighterAction.CrouchBlock:
                case FighterAction.Landing:
                    return true;
                default:
                    return false;
            }
        }

        // ------------------------------------------------------------------ Offense

        private void Decide(GameState s, ref FighterState me, ref FighterState opp, CharacterDef c, CharacterDef oc, float dist)
        {
            _moveAway = false;
            float t = T;
            bool awakened = me.Buff == BuffId.Awakened;
            int interval = (int)Math.Round(Lerp(22f, 6f, t)) - (awakened ? 3 : 0);
            _nextDecisionTick = s.Tick + Math.Max(3, interval) + Roll(0, 4);

            float reach = c.Radius.ToFloat() + oc.Radius.ToFloat() + 1.15f;

            // Punish openings: parried, guard broken, landing, or a long whiffed recovery.
            int window = OpeningTicks(ref opp);
            if (window > 0 && dist < reach + 0.8f && Chance(Lerp(0.35f, 1f, t)))
            {
                if (window > 24 && me.Grounded) _pulse = InputButtons.Heavy;
                else
                {
                    _pulse = InputButtons.Light;
                    _comboGoal = 3;
                }
                return;
            }

            if (opp.Action == FighterAction.Knockdown || opp.Action == FighterAction.GetUp || opp.Action == FighterAction.KO)
            {
                // Wait next to them instead of attacking into wake-up invulnerability.
                return;
            }

            if (dist > 9f && me.Grounded && me.CooldownSkill2 == 0 && Chance(Lerp(0.08f, 0.7f, t)))
            {
                _pulse = InputButtons.Skill2;
                return;
            }

            if (dist > reach + 0.6f)
            {
                // Close in. Sometimes leap in with a plunging attack.
                if (dist < 6f && me.Grounded && Chance(0.06f)) Hold(s, InputButtons.Jump, 2);
                else if (!me.Grounded && dist < 3.5f) _pulse = InputButtons.Heavy;
                return;
            }

            if (!me.Grounded)
            {
                _pulse = Chance(0.5f) ? InputButtons.Heavy : InputButtons.Light;
                return;
            }

            // A player who likes to attack first (faster startup than the beast) is better met with a guard:
            // the beast waits, deflects, and punishes the recovery.
            float pAttack = Predict(PlayerAct.Light, ref me, dist) + Predict(PlayerAct.Heavy, ref me, dist) + Predict(PlayerAct.Skill1, ref me, dist)
                + Predict(PlayerAct.Skill2, ref me, dist) + Predict(PlayerAct.Ultimate, ref me, dist) + Predict(PlayerAct.CrouchAttack, ref me, dist);
            float wait = Math.Min(0.85f, pAttack) * Trust * (awakened ? 0.45f : 0.9f);
            if (opp.Action != FighterAction.Attack && Memory.Observations > 6f && Chance(wait))
            {
                Hold(s, InputButtons.Block, 10 + Roll(0, 14));
                _nextDecisionTick = s.Tick + 10;
                return;
            }

            ChooseCloseAttack(s, ref me, ref opp, c, dist);
        }

        /// <summary>Picks the attack that beats the player's usual defense, mixed with some randomness.</summary>
        private void ChooseCloseAttack(GameState s, ref FighterState me, ref FighterState opp, CharacterDef c, float dist)
        {
            float total = 0f;
            for (int i = 0; i < BeastMemory.Acts; i++) total += Memory.Defense[i];
            float block = Share(PlayerAct.Block, total);
            float low = Share(PlayerAct.CrouchGuard, total);
            float dodge = Share(PlayerAct.Dodge, total) + Share(PlayerAct.Jump, total) * 0.6f;
            float mash = Share(PlayerAct.Light, total) + Share(PlayerAct.Heavy, total) + Share(PlayerAct.Skill1, total) * 0.5f + Share(PlayerAct.Skill2, total) * 0.5f;
            float nothing = Share(PlayerAct.None, total);
            float trust = total < 4f ? 0f : Trust;

            // Base weights for an unknown player, then shifted toward what counters this player.
            float wLight = 30f + trust * (60f * nothing + 25f * dodge);
            float wHeavy = 14f + trust * 70f * block;
            float wGrab = 10f + trust * (45f * block + 70f * low);
            float wSweep = 10f + trust * 65f * block;
            float wSlam = me.CooldownSkill1 == 0 ? 4f + 6f * T + trust * (70f * low + 50f * dodge) : 0f;
            float wBait = 6f + trust * 80f * mash;
            float wUlt = me.CooldownUltimate == 0 ? (2f + 4f * T) + (opp.Health * 4 < GameContent.GetCharacter(opp.CharacterId).MaxHealth ? 14f * T : 0f) : 0f;

            float sum = wLight + wHeavy + wGrab + wSweep + wSlam + wBait + wUlt;
            float r = Roll(0, 10000) / 10000f * sum;
            if ((r -= wLight) < 0f)
            {
                _pulse = InputButtons.Light;
                _comboGoal = Chance(Lerp(0.5f, 0.95f, T)) ? 3 : 1 + Roll(0, 2);
            }
            else if ((r -= wHeavy) < 0f)
            {
                _pulse = InputButtons.Heavy;
            }
            else if ((r -= wGrab) < 0f)
            {
                _pulse = InputButtons.Grab;
            }
            else if ((r -= wSweep) < 0f)
            {
                Hold(s, InputButtons.Crouch, 12);
                _pulse = InputButtons.Light;
                _comboGoal = 0;
            }
            else if ((r -= wSlam) < 0f)
            {
                _pulse = InputButtons.Skill1;
            }
            else if ((r -= wBait) < 0f)
            {
                // Bait: wait behind a guard for the player to swing, parry it, then punish.
                Hold(s, InputButtons.Block, 4 + Roll(0, 10));
                _nextDecisionTick = s.Tick + 12;
            }
            else
            {
                _pulse = InputButtons.Ultimate;
            }
        }

        private float Share(PlayerAct act, float total)
        {
            return total > 0f ? Memory.Defense[(int)act] / total : 0f;
        }

        /// <summary>Keeps pressing light attack inside the cancel window until the planned combo length is reached.</summary>
        private void ContinueCombo(ref FighterState me, CharacterDef c)
        {
            if (_comboGoal <= 0 || me.Action != FighterAction.Attack) return;
            MoveDef m = GameContent.GetMove(me.MoveId);
            if (m == null || m.CancelStart < 0) return;
            if (me.ComboStep + 1 >= _comboGoal)
            {
                _comboGoal = 0;
                return;
            }
            if (me.MoveTick == m.CancelStart + 1) _pulse = InputButtons.Light;
        }

        /// <summary>How many ticks the opponent stays open to a punish, or 0.</summary>
        private int OpeningTicks(ref FighterState opp)
        {
            switch (opp.Action)
            {
                case FighterAction.Parried:
                case FighterAction.GuardBroken:
                case FighterAction.Landing:
                    return opp.StunTicks;
                case FighterAction.Attack:
                {
                    MoveThreat th = opp.MoveId >= 0 && opp.MoveId < _threats.Length ? _threats[opp.MoveId] : default(MoveThreat);
                    MoveDef m = GameContent.GetMove(opp.MoveId);
                    if (m == null || !th.Valid || opp.MoveTick <= th.Last) return 0;
                    int left = m.Total - opp.MoveTick;
                    return m.CancelStart >= 0 && opp.MoveTick >= m.CancelStart ? 0 : left;
                }
                default:
                    return 0;
            }
        }

        // ------------------------------------------------------------------ Helpers

        private static MoveThreat BuildThreat(MoveDef m)
        {
            var th = new MoveThreat();
            if (m == null) return th;
            if (m.Hitboxes.Length == 0 && m.Spawns.Length == 0) return th;
            th.Valid = true;
            th.First = int.MaxValue;
            th.Last = 0;
            float travel = 0f;
            for (int i = 0; i < m.Motions.Length; i++)
            {
                travel += Math.Max(0f, m.Motions[i].Forward.ToFloat()) * (m.Motions[i].End - m.Motions[i].Start + 1);
            }
            if (m.Steerable) travel += m.SteerSpeed.ToFloat() * m.Total * 0.5f;
            for (int i = 0; i < m.Hitboxes.Length; i++)
            {
                HitboxDef hb = m.Hitboxes[i];
                th.First = Math.Min(th.First, hb.Start);
                th.Last = Math.Max(th.Last, hb.End);
                th.Reach = Math.Max(th.Reach, hb.Forward.ToFloat() + hb.Radius.ToFloat() + travel + 0.5f);
                if ((hb.Flags & HitFlags.Low) != 0) th.Low = true;
                if ((hb.Flags & HitFlags.Grab) != 0) th.Grab = true;
                if ((hb.Flags & HitFlags.Unblockable) != 0) th.Unblockable = true;
            }
            if (m.Spawns.Length > 0)
            {
                th.Ranged = true;
                for (int i = 0; i < m.Spawns.Length; i++)
                {
                    th.First = Math.Min(th.First, m.Spawns[i].Tick);
                    th.Last = Math.Max(th.Last, m.Spawns[i].Tick);
                    ProjectileDef pd = GameContent.Projectiles[m.Spawns[i].ProjectileId];
                    th.ProjectileSpeed = Math.Max(th.ProjectileSpeed, pd.Speed.ToFloat());
                    th.SpawnForward = pd.SpawnForward.ToFloat();
                }
                th.Reach = Math.Max(th.Reach, 30f);
            }
            if (th.First == int.MaxValue) th.First = 0;
            return th;
        }

        private void Release(GameState s)
        {
            _held = InputButtons.None;
            _holdUntilTick = s.Tick;
        }

        private void Hold(GameState s, InputButtons buttons, int ticks)
        {
            _held = buttons;
            _holdUntilTick = s.Tick + ticks;
        }

        private bool Chance(float p)
        {
            return Roll(0, 10000) < (int)(Math.Max(0f, Math.Min(1f, p)) * 10000f);
        }

        private int Roll(int min, int max)
        {
            return DeterministicRandom.Range(ref _rng, min, max);
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Math.Max(0f, Math.Min(1f, t));
        }
    }
}
