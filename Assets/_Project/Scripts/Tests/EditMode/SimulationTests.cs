using HEXBOUND.Netcode;
using HEXBOUND.Simulation;
using NUnit.Framework;

namespace HEXBOUND.Tests
{
    /// <summary>
    /// Determinism and rollback tests. Run them in Unity: Window > General > Test Runner > EditMode > Run All.
    /// </summary>
    public class SimulationTests
    {
        private static GameState NewMatch(int c0, int c1, uint seed)
        {
            var s = new GameState();
            GameSimulation.Initialize(s, new MatchSetup { Character0 = c0, Character1 = c1, Seed = seed, Mode = MatchMode.Versus }, null);
            return s;
        }

        private static ulong RunBots(int c0, int c1, uint seed, int ticks)
        {
            GameState s = NewMatch(c0, c1, seed);
            var b0 = new TrainingBot(seed * 3 + 1) { ReactionTicks = 4 };
            var b1 = new TrainingBot(seed * 7 + 5) { ReactionTicks = 4 };
            for (int i = 0; i < ticks; i++)
            {
                GameSimulation.Tick(s, b0.Think(s, 0), b1.Think(s, 1), null);
            }
            return s.Checksum();
        }

        [Test]
        public void FixedPointBasics()
        {
            Assert.AreEqual(Fix.Milli(1500), Fix.FromInt(3) * Fix.Half);
            Assert.AreEqual(Fix.Milli(1500), Fix.FromInt(3) / Fix.FromInt(2));
            Assert.AreEqual(Fix.FromInt(4), Fix.Sqrt(Fix.FromInt(16)));
            Assert.Less(Fix.Abs(FixMath.Sin(FixMath.DegreesToAngle(30)) - Fix.Half).Raw, 100);
            Assert.Less(FixMath.AbsAngleDelta(FixVec3.FromAngle(12000).ToAngle(), 12000), 60);
        }

        [Test]
        public void ArenaEdgeAndSpikesAreConsistent()
        {
            for (int degrees = 0; degrees < 360; degrees += 3)
            {
                FixVec3 dir = FixVec3.FromAngle(FixMath.DegreesToAngle(degrees));
                Assert.IsTrue(Arena.IsInside(dir * Fix.Milli(8500)), "inside at 8.5 units, " + degrees + " degrees");
                Assert.IsFalse(Arena.IsInside(dir * Fix.Milli(14200)), "outside at 14.2 units, " + degrees + " degrees");
                Assert.IsTrue(Arena.IsInside(Arena.ClampInside(dir * Fix.FromInt(20), Fix.Milli(300))), "clamp at " + degrees + " degrees");
            }
            Assert.IsTrue(Arena.IsInside(Arena.Spawn0) && Arena.IsInside(Arena.Spawn1));
            FixVec3 n;
            FixVec3 spawn = Arena.Spawn0;
            Assert.IsFalse(Arena.PushOutOfPillars(ref spawn, Fix.Half, out n), "spawn 0 is clear of spikes");
            spawn = Arena.Spawn1;
            Assert.IsFalse(Arena.PushOutOfPillars(ref spawn, Fix.Half, out n), "spawn 1 is clear of spikes");
            for (int i = 0; i < Arena.Pillars.Length; i++)
            {
                FixVec3 p = Arena.Pillars[i];
                Assert.IsTrue(Arena.PushOutOfPillars(ref p, Fix.Half, out n), "inside spike " + i + " gets pushed");
                Assert.IsFalse(Arena.HitsPillar(p + n * Fix.Milli(10), Fix.Half), "pushed clear of spike " + i);
            }
        }

