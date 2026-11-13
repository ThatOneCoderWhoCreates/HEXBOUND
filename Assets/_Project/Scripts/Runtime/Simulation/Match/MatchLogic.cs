namespace HEXBOUND.Simulation
{
    /// <summary>Round flow: intro countdown, fighting, round end, match end.</summary>
    public static class MatchLogic
    {
        public static void StartRound(GameState s, EventBuffer ev)
        {
            s.Phase = MatchPhase.Intro;
            s.PhaseTicks = 0;
            s.RoundTicksLeft = GameConfig.RoundTimeSeconds * GameConfig.TickRate;
            s.RoundWinner = -1;
            s.LastRoundEnd = RoundEndReason.None;
            s.QuietTicks0 = 0;
            s.QuietTicks1 = 0;
            FighterLogic.ResetForRound(ref s.Fighters[0], 0);
            FighterLogic.ResetForRound(ref s.Fighters[1], 1);
            for (int i = 0; i < s.Projectiles.Length; i++)
            {
                s.Projectiles[i].Active = false;
            }
            if (ev != null) ev.Add(s.Tick, SimEventType.RoundIntro, -1, s.Round, FixVec3.Zero);
        }

        public static bool InputsEnabled(GameState s)
        {
            return s.Phase == MatchPhase.Fighting;
        }

        public static void PostStep(GameState s, EventBuffer ev)
        {
            s.PhaseTicks++;
            bool training = s.Setup.Mode == MatchMode.Training;

            switch (s.Phase)
            {
                case MatchPhase.Intro:
                    if (s.PhaseTicks == GameConfig.FightCallTick && ev != null) ev.Add(s.Tick, SimEventType.RoundFight, -1, s.Round, FixVec3.Zero);
                    if (s.PhaseTicks >= GameConfig.IntroTicks)
                    {
                        s.Phase = MatchPhase.Fighting;
                        s.PhaseTicks = 0;
                    }
                    break;

                case MatchPhase.Fighting:
                    if (training) TrainingUpkeep(s);
                    else if (s.RoundTicksLeft > 0) s.RoundTicksLeft--;
                    CheckRoundEnd(s, ev, training);
                    break;

                case MatchPhase.RoundOver:
                    if (s.PhaseTicks >= (training ? 90 : GameConfig.RoundOverTicks))
                    {
                        if (training)
                        {
                            StartRound(s, ev);
                            s.Phase = MatchPhase.Fighting;
                            break;
                        }
                        bool decided = s.Wins0 >= GameConfig.WinsNeeded || s.Wins1 >= GameConfig.WinsNeeded || s.Round >= GameConfig.MaxRounds;
                        if (decided)
                        {
                            s.Phase = MatchPhase.MatchOver;
                            s.PhaseTicks = 0;
                            s.MatchWinner = s.Wins0 > s.Wins1 ? 0 : (s.Wins1 > s.Wins0 ? 1 : 2);
                            if (ev != null) ev.Add(s.Tick, SimEventType.MatchEnd, s.MatchWinner, 0, FixVec3.Zero);
                        }
                        else
                        {
                            s.Round++;
                            StartRound(s, ev);
                        }
                    }
                    break;
            }
        }

        private static void CheckRoundEnd(GameState s, EventBuffer ev, bool training)
        {
            ref FighterState f0 = ref s.Fighters[0];
            ref FighterState f1 = ref s.Fighters[1];
            bool out0 = f0.Action == FighterAction.KO || f0.Action == FighterAction.RingOut;
            bool out1 = f1.Action == FighterAction.KO || f1.Action == FighterAction.RingOut;
            bool timeout = !training && s.RoundTicksLeft <= 0;
            if (!out0 && !out1 && !timeout) return;

            int winner;
            RoundEndReason reason;
            if (out0 && out1)
            {
                winner = 2;
                reason = RoundEndReason.DoubleKO;
            }
            else if (out0 || out1)
            {
                winner = out0 ? 1 : 0;
                ref FighterState loser = ref (out0 ? ref f0 : ref f1);
                reason = loser.Action == FighterAction.RingOut ? RoundEndReason.RingOut : RoundEndReason.KO;
            }
            else
            {
                CharacterDef c0 = GameContent.GetCharacter(f0.CharacterId);
                CharacterDef c1 = GameContent.GetCharacter(f1.CharacterId);
                long p0 = (long)f0.Health * 1000 / c0.MaxHealth;
                long p1 = (long)f1.Health * 1000 / c1.MaxHealth;
                winner = p0 > p1 ? 0 : (p1 > p0 ? 1 : 2);
                reason = RoundEndReason.Timeout;
            }

            s.Phase = MatchPhase.RoundOver;
            s.PhaseTicks = 0;
            s.RoundWinner = winner;
            s.LastRoundEnd = reason;
            if (!training)
            {
                if (winner == 0) s.Wins0++;
                else if (winner == 1) s.Wins1++;
            }
            if (ev != null) ev.Add(s.Tick, SimEventType.RoundEnd, winner, (int)reason, FixVec3.Zero);
        }

        private static void TrainingUpkeep(GameState s)
        {
            s.QuietTicks0++;
            s.QuietTicks1++;
            Refill(ref s.Fighters[0], s.QuietTicks0, s.Setup.InfiniteMagic);
            Refill(ref s.Fighters[1], s.QuietTicks1, s.Setup.InfiniteMagic);
        }

        private static void Refill(ref FighterState f, int quiet, bool infiniteMagic)
        {
            if (quiet >= GameConfig.TrainingRefillDelay)
            {
                f.Health = GameContent.GetCharacter(f.CharacterId).MaxHealth;
            }
            if (infiniteMagic)
            {
                f.Magic = GameConfig.MaxMagic;
                f.CooldownSkill1 = 0;
                f.CooldownSkill2 = 0;
                f.CooldownUltimate = 0;
                f.CooldownRecharge = 0;
            }
        }
    }
}
