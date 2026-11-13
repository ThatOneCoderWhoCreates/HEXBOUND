using System;
using System.Globalization;
using System.Text;

namespace HEXBOUND.Simulation
{
    /// <summary>What the player did, as the beast sees it.</summary>
    public enum PlayerAct
    {
        None = 0,
        Light,
        Heavy,
        Skill1,
        Skill2,
        Ultimate,
        Grab,
        Dodge,
        Jump,
        Block,
        CrouchAttack,
        CrouchGuard,
        AirAttack,
        Heal,
        Recharge,
    }

    /// <summary>
    /// The beast's long-term memory of one player: which actions they pick in which situation, what they
    /// press after what, how fast they fire skills once the cooldown ends, how they defend, and the beast's level.
    /// Saved between matches (the game stores it as text), so the beast keeps learning and keeps getting harder.
    /// </summary>
    public sealed class BeastMemory
    {
        public const int Acts = 15;
        /// <summary>3 distance bands x 3 beast states.</summary>
        public const int Contexts = 9;
        public const int MaxLevel = 10;
        private const string Version = "beast2";

        /// <summary>0 = first meeting, 10 = hardest. Rises by one every time the player beats it.</summary>
        public int Level;
        public int Matches;
        public int PlayerWins;
        public int BeastWins;

        /// <summary>[context * Acts + act]: how often the player chose each action in each situation.</summary>
        public readonly float[] Situational = new float[Contexts * Acts];
        /// <summary>[previous act * Acts + next act]: what the player does after what (combo and spam habits).</summary>
        public readonly float[] Sequence = new float[Acts * Acts];
        /// <summary>[act]: how the player answers the beast's attacks.</summary>
        public readonly float[] Defense = new float[Acts];
        /// <summary>
        /// [context * Acts + act]: ticks in which the action was available (off cooldown, enough magic) while the
        /// player was free to act. Uses divided by this is how eagerly the player uses it when they can: a skill
        /// fired the moment its cooldown ends has a very high rate even if it is used rarely overall.
        /// </summary>
        public readonly float[] Available = new float[Contexts * Acts];

        /// <summary>Old habits fade a little each match so the beast follows the player's current style.</summary>
        public void Fade(float keep)
        {
            Scale(Situational, keep);
            Scale(Sequence, keep);
            Scale(Defense, keep);
            Scale(Available, keep);
        }

        private static void Scale(float[] a, float k)
        {
            for (int i = 0; i < a.Length; i++) a[i] *= k;
        }

        /// <summary>Records a finished match and raises the level when the player won.</summary>
        public void RecordMatch(bool playerWon)
        {
            Matches++;
            if (playerWon)
            {
                PlayerWins++;
                if (Level < MaxLevel) Level++;
            }
            else
            {
                BeastWins++;
            }
        }

        /// <summary>Total number of player actions remembered (rough measure of how well it knows you).</summary>
        public float Observations
        {
            get
            {
                float n = 0f;
                for (int i = 0; i < Sequence.Length; i++) n += Sequence[i];
                return n;
            }
        }

        /// <summary>The action the player uses most overall, for the menu text.</summary>
        public PlayerAct FavoriteAct()
        {
            float best = 0f;
            PlayerAct act = PlayerAct.None;
            for (int k = 1; k < Acts; k++)
            {
                float n = 0f;
                for (int p = 0; p < Acts; p++) n += Sequence[p * Acts + k];
                if (n > best)
                {
                    best = n;
                    act = (PlayerAct)k;
                }
            }
            return act;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append(Version).Append('|');
            sb.Append(Level).Append(',').Append(Matches).Append(',').Append(PlayerWins).Append(',').Append(BeastWins).Append('|');
            Write(sb, Situational);
            sb.Append('|');
            Write(sb, Sequence);
            sb.Append('|');
            Write(sb, Defense);
            sb.Append('|');
            Write(sb, Available);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, float[] a)
        {
            for (int i = 0; i < a.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(a[i].ToString("0.###", CultureInfo.InvariantCulture));
            }
        }

        /// <summary>Reads saved memory. Returns a fresh memory if the text is empty or from another version.</summary>
        public static BeastMemory Deserialize(string text)
        {
            var m = new BeastMemory();
            if (string.IsNullOrEmpty(text)) return m;
            try
            {
                string[] parts = text.Split('|');
                if (parts.Length != 6 || parts[0] != Version) return m;
                string[] head = parts[1].Split(',');
                m.Level = Math.Max(0, Math.Min(MaxLevel, int.Parse(head[0], CultureInfo.InvariantCulture)));
                m.Matches = int.Parse(head[1], CultureInfo.InvariantCulture);
                m.PlayerWins = int.Parse(head[2], CultureInfo.InvariantCulture);
                m.BeastWins = int.Parse(head[3], CultureInfo.InvariantCulture);
                Read(parts[2], m.Situational);
                Read(parts[3], m.Sequence);
                Read(parts[4], m.Defense);
                Read(parts[5], m.Available);
            }
            catch (FormatException)
            {
                return new BeastMemory();
            }
            catch (IndexOutOfRangeException)
            {
                return new BeastMemory();
            }
            return m;
        }

        private static void Read(string text, float[] a)
        {
            string[] values = text.Split(',');
            int n = Math.Min(values.Length, a.Length);
            for (int i = 0; i < n; i++)
            {
                float v = float.Parse(values[i], CultureInfo.InvariantCulture);
                a[i] = float.IsNaN(v) || v < 0f ? 0f : v;
            }
        }
    }
}
