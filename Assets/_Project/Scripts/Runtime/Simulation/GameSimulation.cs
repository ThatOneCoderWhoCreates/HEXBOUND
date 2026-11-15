namespace HEXBOUND.Simulation
{
    /// <summary>
    /// Entry point of the deterministic simulation. Given the same starting state and the same inputs,
    /// Tick always produces the same result on every machine.
    /// </summary>
    public static class GameSimulation
    {
        public static void Initialize(GameState s, MatchSetup setup, EventBuffer ev)
        {
            s.Tick = 0;
            s.Setup = setup;
            Arena.Use(setup.Map);
            s.Rng = setup.Seed == 0 ? 1u : setup.Seed;
            s.Round = 1;
            s.Wins0 = 0;
            s.Wins1 = 0;
            s.MatchWinner = -1;
            s.NextProjectileSerial = 1;
            s.Fighters[0] = default(FighterState);
            s.Fighters[1] = default(FighterState);
            s.Fighters[0].CharacterId = (byte)setup.Character0;
            s.Fighters[1].CharacterId = (byte)setup.Character1;
            MatchLogic.StartRound(s, ev);
            if (setup.Mode == MatchMode.Training)
            {
                s.Phase = MatchPhase.Fighting;
            }
        }

        public static void Tick(GameState s, InputFrame input0, InputFrame input1, EventBuffer ev)
        {
            Arena.Use(s.Setup.Map);
            Arena.SetBroken(Arena.BrokenMask(s));
            Arena.SetBrokenStructures(Arena.BrokenStructureMask(s));
            s.Tick++;
            if (s.Phase == MatchPhase.MatchOver)
            {
                s.PhaseTicks++;
                return;
            }

            bool enabled = MatchLogic.InputsEnabled(s);
            InputFrame in0 = enabled ? input0 : StripActions(input0);
            InputFrame in1 = enabled ? input1 : StripActions(input1);

            FighterState start0 = s.Fighters[0];
            FighterState start1 = s.Fighters[1];
            FighterLogic.Step(s, 0, in0, ref start1, ev);
            FighterLogic.Step(s, 1, in1, ref start0, ev);
            FighterLogic.Separate(s);
            ProjectileLogic.Step(s, ev);
            CombatResolver.Resolve(s, ev);
            FighterLogic.UpdateThrownWeapons(s, ev);
            MatchLogic.PostStep(s, ev);
        }

        /// <summary>Outside of the fighting phase, buttons still update edge detection but do nothing.</summary>
        private static InputFrame StripActions(InputFrame input)
        {
            InputFrame stripped = default(InputFrame);
            stripped.Buttons = InputButtons.None;
            return stripped;
        }
    }
}
