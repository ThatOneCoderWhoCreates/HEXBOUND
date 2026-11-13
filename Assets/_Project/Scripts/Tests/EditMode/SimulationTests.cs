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
        public void InputPacking()
        {
            var input = new InputFrame { Buttons = InputButtons.Heavy | InputButtons.LockOn, MoveAngle = 200, HasMove = true };
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