        [Test]
        public void HealRestoresFivePercentWithCooldown()
        {
            GameState s = NewMatch(0, 1, 5);
            int guard = 0;
            while (s.Phase != MatchPhase.Fighting && guard++ < 2000) GameSimulation.Tick(s, InputFrame.Empty, InputFrame.Empty, null);
            CharacterDef c = GameContent.GetCharacter(s.Fighters[0].CharacterId);
            s.Fighters[0].Health = c.MaxHealth / 2;
            var heal = new InputFrame { Buttons = InputButtons.Heal };
            GameSimulation.Tick(s, heal, InputFrame.Empty, null);
            for (int i = 0; i < 40; i++) GameSimulation.Tick(s, InputFrame.Empty, InputFrame.Empty, null);
            Assert.AreEqual(c.MaxHealth / 2 + c.MaxHealth * 5 / 100, s.Fighters[0].Health, "healed 5%");
            Assert.Greater(s.Fighters[0].CooldownHeal, 0, "heal is on cooldown");

            int before = s.Fighters[0].Health;
            GameSimulation.Tick(s, heal, InputFrame.Empty, null);
            for (int i = 0; i < 40; i++) GameSimulation.Tick(s, InputFrame.Empty, InputFrame.Empty, null);
            Assert.AreEqual(before, s.Fighters[0].Health, "no heal during cooldown");
        }

        [Test]
        public void InputPacking()
        {
            var input = new InputFrame { Buttons = InputButtons.Heavy | InputButtons.LockOn | InputButtons.Heal, MoveAngle = 200, HasMove = true };
            Assert.IsTrue(InputFrame.Unpack(input.Pack()).Equals(input));
        }

        [Test]
        public void SameInputsGiveSameResult([Values(0, 1, 2)] int c0, [Values(0, 1, 2)] int c1)
        {
            uint seed = (uint)(100 + c0 * 3 + c1);
            Assert.AreEqual(RunBots(c0, c1, seed, 6000), RunBots(c0, c1, seed, 6000));
        }

        [Test]
        public void SnapshotRestoreReplaysIdentically()
        {
            GameState s = NewMatch(0, 2, 77);
            var b0 = new TrainingBot(1);
            var b1 = new TrainingBot(2);
            var in0 = new InputFrame[2000];
            var in1 = new InputFrame[2000];
            var snapshot = new GameState();
            for (int i = 0; i < 2000; i++)
            {
                in0[i] = b0.Think(s, 0);
                in1[i] = b1.Think(s, 1);
                if (i == 1000) snapshot.CopyFrom(s);
                GameSimulation.Tick(s, in0[i], in1[i], null);
            }
            for (int i = 1000; i < 2000; i++)
            {
                GameSimulation.Tick(snapshot, in0[i], in1[i], null);
            }
            Assert.AreEqual(s.Checksum(), snapshot.Checksum());
        }

        [Test]
        public void RollbackStaysInSyncOverBadNetwork()
        {
            var net = new SimulatedNetwork(4) { LatencyMs = 100, JitterMs = 30, LossChance = 0.08 };
            var host = new NetMatch(net.A, true, 2, 99);
            var client = new NetMatch(net.B, false, 2);
            var botH = new TrainingBot(11) { ReactionTicks = 5 };
            var botC = new TrainingBot(12) { ReactionTicks = 5 };
            double t = 0;
            bool picked = false;
            while (t < 90)
            {
                double dt = 1.0 / 60;
                t += dt;
                net.NowMs = t * 1000;
                host.Update(dt, () => host.Session == null ? default(InputFrame) : botH.Think(host.Session.State, 0));
                client.Update(dt, () => client.Session == null ? default(InputFrame) : botC.Think(client.Session.State, 1));
                if (!picked && host.State == NetMatchState.CharacterSelect && client.State == NetMatchState.CharacterSelect)
                {
                    host.SetCharacter(0, true);
                    client.SetCharacter(1, true);
                    picked = true;
                }
                if (host.Session != null) host.Session.ClearEvents();
                if (client.Session != null) client.Session.ClearEvents();
            }
            Assert.IsNotNull(host.Session);
            Assert.IsNotNull(client.Session);
            Assert.IsFalse(host.Session.Desynced, "host desync");
            Assert.IsFalse(client.Session.Desynced, "client desync");
            Assert.Greater(host.Session.CurrentTick, 4000);
        }
    }
}
