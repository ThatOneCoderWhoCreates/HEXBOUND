namespace HEXBOUND.Simulation
{
    /// <summary>Per-tick fighter update: input, state machine, moves and physics.</summary>
    public static class FighterLogic
    {
        private static readonly Fix FlightBlend = Fix.Milli(250);
        private static readonly Fix AirControl = Fix.Milli(120);
        private static readonly Fix FloorSnapTolerance = Fix.Milli(-250);
        private static readonly Fix EdgeMargin = Fix.Milli(300);
        private static readonly Fix VoidCatchHeight = Fix.FromInt(-1);

        public static void ResetForRound(ref FighterState f, int index)
        {
            CharacterDef c = GameContent.GetCharacter(f.CharacterId);
            InputButtons prev = f.PrevButtons;
            byte charId = f.CharacterId;
            f = default(FighterState);
            f.CharacterId = charId;
            f.PrevButtons = prev;
            f.Action = FighterAction.Idle;
            f.MoveId = -1;
            f.Position = index == 0 ? Arena.Spawn0 : Arena.Spawn1;
            f.Facing = index == 0 ? 0 : FixMath.HalfTurn;
            f.Grounded = true;
            f.Health = c.MaxHealth;
            f.Magic = c.MaxMagic;
            f.WeaponsHeld = (byte)c.WeaponMask;
        }

        /// <summary>True when the fighter has the weapon of its current mode in hand (always true for fist fighters).</summary>
        public static bool IsArmed(ref FighterState f, CharacterDef c)
        {
            if (!c.CanThrowWeapon) return true;
            return (f.WeaponsHeld & (1 << f.WeaponMode)) != 0;
        }

        /// <summary>Speed multiplier for running and air control: faster with empty hands (less weight).</summary>
        public static readonly Fix UnarmedSpeedScale = Fix.Milli(1200);
        /// <summary>Walking speed as a fraction of running speed (players walk while holding Shift).</summary>
        public static readonly Fix WalkSpeedScale = Fix.Milli(500);

        public static bool IsFlying(ref FighterState f)
        {
            return f.Buff == BuffId.Levitate && (f.Action == FighterAction.Air || f.Action == FighterAction.Attack);
        }

        public static bool IsInvulnerable(ref FighterState f)
        {
            if (f.InvulnTicks > 0) return true;
            if (f.Action == FighterAction.Knockdown || f.Action == FighterAction.GetUp) return true;
            if (f.Action == FighterAction.KO || f.Action == FighterAction.RingOut) return true;
            if (f.Action == FighterAction.Attack)
            {
                MoveDef m = GameContent.GetMove(f.MoveId);
                if (m != null && m.InvulnStart >= 0 && f.MoveTick >= m.InvulnStart && f.MoveTick <= m.InvulnEnd) return true;
            }
            return false;
        }

        /// <summary>States where the player's own movement must not carry them off the arena edge.</summary>
        private static bool IsVoluntary(FighterAction action)
        {
            switch (action)
            {
                case FighterAction.Idle:
                case FighterAction.Move:
                case FighterAction.Air:
                case FighterAction.Attack:
                case FighterAction.Block:
                case FighterAction.BlockStun:
                case FighterAction.Landing:
                case FighterAction.Parried:
                case FighterAction.GuardBroken:
                case FighterAction.GetUp:
                case FighterAction.Crouch:
                case FighterAction.CrouchBlock:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>True while the fighter is low (crouch, low guard, crouch attack): attacks centered high miss.</summary>
        public static bool IsCrouched(ref FighterState f)
        {
            switch (f.Action)
            {
                case FighterAction.Crouch:
                case FighterAction.CrouchBlock:
                    return true;
                case FighterAction.BlockStun:
                    return f.LowGuardStun;
                case FighterAction.Attack:
                    MoveDef m = GameContent.GetMove(f.MoveId);
                    return m != null && m.Crouching;
                default:
                    return false;
            }
        }

        /// <summary>The fighter's movement speed multiplier from its ultimate buff (Twin Blades, Awakened).</summary>
        private static Fix SpeedScale(ref FighterState f, CharacterDef c)
        {
            Fix scale = f.Buff != BuffId.None && f.Buff == c.UltimateBuff ? c.BuffSpeedScale : Fix.One;
            if (!IsArmed(ref f, c)) scale = scale * UnarmedSpeedScale;
            return scale;
        }

        public static void Step(GameState s, int index, InputFrame input, ref FighterState opponentAtStart, EventBuffer ev)
        {
            ref FighterState f = ref s.Fighters[index];
            CharacterDef c = GameContent.GetCharacter(f.CharacterId);

            InputButtons pressed = input.Buttons & ~f.PrevButtons;
            f.PrevButtons = input.Buttons;
            f.HasAim = input.HasAim;
            f.AimYaw = input.AimYaw;
            f.AimPitch = input.AimPitch;

            if ((pressed & InputButtons.LockOn) != 0) f.LockOn = !f.LockOn;
            if (f.LockOn)
            {
                FixVec3 toOpp = (opponentAtStart.Position - f.Position).Horizontal();
                if (toOpp.LengthSquared() > GameConfig.LockOnRange * GameConfig.LockOnRange) f.LockOn = false;
            }

            if ((pressed & InputButtons.SwitchWeapon) != 0 && c.SecondaryWeapon != null && f.Action != FighterAction.Attack)
            {
                f.WeaponMode = (byte)(f.WeaponMode == 0 ? 1 : 0);
                f.ComboStep = 0;
                if (ev != null) ev.Add(s.Tick, SimEventType.WeaponSwitch, index, f.WeaponMode, f.Position);
            }

            InputButtons action = PickAction(pressed);
            if (action != InputButtons.None)
            {
                f.BufferedButton = action;
                f.BufferTicks = GameConfig.InputBufferTicks;
            }
            else if (f.BufferTicks > 0)
            {
                f.BufferTicks--;
                if (f.BufferTicks == 0) f.BufferedButton = InputButtons.None;
            }

            // G acts on release: a tap drops the weapon at your feet, holding it first throws the weapon.
            if (c.CanThrowWeapon)
            {
                if (input.Held(InputButtons.Throw))
                {
                    if (f.ThrowHold < 255) f.ThrowHold++;
                }
                else if (f.ThrowHold > 0)
                {
                    f.ThrowCharged = f.ThrowHold >= ThrowHoldTicks;
                    f.ThrowHold = 0;
                    f.BufferedButton = InputButtons.Throw;
                    f.BufferTicks = GameConfig.InputBufferTicks;
                }
            }

            TickTimers(ref f, s, index, ev);

            if (f.HitStop > 0)
            {
                f.HitStop--;
                return;
            }

            f.ActionTicks++;
            FixVec3 previous = f.Position;

            if (ShouldAwaken(ref f, c))
            {
                f.Awakened = true;
                f.BufferedButton = InputButtons.None;
                f.BufferTicks = 0;
                f.ComboCount = 0;
                f.JuggleCount = 0;
                f.ComboDamage = 0;
                f.LockOn = true;
                if (ev != null) ev.Add(s.Tick, SimEventType.Awaken, index, 0, f.Position);
                StartMove(s, index, ref f, c, c.AwakenMove, 0, input, ref opponentAtStart, ev);
                Integrate(s, index, ref f, c, input, previous, ev);
                return;
            }

            switch (f.Action)
            {
                case FighterAction.Idle:
                case FighterAction.Move:
                case FighterAction.Block:
                case FighterAction.Crouch:
                case FighterAction.CrouchBlock:
                    GroundActionable(s, index, ref f, c, input, ref opponentAtStart, ev);
                    break;
                case FighterAction.Air:
                    AirActionable(s, index, ref f, c, input, ref opponentAtStart, ev);
                    break;
                case FighterAction.Attack:
                    ProcessMove(s, index, ref f, c, input, ref opponentAtStart, ev);
                    break;
                case FighterAction.HitStun:
                case FighterAction.BlockStun:
                case FighterAction.GuardBroken:
                case FighterAction.Parried:
                case FighterAction.Landing:
                case FighterAction.GetUp:
                    ProcessStun(s, index, ref f, c, input, ref opponentAtStart, ev);
                    break;
                case FighterAction.Knockdown:
                    ApplyGroundFriction(ref f);
                    f.StunTicks--;
                    if (f.StunTicks <= 0)
                    {
                        SetAction(ref f, FighterAction.GetUp);
                        f.StunTicks = GameConfig.GetUpTicks;
                        f.InvulnTicks = GameConfig.GetUpInvuln;
                    }
                    break;
                case FighterAction.Launched:
                    f.Velocity.X = f.Velocity.X * GameConfig.AirFriction;
                    f.Velocity.Z = f.Velocity.Z * GameConfig.AirFriction;
                    break;
                case FighterAction.KO:
                    if (f.Grounded) ApplyGroundFriction(ref f);
                    break;
                case FighterAction.RingOut:
                case FighterAction.Victory:
                    if (f.Grounded) ApplyGroundFriction(ref f);
                    break;
            }

            Integrate(s, index, ref f, c, input, previous, ev);
        }

        private static InputButtons PickAction(InputButtons pressed)
        {
            if ((pressed & InputButtons.Ultimate) != 0) return InputButtons.Ultimate;
            if ((pressed & InputButtons.Skill2) != 0) return InputButtons.Skill2;
            if ((pressed & InputButtons.Skill1) != 0) return InputButtons.Skill1;
            if ((pressed & InputButtons.Grab) != 0) return InputButtons.Grab;
            if ((pressed & InputButtons.Heavy) != 0) return InputButtons.Heavy;
            if ((pressed & InputButtons.Light) != 0) return InputButtons.Light;
            if ((pressed & InputButtons.Dodge) != 0) return InputButtons.Dodge;
            if ((pressed & InputButtons.Jump) != 0) return InputButtons.Jump;
            if ((pressed & InputButtons.Recharge) != 0) return InputButtons.Recharge;
            if ((pressed & InputButtons.Heal) != 0) return InputButtons.Heal;
            return InputButtons.None;
        }

        private static void TickTimers(ref FighterState f, GameState s, int index, EventBuffer ev)
        {
            if (f.CooldownSkill1 > 0) f.CooldownSkill1--;
            if (f.CooldownSkill2 > 0) f.CooldownSkill2--;
            if (f.CooldownUltimate > 0) f.CooldownUltimate--;
            if (f.CooldownRecharge > 0) f.CooldownRecharge--;
            if (f.CooldownDodge > 0) f.CooldownDodge--;
            if (f.CooldownHeal > 0) f.CooldownHeal--;
            if (f.CooldownShot > 0) f.CooldownShot--;
            if (f.ComboGrace > 0) f.ComboGrace--;
            if (f.InvulnTicks > 0) f.InvulnTicks--;
            if (f.RehitTimer > 0) f.RehitTimer--;
            if (f.Buff == BuffId.Levitate && !s.Setup.InfiniteMagic && s.Tick % GameConfig.FlightDrainEvery != 0)
            {
                // Flying burns magic; when it runs out, the flight ends.
                f.Magic--;
                if (f.Magic <= 0)
                {
                    f.Magic = 0;
                    f.BuffTicks = 1;
                }
            }
            if (f.Buff != BuffId.None)
            {
                f.BuffTicks--;
                if (f.BuffTicks <= 0)
                {
                    if (ev != null) ev.Add(s.Tick, SimEventType.BuffEnd, index, (int)f.Buff, f.Position);
                    f.Buff = BuffId.None;
                    f.BuffTicks = 0;
                }
            }
        }

        private static void SetAction(ref FighterState f, FighterAction action)
        {
            f.Action = action;
            f.ActionTicks = 0;
            f.LowGuardStun = false;
        }

        /// <summary>The beast awakens once per round when its health falls low, as soon as it is on its feet.</summary>
        private static bool ShouldAwaken(ref FighterState f, CharacterDef c)
        {
            if (c.AwakenMove == null || f.Awakened || f.Health <= 0 || !f.Grounded) return false;
            if (f.Health * 100 > c.MaxHealth * c.AwakenPercent) return false;
            switch (f.Action)
            {
                case FighterAction.Idle:
                case FighterAction.Move:
                case FighterAction.Block:
                case FighterAction.Crouch:
                case FighterAction.CrouchBlock:
                case FighterAction.HitStun:
                case FighterAction.BlockStun:
                case FighterAction.GuardBroken:
                case FighterAction.Parried:
                case FighterAction.Landing:
                case FighterAction.Attack:
                    return true;
                default:
                    return false;
            }
        }

        private static void ApplyGroundFriction(ref FighterState f)
        {
            f.Velocity.X = f.Velocity.X * GameConfig.GroundFriction;
            f.Velocity.Z = f.Velocity.Z * GameConfig.GroundFriction;
        }

        private static int AngleTo(ref FighterState f, ref FighterState target)
        {
            return (target.Position - f.Position).ToAngle();
        }

        private static void GroundActionable(GameState s, int index, ref FighterState f, CharacterDef c, InputFrame input, ref FighterState opp, EventBuffer ev)
        {
            if (TryStartAction(s, index, ref f, c, input, ref opp, ev, false)) return;

            bool crouchHeld = input.Held(InputButtons.Crouch) && c.CrouchAttack != null;
            if (crouchHeld && input.Held(InputButtons.Heavy))
            {
                // Low guard: C + right mouse held.
                if (f.Action != FighterAction.CrouchBlock) SetAction(ref f, FighterAction.CrouchBlock);
                f.BlockTicks = GameConfig.ParryWindow + 1;
                f.Facing = FixMath.RotateTowards(f.Facing, AngleTo(ref f, ref opp), GameConfig.TurnSpeed);
                ApplyGroundFriction(ref f);
                return;
            }

            bool blockHeld = input.Held(InputButtons.Block) || (c.HeavyIsGuard && IsArmed(ref f, c) && input.Held(InputButtons.Heavy));
            if (blockHeld && !crouchHeld)
            {
                if (f.Action != FighterAction.Block)
                {
                    SetAction(ref f, FighterAction.Block);
                    f.BlockTicks = 0;
                }
                else
                {
                    f.BlockTicks++;
                }
                f.Facing = FixMath.RotateTowards(f.Facing, AngleTo(ref f, ref opp), GameConfig.TurnSpeed);
                ApplyGroundFriction(ref f);
                return;
            }

            if (crouchHeld)
            {
                // Crouch: stay low and still, turning toward the opponent when locked on.
                if (f.Action != FighterAction.Crouch) SetAction(ref f, FighterAction.Crouch);
                if (f.LockOn) f.Facing = FixMath.RotateTowards(f.Facing, AngleTo(ref f, ref opp), GameConfig.TurnSpeed);
                else if (input.HasMove) f.Facing = FixMath.RotateTowards(f.Facing, input.MoveAngleFull, GameConfig.TurnSpeed);
                ApplyGroundFriction(ref f);
                return;
            }

            if (input.HasMove)
            {
                int dir = input.MoveAngleFull;
                Fix speed = c.MoveSpeed * SpeedScale(ref f, c);
                if (!input.Held(InputButtons.Run)) speed = speed * WalkSpeedScale;
                FixVec3 v = FixVec3.FromAngle(dir) * speed;
                f.Velocity.X = v.X;
                f.Velocity.Z = v.Z;
                int faceTarget = f.LockOn ? AngleTo(ref f, ref opp) : dir;
                f.Facing = FixMath.RotateTowards(f.Facing, faceTarget, GameConfig.TurnSpeed);
                if (f.Action != FighterAction.Move) SetAction(ref f, FighterAction.Move);
            }
            else
            {
                f.Velocity.X = Fix.Zero;
                f.Velocity.Z = Fix.Zero;
                if (f.LockOn) f.Facing = FixMath.RotateTowards(f.Facing, AngleTo(ref f, ref opp), GameConfig.TurnSpeed);
                if (f.Action != FighterAction.Idle) SetAction(ref f, FighterAction.Idle);
            }
        }

        private static void AirActionable(GameState s, int index, ref FighterState f, CharacterDef c, InputFrame input, ref FighterState opp, EventBuffer ev)
        {
            if (TryStartAction(s, index, ref f, c, input, ref opp, ev, false)) return;

            bool flying = f.Buff == BuffId.Levitate;
            if (flying)
            {
                FixVec3 target = input.HasMove ? FixVec3.FromAngle(input.MoveAngleFull) * GameConfig.FlightSpeed : FixVec3.Zero;
                f.Velocity.X = f.Velocity.X + (target.X - f.Velocity.X) * FlightBlend;
                f.Velocity.Z = f.Velocity.Z + (target.Z - f.Velocity.Z) * FlightBlend;
                FlightVertical(ref f, input);
            }
            else if (input.HasMove)
            {
                FixVec3 target = FixVec3.FromAngle(input.MoveAngleFull) * (c.AirSpeed * (IsArmed(ref f, c) ? Fix.One : UnarmedSpeedScale));
                f.Velocity.X = f.Velocity.X + (target.X - f.Velocity.X) * AirControl;
                f.Velocity.Z = f.Velocity.Z + (target.Z - f.Velocity.Z) * AirControl;
            }

            if (f.LockOn) f.Facing = FixMath.RotateTowards(f.Facing, AngleTo(ref f, ref opp), GameConfig.TurnSpeed);
            else if (input.HasMove) f.Facing = FixMath.RotateTowards(f.Facing, input.MoveAngleFull, GameConfig.TurnSpeed);
        }

        private static void FlightVertical(ref FighterState f, InputFrame input)
        {
            Fix ground = Arena.GroundAt(f.Position);
            if (f.Position.Y < ground + GameConfig.FlightMinHeight)
            {
                f.Velocity.Y = GameConfig.FlightVerticalSpeed;
            }
            else if (input.Held(InputButtons.Jump) && f.Position.Y < GameConfig.FlightMaxHeight)
            {
                f.Velocity.Y = GameConfig.FlightVerticalSpeed;
            }
            else if (input.Held(InputButtons.Block) && f.Position.Y > ground + GameConfig.FlightMinHeight)
            {
                f.Velocity.Y = -GameConfig.FlightVerticalSpeed;
            }
            else
            {
                f.Velocity.Y = Fix.Zero;
            }
        }

        private static void ProcessStun(GameState s, int index, ref FighterState f, CharacterDef c, InputFrame input, ref FighterState opp, EventBuffer ev)
        {
            if (f.Grounded) ApplyGroundFriction(ref f);
            f.StunTicks--;
            if (f.StunTicks > 0) return;

            FighterAction previous = f.Action;
            f.ComboCount = 0;
            f.JuggleCount = 0;
            f.ComboDamage = 0;
            if (!f.Grounded)
            {
                SetAction(ref f, FighterAction.Air);
                return;
            }
            if (previous == FighterAction.BlockStun && input.Held(InputButtons.Crouch) && input.Held(InputButtons.Heavy) && c.CrouchAttack != null)
            {
                SetAction(ref f, FighterAction.CrouchBlock);
                f.BlockTicks = GameConfig.ParryWindow + 1;
                return;
            }
            if (previous == FighterAction.BlockStun && (input.Held(InputButtons.Block) || (c.HeavyIsGuard && IsArmed(ref f, c) && input.Held(InputButtons.Heavy))))
            {
                SetAction(ref f, FighterAction.Block);
                // Returning to block after blockstun does not open a new parry window.
                f.BlockTicks = GameConfig.ParryWindow + 1;
                return;
            }
            SetAction(ref f, FighterAction.Idle);
            GroundActionable(s, index, ref f, c, input, ref opp, ev);
        }

        /// <summary>Tries to start the buffered action. Returns true if a move or jump started.</summary>
        private static bool TryStartAction(GameState s, int index, ref FighterState f, CharacterDef c, InputFrame input, ref FighterState opp, EventBuffer ev, bool fromCancel)
        {
            InputButtons button = f.BufferedButton;
            if (button == InputButtons.None) return false;

            bool flying = f.Buff == BuffId.Levitate;
            bool air = !f.Grounded || flying;
            bool buffed = f.Buff != BuffId.None && f.Buff == c.UltimateBuff;
            bool alt = f.WeaponMode == 1 && c.SecondaryWeapon != null;
            bool armed = IsArmed(ref f, c);
            MoveDef move = null;
            int comboStep = 0;

            switch (button)
            {
                case InputButtons.Ultimate:
                    if (f.Buff == BuffId.Levitate && c.UltimateBuff == BuffId.Levitate)
                    {
                        // R while flying lands: the flight ends and the fighter falls.
                        ConsumeBuffer(ref f);
                        f.Buff = BuffId.None;
                        f.BuffTicks = 0;
                        if (ev != null) ev.Add(s.Tick, SimEventType.BuffEnd, index, (int)BuffId.Levitate, f.Position);
                        return false;
                    }
                    if (NeedsPrimary(s, index, ref f, c, ev)) return false;
                    move = c.Ultimate;
                    break;
                case InputButtons.Skill1:
                    if (NeedsPrimary(s, index, ref f, c, ev)) return false;
                    move = c.Skill1;
                    break;
                case InputButtons.Skill2:
                    if (NeedsPrimary(s, index, ref f, c, ev)) return false;
                    move = c.Skill2;
                    break;
                case InputButtons.Grab:
                    if (air) return false;
                    move = c.Grab;
                    break;
                case InputButtons.Throw:
                    // G tapped: the weapon drops straight down at the fighter's feet. G held, then released: it is thrown.
                    if (!armed || !c.CanThrowWeapon)
                    {
                        ConsumeBuffer(ref f);
                        return false;
                    }
                    move = f.ThrowCharged ? (f.WeaponMode == 1 ? c.ThrowSecondary : c.ThrowPrimary) : null;
                    if (move == null)
                    {
                        ConsumeBuffer(ref f);
                        DropWeapon(s, index, ref f, c, ev);
                        return false;
                    }
                    break;
                case InputButtons.Heavy:
                    if (!armed && !(input.Held(InputButtons.Crouch) && !air && c.CrouchAttack != null))
                    {
                        move = air ? c.FistAirHeavy : c.FistHeavy;
                        break;
                    }
                    if (!air && input.Held(InputButtons.Crouch) && c.CrouchGuardMove != null && armed)
                    {
                        move = c.CrouchGuardMove;
                        break;
                    }
                    if (!air && input.Held(InputButtons.Crouch) && c.CrouchAttack != null)
                    {
                        // C + right mouse is the low guard, handled as a held state.
                        ConsumeBuffer(ref f);
                        return false;
                    }
                    if (!air && c.HeavyIsGuard)
                    {
                        // Right mouse is this fighter's guard (held state), not an attack.
                        ConsumeBuffer(ref f);
                        return false;
                    }
                    if (air) move = alt && c.AltAirHeavy != null ? c.AltAirHeavy : (buffed && c.BuffAirHeavy != null ? c.BuffAirHeavy : c.AirHeavy);
                    else move = alt && c.AltHeavy != null ? c.AltHeavy : (buffed && c.BuffHeavy != null ? c.BuffHeavy : c.Heavy);
                    break;
                case InputButtons.Light:
                    if (!air && input.Held(InputButtons.Crouch) && c.CrouchAttack != null)
                    {
                        move = armed ? c.CrouchAttack : c.FistCrouchAttack;
                    }
                    else if (air && !armed)
                    {
                        move = c.FistAirLight;
                    }
                    else if (air)
                    {
                        move = alt && c.AltAirLight != null ? c.AltAirLight : (buffed && c.BuffAirLight != null ? c.BuffAirLight : c.AirLight);
                    }
                    else
                    {
                        MoveDef[] chain = !armed ? c.FistChain : (alt && c.AltLightChain != null ? c.AltLightChain : (buffed && c.BuffLightChain != null ? c.BuffLightChain : c.LightChain));
                        if (fromCancel && f.Action == FighterAction.Attack && f.ComboStep < chain.Length && chain[f.ComboStep].Id == f.MoveId)
                        {
                            comboStep = f.ComboStep + 1;
                            if (comboStep >= chain.Length) return false;
                        }
                        else if (!fromCancel && f.ComboGrace > 0 && f.ComboStep + 1 < chain.Length)
                        {
                            // Pressed shortly after the previous combo hit finished: continue the combo.
                            comboStep = f.ComboStep + 1;
                        }
                        move = chain[comboStep];
                    }
                    break;
                case InputButtons.Dodge:
                    if (f.CooldownDodge > 0) return false;
                    if (air)
                    {
                        if (f.AirDashUsed && !flying) return false;
                        move = c.AirDash;
                    }
                    else
                    {
                        move = c.Dodge;
                    }
                    break;
                case InputButtons.Jump:
                    if (air) return false;
                    ConsumeBuffer(ref f);
                    f.Velocity.Y = c.JumpVelocity;
                    f.Grounded = false;
                    f.MoveId = -1;
                    SetAction(ref f, FighterAction.Air);
                    if (ev != null) ev.Add(s.Tick, SimEventType.Jump, index, 0, f.Position);
                    return true;
                case InputButtons.Recharge:
                    if (f.CooldownRecharge > 0 || f.Magic >= c.MaxMagic || c.Recharge == null) return false;
                    move = c.Recharge;
                    break;
                case InputButtons.Heal:
                    if (f.CooldownHeal > 0 || f.Health >= c.MaxHealth) return false;
                    move = c.Heal;
                    break;
            }

            if (move == null) return false;
            if (air && !move.UsableInAir) return false;
            if (!air && !move.UsableOnGround) return false;
            if (!CooldownReady(ref f, move)) return false;

            bool infinite = s.Setup.InfiniteMagic;
            if (!infinite && f.Magic < move.MagicCost)
            {
                ConsumeBuffer(ref f);
                if (ev != null) ev.Add(s.Tick, SimEventType.NotEnoughMagic, index, move.Id, f.Position);
                return false;
            }

            ConsumeBuffer(ref f);
            if (!infinite) f.Magic -= move.MagicCost;
            // The rocket fire rate applies even with infinite magic (it is a weapon limit, not an ability cooldown).
            if (!infinite || move.Slot == CooldownSlot.Shot) SetCooldown(ref f, move);
            if (air && move == c.AirDash && !flying) f.AirDashUsed = true;
            StartMove(s, index, ref f, c, move, comboStep, input, ref opp, ev);
            return true;
        }

        private static void ConsumeBuffer(ref FighterState f)
        {
            f.BufferedButton = InputButtons.None;
            f.BufferTicks = 0;
        }

        private static bool CooldownReady(ref FighterState f, MoveDef move)
        {
            switch (move.Slot)
            {
                case CooldownSlot.Skill1: return f.CooldownSkill1 <= 0;
                case CooldownSlot.Skill2: return f.CooldownSkill2 <= 0;
                case CooldownSlot.Ultimate: return f.CooldownUltimate <= 0;
                case CooldownSlot.Recharge: return f.CooldownRecharge <= 0;
                case CooldownSlot.Dodge: return f.CooldownDodge <= 0;
                case CooldownSlot.Heal: return f.CooldownHeal <= 0;
                case CooldownSlot.Shot: return f.CooldownShot <= 0;
                default: return true;
            }
        }

        private static void SetCooldown(ref FighterState f, MoveDef move)
        {
            switch (move.Slot)
            {
                case CooldownSlot.Skill1: f.CooldownSkill1 = move.Cooldown; break;
                case CooldownSlot.Skill2: f.CooldownSkill2 = move.Cooldown; break;
                case CooldownSlot.Ultimate: f.CooldownUltimate = move.Cooldown; break;
                case CooldownSlot.Recharge: f.CooldownRecharge = move.Cooldown; break;
                case CooldownSlot.Dodge: f.CooldownDodge = move.Cooldown; break;
                case CooldownSlot.Heal: f.CooldownHeal = move.Cooldown; break;
                case CooldownSlot.Shot: f.CooldownShot = move.Cooldown; break;
            }
        }

        private static void StartMove(GameState s, int index, ref FighterState f, CharacterDef c, MoveDef move, int comboStep, InputFrame input, ref FighterState opp, EventBuffer ev)
        {
            SetAction(ref f, FighterAction.Attack);
            f.ComboGrace = 0;
            f.MoveId = move.Id;
            f.MoveTick = -1;
            f.HitMask = 0;
            f.PillarHitMask = 0;
            f.StructureHitMask = 0;
            f.RehitTimer = 0;
            f.ComboStep = comboStep;

            bool isMovementMove = move.Pose == MovePose.Dodge;
            if (isMovementMove)
            {
                if (input.HasMove)
                {
                    f.MoveDirection = input.MoveAngleFull;
                    if (!f.LockOn) f.Facing = input.MoveAngleFull;
                }
                else
                {
                    f.MoveDirection = move.TeleportDistance > Fix.Zero ? f.Facing : FixMath.NormalizeAngle(f.Facing + FixMath.HalfTurn);
                }
            }
            else
            {
                int intended = input.HasMove ? input.MoveAngleFull : f.Facing;
                if (move.SoftTarget && ShouldTarget(ref f, ref opp, intended)) intended = AngleTo(ref f, ref opp);
                f.Facing = intended;
                f.MoveDirection = intended;
            }

            if (ev != null) ev.Add(s.Tick, move.Pose == MovePose.Dodge && move.TeleportDistance == Fix.Zero ? SimEventType.Dodge : SimEventType.MoveStart, index, move.Id, f.Position);
            ProcessMove(s, index, ref f, c, input, ref opp, ev);
        }

        private static bool ShouldTarget(ref FighterState f, ref FighterState opp, int intendedAngle)
        {
            if (opp.Action == FighterAction.KO || opp.Action == FighterAction.RingOut) return false;
            if (f.LockOn) return true;
            FixVec3 to = (opp.Position - f.Position).Horizontal();
            if (to.LengthSquared() > GameConfig.SoftTargetRange * GameConfig.SoftTargetRange) return false;
            return FixMath.AbsAngleDelta(intendedAngle, to.ToAngle()) <= GameConfig.SoftTargetArc;
        }

        private static void ProcessMove(GameState s, int index, ref FighterState f, CharacterDef c, InputFrame input, ref FighterState opp, EventBuffer ev)
        {
            MoveDef m = GameContent.GetMove(f.MoveId);
            if (m == null || f.MoveTick + 1 >= m.Total)
            {
                EndMove(ref f);
                if (f.Action == FighterAction.Air) AirActionable(s, index, ref f, c, input, ref opp, ev);
                else GroundActionable(s, index, ref f, c, input, ref opp, ev);
                return;
            }

            f.MoveTick++;
            int t = f.MoveTick;

            // Horizontal motion.
            if (m.Steerable)
            {
                FixVec3 steer = input.HasMove ? FixVec3.FromAngle(input.MoveAngleFull) * m.SteerSpeed : FixVec3.Zero;
                f.Velocity.X = steer.X;
                f.Velocity.Z = steer.Z;
            }
            else
            {
                bool moved = false;
                for (int i = 0; i < m.Motions.Length; i++)
                {
                    MotionDef md = m.Motions[i];
                    if (t < md.Start || t > md.End) continue;
                    FixVec3 v = FixVec3.FromAngle(f.MoveDirection) * md.Forward;
                    f.Velocity.X = v.X;
                    f.Velocity.Z = v.Z;
                    if (md.SetVertical) f.Velocity.Y = md.Up;
                    moved = true;
                }
                if (!moved)
                {
                    if (f.Grounded) ApplyGroundFriction(ref f);
                    else
                    {
                        f.Velocity.X = f.Velocity.X * GameConfig.AirFriction;
                        f.Velocity.Z = f.Velocity.Z * GameConfig.AirFriction;
                    }
                }
            }

            if (f.Buff == BuffId.Levitate && !HasVerticalMotion(m, t)) FlightVertical(ref f, input);

            if (m.TeleportDistance > Fix.Zero && t == m.TeleportTick)
            {
                FixVec3 from = f.Position;
                FixVec3 to = from + FixVec3.FromAngle(f.MoveDirection) * m.TeleportDistance;
                if (Arena.IsInside(from) && !Arena.IsInside(to, EdgeMargin)) to = Arena.ClampInside(to, EdgeMargin);
                f.Position = to;
                if (ev != null) ev.Add(s.Tick, SimEventType.Teleport, index, m.Id, from);
            }

            if (m.ApplyBuff != BuffId.None && t == m.BuffTick)
            {
                f.Buff = m.ApplyBuff;
                f.BuffTicks = m.BuffDuration;
                if (ev != null) ev.Add(s.Tick, SimEventType.BuffStart, index, (int)m.ApplyBuff, f.Position);
            }

            if (m.MagicGain > 0 && t == m.MagicGainTick)
            {
                f.Magic += m.MagicGain;
                if (f.Magic > c.MaxMagic) f.Magic = c.MaxMagic;
                if (ev != null) ev.Add(s.Tick, SimEventType.Recharge, index, m.MagicGain, f.Position);
            }

            if (m.HealPercent > 0 && t == m.HealTick && f.Health > 0)
            {
                int before = f.Health;
                f.Health += c.MaxHealth * m.HealPercent / 100;
                if (f.Health > c.MaxHealth) f.Health = c.MaxHealth;
                if (ev != null) ev.Add(s.Tick, SimEventType.Heal, index, f.Health - before, f.Position);
            }

            for (int i = 0; i < m.Spawns.Length; i++)
            {
                if (m.Spawns[i].Tick != t) continue;
                int slot = ProjectileLogic.Spawn(s, index, m.Spawns[i], ref opp, ev);
                if (m.ThrowsWeapon) ReleaseThrownWeapon(s, index, ref f, c, slot, ev);
                else if (slot >= 0 && ApplyRecoil(s, index, ref f, slot, ev)) return;
            }

            if (m.CancelStart >= 0 && t >= m.CancelStart && t <= m.CancelEnd)
            {
                TryStartAction(s, index, ref f, c, input, ref opp, ev, true);
            }
        }

        /// <summary>The thrown weapon leaves the hand: it flies as the projectile in the slot; Vex switches to her other weapon if she still has it.</summary>
        private static void ReleaseThrownWeapon(GameState s, int index, ref FighterState f, CharacterDef c, int slot, EventBuffer ev)
        {
            int bit = 1 << f.WeaponMode;
            if ((f.WeaponsHeld & bit) == 0) return;
            f.WeaponsHeld = (byte)(f.WeaponsHeld & ~bit);
            f.PickupReady = (byte)(f.PickupReady & ~bit);
            if (slot >= 0)
            {
                s.Projectiles[slot].Carries = (byte)(f.WeaponMode + 1);
                f.WeaponsThrown = (byte)(f.WeaponsThrown | bit);
                SetDropPos(ref f, f.WeaponMode, s.Projectiles[slot].Position);
            }
            else
            {
                // No free projectile slot: the weapon just drops at the fighter's feet.
                f.WeaponsDropped = (byte)(f.WeaponsDropped | bit);
                SetDropPos(ref f, f.WeaponMode, new FixVec3(f.Position.X, Fix.Zero, f.Position.Z));
            }
            int other = 1 - f.WeaponMode;
            if (c.SecondaryWeapon != null && (f.WeaponsHeld & (1 << other)) != 0)
            {
                f.WeaponMode = (byte)other;
                if (ev != null) ev.Add(s.Tick, SimEventType.WeaponSwitch, index, f.WeaponMode, f.Position);
            }
            if (ev != null) ev.Add(s.Tick, SimEventType.WeaponThrow, index, bit, f.Position);
        }

        private static void SetDropPos(ref FighterState f, int weapon, FixVec3 p)
        {
            if (weapon == 0) f.DropPos0 = p;
            else f.DropPos1 = p;
        }

        private static FixVec3 GetDropPos(ref FighterState f, int weapon)
        {
            return weapon == 0 ? f.DropPos0 : f.DropPos1;
        }

        /// <summary>Holding G at least this long (0.3 s) before releasing throws the weapon instead of dropping it.</summary>
        public const int ThrowHoldTicks = 18;

        /// <summary>Q, E and R belong to the primary weapon: without it they cannot be used (the press is used up).</summary>
        private static bool NeedsPrimary(GameState s, int index, ref FighterState f, CharacterDef c, EventBuffer ev)
        {
            if (!c.CanThrowWeapon || (f.WeaponsHeld & 1) != 0) return false;
            ConsumeBuffer(ref f);
            if (ev != null) ev.Add(s.Tick, SimEventType.NeedWeapon, index, 0, f.Position);
            return true;
        }

        /// <summary>The weapon in hand drops to the floor right under the fighter (lost if that is off the arena).</summary>
        private static void DropWeapon(GameState s, int index, ref FighterState f, CharacterDef c, EventBuffer ev)
        {
            int w = f.WeaponMode;
            int bit = 1 << w;
            f.WeaponsHeld = (byte)(f.WeaponsHeld & ~bit);
            f.PickupReady = (byte)(f.PickupReady & ~bit);
            FixVec3 at = new FixVec3(f.Position.X, Arena.GroundAt(f.Position), f.Position.Z);
            if (Arena.IsInside(at, Fix.Milli(200)))
            {
                SetDropPos(ref f, w, at);
                f.WeaponsDropped = (byte)(f.WeaponsDropped | bit);
                if (ev != null) ev.Add(s.Tick, SimEventType.WeaponDrop, index, bit, f.Position);
            }
            else if (ev != null)
            {
                ev.Add(s.Tick, SimEventType.WeaponLost, index, bit, f.Position);
            }
            int other = 1 - w;
            if (c.SecondaryWeapon != null && (f.WeaponsHeld & (1 << other)) != 0)
            {
                f.WeaponMode = (byte)other;
                if (ev != null) ev.Add(s.Tick, SimEventType.WeaponSwitch, index, f.WeaponMode, f.Position);
            }
        }

        /// <summary>Walking over your own dropped weapon picks it up, once you have stepped away from where you dropped it.</summary>
        public static readonly Fix PickUpRange = Fix.Milli(900);
        private static readonly Fix PickUpArmRange = Fix.Milli(1400);

        private static void AutoPickUp(GameState s, int index, ref FighterState f, CharacterDef c, EventBuffer ev)
        {
            if (f.WeaponsDropped == 0) return;
            bool able = f.Grounded && f.Action != FighterAction.KO && f.Action != FighterAction.RingOut && f.Action != FighterAction.Knockdown;
            for (int w = 0; w < 2; w++)
            {
                int bit = 1 << w;
                if ((f.WeaponsDropped & bit) == 0) continue;
                FixVec3 d = (GetDropPos(ref f, w) - f.Position).Horizontal();
                Fix distSq = d.LengthSquared();
                // On another level (a platform above or below): too far to reach.
                if (Fix.Abs(GetDropPos(ref f, w).Y - f.Position.Y) > Fix.One) distSq = distSq + Fix.FromInt(100);
                if (distSq > PickUpArmRange * PickUpArmRange)
                {
                    f.PickupReady = (byte)(f.PickupReady | bit);
                    continue;
                }
                if ((f.PickupReady & bit) == 0 || !able || distSq > PickUpRange * PickUpRange) continue;
                bool wasUnarmed = !IsArmed(ref f, c);
                f.WeaponsDropped = (byte)(f.WeaponsDropped & ~bit);
                f.WeaponsHeld = (byte)(f.WeaponsHeld | bit);
                if (wasUnarmed && f.WeaponMode != w && c.SecondaryWeapon != null)
                {
                    f.WeaponMode = (byte)w;
                    if (ev != null) ev.Add(s.Tick, SimEventType.WeaponSwitch, index, f.WeaponMode, f.Position);
                }
                if (ev != null) ev.Add(s.Tick, SimEventType.WeaponPickup, index, bit, f.Position);
            }
        }

        /// <summary>
        /// After the projectiles moved: thrown weapons that stopped flying land where they were last seen, if that is
        /// over the floor; otherwise they are lost for the rest of the round.
        /// </summary>
        public static void UpdateThrownWeapons(GameState s, EventBuffer ev)
        {
            for (int i = 0; i < 2; i++)
            {
                ref FighterState f = ref s.Fighters[i];
                AutoPickUp(s, i, ref f, GameContent.GetCharacter(f.CharacterId), ev);
                if (f.WeaponsThrown == 0) continue;
                for (int w = 0; w < 2; w++)
                {
                    int bit = 1 << w;
                    if ((f.WeaponsThrown & bit) == 0) continue;
                    bool flying = false;
                    for (int k = 0; k < s.Projectiles.Length; k++)
                    {
                        ref ProjectileState p = ref s.Projectiles[k];
                        if (!p.Active || p.Owner != i || p.Carries != w + 1) continue;
                        flying = true;
                        SetDropPos(ref f, w, p.Position);
                    }
                    if (flying) continue;
                    f.WeaponsThrown = (byte)(f.WeaponsThrown & ~bit);
                    FixVec3 at = GetDropPos(ref f, w);
                    if (Arena.IsInside(at, Fix.Milli(300)) && at.Y > GameConfig.RingOutHeight)
                    {
                        at = Arena.ClampInside(at, Fix.Milli(400));
                        FixVec3 pushed;
                        Arena.PushOutOfTerrain(ref at, Fix.Milli(300), out pushed);
                        at.Y = Arena.GroundAt(at);
                        SetDropPos(ref f, w, at);
                        f.WeaponsDropped = (byte)(f.WeaponsDropped | bit);
                        if (ev != null) ev.Add(s.Tick, SimEventType.WeaponDrop, i, bit, at);
                    }
                    else if (ev != null)
                    {
                        ev.Add(s.Tick, SimEventType.WeaponLost, i, bit, at);
                    }
                }
            }
        }

        /// <summary>Most recoil boosts from downward shots before landing again.</summary>
        public const int MaxRecoilJumps = 2;

        /// <summary>
        /// A crosshair-aimed shot fired steeply downward pushes the shooter up (and a little back): a rocket jump.
        /// Returns true if the shooter was thrown into the air (the move ends).
        /// </summary>
        private static bool ApplyRecoil(GameState s, int index, ref FighterState f, int slot, EventBuffer ev)
        {
            ProjectileDef def = GameContent.Projectiles[s.Projectiles[slot].DefId];
            if (!def.UseAim || def.RecoilLift <= Fix.Zero || !f.HasAim) return false;
            int pitch = ProjectileLogic.AimPitchAngle(f.AimPitch);
            if (pitch > -FixMath.FullTurn * 35 / 360) return false;
            if (!f.Grounded && f.RecoilJumps >= MaxRecoilJumps) return false;
            Fix down = -FixMath.Sin(pitch);
            Fix lift = def.RecoilLift * down;
            if (f.Velocity.Y < lift) f.Velocity.Y = lift;
            FixVec3 back = FixVec3.FromAngle(f.AimYaw << 8) * (def.RecoilLift * FixMath.Cos(pitch) * Fix.Milli(400));
            f.Velocity.X = f.Velocity.X - back.X;
            f.Velocity.Z = f.Velocity.Z - back.Z;
            f.Grounded = false;
            f.RecoilJumps++;
            f.MoveId = -1;
            f.MoveTick = 0;
            SetAction(ref f, FighterAction.Air);
            if (ev != null) ev.Add(s.Tick, SimEventType.RecoilJump, index, 0, f.Position);
            return true;
        }

        private static bool IsChainMove(CharacterDef c, int moveId)
        {
            MoveDef[][] chains = { c.LightChain, c.BuffLightChain, c.AltLightChain, c.FistChain };
            foreach (MoveDef[] chain in chains)
            {
                if (chain == null) continue;
                for (int i = 0; i < chain.Length; i++)
                {
                    if (chain[i] != null && chain[i].Id == moveId) return true;
                }
            }
            return false;
        }

        private static bool HasVerticalMotion(MoveDef m, int t)
        {
            for (int i = 0; i < m.Motions.Length; i++)
            {
                if (m.Motions[i].SetVertical && t >= m.Motions[i].Start && t <= m.Motions[i].End) return true;
            }
            return false;
        }

        private static void EndMove(ref FighterState f)
        {
            // A finished combo hit leaves a short window to continue the combo.
            CharacterDef c = GameContent.GetCharacter(f.CharacterId);
            f.ComboGrace = IsChainMove(c, f.MoveId) ? GameConfig.ComboGraceTicks : 0;
            f.MoveId = -1;
            f.MoveTick = 0;
            bool flying = f.Buff == BuffId.Levitate;
            SetAction(ref f, f.Grounded && !flying ? FighterAction.Idle : FighterAction.Air);
        }

        private static void Integrate(GameState s, int index, ref FighterState f, CharacterDef c, InputFrame input, FixVec3 previous, EventBuffer ev)
        {
            bool flying = IsFlying(ref f);
            if (!f.Grounded && !flying)
            {
                Fix gravity = c.Gravity;
                if (f.Action == FighterAction.Attack)
                {
                    MoveDef m = GameContent.GetMove(f.MoveId);
                    if (m != null)
                    {
                        if (HasVerticalMotion(m, f.MoveTick)) gravity = Fix.Zero;
                        else gravity = gravity * m.GravityScale;
                    }
                }
                else if (f.Action == FighterAction.Launched)
                {
                    gravity = gravity + GameConfig.JuggleGravityStep * f.JuggleCount;
                }
                f.Velocity.Y = f.Velocity.Y - gravity;
            }

            FixVec3 next = f.Position + f.Velocity;

            bool wasInside = Arena.IsInside(previous);
            if (IsVoluntary(f.Action) && wasInside && next.Y > VoidCatchHeight && !Arena.IsInside(next, EdgeMargin))
            {
                FixVec3 clamped = Arena.ClampInside(next, EdgeMargin);
                next.X = clamped.X;
                next.Z = clamped.Z;
            }
            else if (wasInside && f.Action != FighterAction.RingOut && Arena.IsWallAt(next) && !Arena.IsInside(next, EdgeMargin))
            {
                // Invisible wall (for example the Sanctuary stairs): stop at the edge and lose the outward speed.
                FixVec3 clamped = Arena.ClampInside(next, EdgeMargin);
                next.X = clamped.X;
                next.Z = clamped.Z;
                FixVec3 outward = clamped.Horizontal();
                Fix len = outward.HorizontalLength();
                if (len > Fix.Zero)
                {
                    outward = outward / len;
                    Fix speedOut = f.Velocity.X * outward.X + f.Velocity.Z * outward.Z;
                    if (speedOut > Fix.Zero)
                    {
                        f.Velocity.X = f.Velocity.X - outward.X * speedOut;
                        f.Velocity.Z = f.Velocity.Z - outward.Z * speedOut;
                    }
                }
            }

            // Raised blocks (platforms, walls, stairs): stopped by the sides you cannot step onto.
            if (f.Action != FighterAction.RingOut)
            {
                FixVec3 wallNormal;
                // On foot a fighter climbs low ledges (stairs); in the air only the very top edge, so nobody passes
                // through the top of a wall while flying past it.
                Fix climb = f.Grounded ? Arena.StepHeight : Fix.Milli(120);
                // Falling onto a platform or a wall top: stop on it before checking the sides, so a fast fall
                // never sinks into the block and gets pushed out sideways.
                if (f.Velocity.Y <= Fix.Zero && !IsFlying(ref f) && Arena.IsInside(next))
                {
                    Fix top = Arena.GroundAt(new FixVec3(next.X, previous.Y, next.Z));
                    if (next.Y < top && previous.Y >= top - climb) next.Y = top;
                }
                // Rising into a roof or a bridge deck from below: the head stops at its underside.
                if (f.Velocity.Y > Fix.Zero && Arena.HitCeiling(ref next, previous.Y, c.Height)) f.Velocity.Y = Fix.Zero;
                if (Arena.PushOutOfTerrain(ref next, c.Radius, out wallNormal, climb, c.Height))
                {
                    Fix into = f.Velocity.X * wallNormal.X + f.Velocity.Z * wallNormal.Z;
                    if (into < Fix.Zero)
                    {
                        f.Velocity.X = f.Velocity.X - wallNormal.X * into;
                        f.Velocity.Z = f.Velocity.Z - wallNormal.Z * into;
                    }
                }
            }

            bool inside = Arena.IsInside(next);
            bool wasGrounded = f.Grounded;
            bool wasGroundedForSnap = wasGrounded;
            Fix ground = inside ? Arena.GroundAt(next) : Fix.Zero;
            Fix snapBelow = ground.Raw == 0 ? FloorSnapTolerance : -(wasGroundedForSnap ? Arena.StepHeight : Fix.Milli(120));
            bool landing = next.Y <= ground && previous.Y >= ground + snapBelow;
            // Walking down stairs or off a low step keeps the feet on the ground.
            bool steppingDown = wasGrounded && next.Y > ground && next.Y - ground <= Arena.StepHeight;
            if (inside && (landing || steppingDown) && f.Velocity.Y <= Fix.Zero && !flying)
            {
                next.Y = ground;
                f.Velocity.Y = Fix.Zero;
                f.Grounded = true;
                f.AirDashUsed = false;
                f.RecoilJumps = 0;
                if (!wasGrounded) OnLand(s, index, ref f, ev);
            }
            else
            {
                f.Grounded = false;
                // Flying (Levitate) never sinks into the floor or a platform, even during a diving attack.
                if (flying && inside && next.Y < ground)
                {
                    next.Y = ground;
                    if (f.Velocity.Y < Fix.Zero) f.Velocity.Y = Fix.Zero;
                }
            }

            f.Position = next;
            if (f.Action != FighterAction.RingOut) BlockByPillars(ref f, c);

            if (f.Position.Y < GameConfig.RingOutHeight && f.Action != FighterAction.RingOut)
            {
                SetAction(ref f, FighterAction.RingOut);
                f.Velocity = FixVec3.Zero;
                f.MoveId = -1;
                if (ev != null) ev.Add(s.Tick, SimEventType.RingOut, index, 0, f.Position);
            }
            if (f.Action == FighterAction.RingOut)
            {
                f.Velocity = FixVec3.Zero;
                f.Position.Y = GameConfig.RingOutHeight;
            }
        }

        /// <summary>Keeps a fighter out of the rock spikes and removes velocity pointing into them.</summary>
        private static void BlockByPillars(ref FighterState f, CharacterDef c)
        {
            FixVec3 normal;
            if (!Arena.PushOutOfPillars(ref f.Position, c.Radius, out normal)) return;
            Fix into = f.Velocity.X * normal.X + f.Velocity.Z * normal.Z;
            if (into < Fix.Zero)
            {
                f.Velocity.X = f.Velocity.X - normal.X * into;
                f.Velocity.Z = f.Velocity.Z - normal.Z * into;
            }
        }

        private static void OnLand(GameState s, int index, ref FighterState f, EventBuffer ev)
        {
            if (ev != null) ev.Add(s.Tick, SimEventType.Land, index, (int)f.Action, f.Position);
            switch (f.Action)
            {
                case FighterAction.Air:
                    SetAction(ref f, FighterAction.Idle);
                    break;
                case FighterAction.Attack:
                    MoveDef m = GameContent.GetMove(f.MoveId);
                    if (m != null && m.EndsOnLanding)
                    {
                        f.MoveId = -1;
                        SetAction(ref f, FighterAction.Landing);
                        f.StunTicks = m.LandingRecovery;
                        f.Velocity.X = Fix.Zero;
                        f.Velocity.Z = Fix.Zero;
                    }
                    break;
                case FighterAction.Launched:
                    SetAction(ref f, FighterAction.Knockdown);
                    f.StunTicks = GameConfig.KnockdownTicks;
                    f.Velocity.X = f.Velocity.X * Fix.Half;
                    f.Velocity.Z = f.Velocity.Z * Fix.Half;
                    break;
                case FighterAction.HitStun:
                    break;
            }
        }

        /// <summary>Pushes the two fighters apart so they cannot overlap.</summary>
        public static void Separate(GameState s)
        {
            ref FighterState a = ref s.Fighters[0];
            ref FighterState b = ref s.Fighters[1];
            if (a.Action == FighterAction.RingOut || b.Action == FighterAction.RingOut) return;

            CharacterDef ca = GameContent.GetCharacter(a.CharacterId);
            CharacterDef cb = GameContent.GetCharacter(b.CharacterId);
            Fix dy = Fix.Abs(a.Position.Y - b.Position.Y);
            if (dy > Fix.Max(ca.Height, cb.Height)) return;

            FixVec3 delta = (b.Position - a.Position).Horizontal();
            Fix minDist = ca.Radius + cb.Radius;
            Fix distSq = delta.LengthSquared();
            if (distSq >= minDist * minDist) return;

            Fix dist = Fix.Sqrt(distSq);
            FixVec3 dir = dist > Fix.Zero ? delta / dist : FixVec3.FromAngle(a.Facing);
            Fix push = (minDist - dist) * Fix.Half;
            FixVec3 aBefore = a.Position;
            FixVec3 bBefore = b.Position;
            a.Position = a.Position - dir * push;
            b.Position = b.Position + dir * push;

            if ((IsVoluntary(a.Action) || Arena.IsWallAt(a.Position)) && Arena.IsInside(aBefore) && !Arena.IsInside(a.Position, EdgeMargin)) a.Position = Arena.ClampInside(a.Position, EdgeMargin);
            if ((IsVoluntary(b.Action) || Arena.IsWallAt(b.Position)) && Arena.IsInside(bBefore) && !Arena.IsInside(b.Position, EdgeMargin)) b.Position = Arena.ClampInside(b.Position, EdgeMargin);
            BlockByPillars(ref a, ca);
            BlockByPillars(ref b, cb);
            FixVec3 ignored;
            Arena.PushOutOfTerrain(ref a.Position, ca.Radius, out ignored, a.Grounded ? Arena.StepHeight : Fix.Milli(120), ca.Height);
            Arena.PushOutOfTerrain(ref b.Position, cb.Radius, out ignored, b.Grounded ? Arena.StepHeight : Fix.Milli(120), cb.Height);
        }
    }
}
