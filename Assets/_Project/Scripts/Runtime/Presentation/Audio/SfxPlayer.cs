using System.Collections.Generic;
using HEXBOUND.Core;
using UnityEngine;

namespace HEXBOUND.Presentation
{
    public enum Sfx
    {
        SwingLight,
        SwingHeavy,
        Hit,
        HitHeavy,
        Block,
        Parry,
        GuardBreak,
        Jump,
        Land,
        Dodge,
        Cast,
        Magic,
        Teleport,
        Buff,
        Ko,
        UiClick,
        UiBack,
        Denied,
        Announce,
        Explosion,
        Footstep,
        BodySlam,
        ProjectileHit,
        Clash,
        RocketLaunch,
        PistolShot,
        WeaponThrow,
        WeaponDrop,
        WeaponPickup,
    }

    /// <summary>
    /// Sound effects. Clips are loaded from Resources/Audio/SFX (recorded CC0 foley: Kenney Impact Sounds and RPG Audio, rx_ clips
    /// from OpenGameArt and Kenney; and the game's own wx_ battle set),
    /// matched by file name prefix, with a random variant each time. Any sound without a recorded clip
    /// uses a placeholder synthesized in code.
    /// </summary>
    public sealed class SfxPlayer
    {
        private const int SampleRate = 44100;
        private readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        private readonly Dictionary<Sfx, AudioClip[]> _variants = new Dictionary<Sfx, AudioClip[]>();

        /// <summary>Which recorded clips (file name prefix) are used for each sound.</summary>
        private static readonly KeyValuePair<Sfx, string>[] PackMap =
        {
            // Recorded impacts (Kenney Impact Sounds, CC0).
            new KeyValuePair<Sfx, string>(Sfx.Hit, "impactPunch_medium"),
            new KeyValuePair<Sfx, string>(Sfx.HitHeavy, "impactPunch_heavy"),
            new KeyValuePair<Sfx, string>(Sfx.Block, "rx_block_"),
            new KeyValuePair<Sfx, string>(Sfx.Land, "impactSoft_heavy"),
            new KeyValuePair<Sfx, string>(Sfx.Footstep, "rx_step_"),
            new KeyValuePair<Sfx, string>(Sfx.BodySlam, "impactPlank_medium"),
            new KeyValuePair<Sfx, string>(Sfx.UiClick, "impactGeneric_light"),
            new KeyValuePair<Sfx, string>(Sfx.UiBack, "impactWood_light"),
            new KeyValuePair<Sfx, string>(Sfx.Denied, "impactTin_medium"),
            // Recorded weapon and body foley (CC0, Tools/RecordedSfx): swishes (qubodup), sword swings and clashes
            // (StarNinjas), cloth, knife draws and footsteps (Kenney RPG Audio), metal impacts (Kenney Impact Sounds).
            new KeyValuePair<Sfx, string>(Sfx.SwingLight, "rx_swinglight_"),
            new KeyValuePair<Sfx, string>(Sfx.SwingHeavy, "rx_swingheavy_"),
            new KeyValuePair<Sfx, string>(Sfx.Dodge, "rx_dodge_"),
            new KeyValuePair<Sfx, string>(Sfx.Parry, "rx_parry_"),
            new KeyValuePair<Sfx, string>(Sfx.Clash, "rx_clash_"),
            new KeyValuePair<Sfx, string>(Sfx.WeaponThrow, "rx_throw_"),
            new KeyValuePair<Sfx, string>(Sfx.WeaponDrop, "rx_drop_"),
            new KeyValuePair<Sfx, string>(Sfx.WeaponPickup, "rx_pickup_"),
            // WUSHEN battle set (designed for the game, Tools/BattleSfx): blasts, magic, war drum.
            new KeyValuePair<Sfx, string>(Sfx.Jump, "wx_jump_"),
            new KeyValuePair<Sfx, string>(Sfx.GuardBreak, "wx_guardbreak_"),
            new KeyValuePair<Sfx, string>(Sfx.ProjectileHit, "wx_projectilehit_"),
            new KeyValuePair<Sfx, string>(Sfx.Explosion, "wx_explosion_"),
            new KeyValuePair<Sfx, string>(Sfx.Cast, "wx_cast_"),
            new KeyValuePair<Sfx, string>(Sfx.Magic, "wx_magic_"),
            new KeyValuePair<Sfx, string>(Sfx.Teleport, "wx_teleport_"),
            new KeyValuePair<Sfx, string>(Sfx.Buff, "wx_buff_"),
            new KeyValuePair<Sfx, string>(Sfx.Ko, "wx_ko_"),
            new KeyValuePair<Sfx, string>(Sfx.Announce, "wx_drum_"),
            new KeyValuePair<Sfx, string>(Sfx.RocketLaunch, "wx_rocket_"),
            new KeyValuePair<Sfx, string>(Sfx.PistolShot, "wx_pistol_"),
        };
        private readonly AudioSource[] _sources;
        private int _next;
        private System.Random _random = new System.Random(3);

        public SfxPlayer(Transform parent)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            _sources = new AudioSource[16];
            for (int i = 0; i < _sources.Length; i++)
            {
                _sources[i] = go.AddComponent<AudioSource>();
                _sources[i].playOnAwake = false;
                _sources[i].spatialBlend = 0f;
            }

            _clips[Sfx.SwingLight] = Noise("swing", 0.14f, 2800f, 900f, 0.35f, 0f);
            _clips[Sfx.SwingHeavy] = Noise("swingHeavy", 0.24f, 1600f, 500f, 0.45f, 0f);
            _clips[Sfx.Hit] = Impact("hit", 0.16f, 160f, 0.7f, 0.5f);
            _clips[Sfx.HitHeavy] = Impact("hitHeavy", 0.3f, 90f, 0.9f, 0.65f);
            _clips[Sfx.Block] = Tone("block", 0.18f, 880f, 620f, 0.35f, 0.3f);
            _clips[Sfx.Parry] = Tone("parry", 0.45f, 1760f, 1760f, 0.4f, 0.05f);
            _clips[Sfx.GuardBreak] = Impact("guardBreak", 0.4f, 70f, 1f, 0.8f);
            _clips[Sfx.Jump] = Noise("jump", 0.12f, 1200f, 2400f, 0.2f, 0f);
            _clips[Sfx.Land] = Impact("land", 0.12f, 120f, 0.4f, 0.3f);
            _clips[Sfx.Dodge] = Noise("dodge", 0.2f, 3500f, 1200f, 0.25f, 0f);
            _clips[Sfx.Cast] = Tone("cast", 0.25f, 400f, 1400f, 0.3f, 0.15f);
            _clips[Sfx.Magic] = Tone("magic", 0.5f, 600f, 1200f, 0.28f, 0.1f);
            _clips[Sfx.Teleport] = Tone("teleport", 0.3f, 1800f, 300f, 0.3f, 0.2f);
            _clips[Sfx.Buff] = Tone("buff", 0.8f, 220f, 880f, 0.35f, 0.2f);
            _clips[Sfx.Ko] = Impact("ko", 0.9f, 55f, 1f, 0.9f);
            _clips[Sfx.UiClick] = Tone("click", 0.05f, 1400f, 1200f, 0.2f, 0f);
            _clips[Sfx.UiBack] = Tone("back", 0.07f, 900f, 600f, 0.2f, 0f);
            _clips[Sfx.Denied] = Tone("denied", 0.18f, 180f, 160f, 0.3f, 0.5f);
            _clips[Sfx.Announce] = Tone("announce", 0.6f, 330f, 660f, 0.35f, 0.3f);
            _clips[Sfx.Explosion] = Impact("explosion", 0.6f, 60f, 1f, 0.9f);
            _clips[Sfx.Footstep] = Noise("footstep", 0.06f, 900f, 300f, 0.15f, 0f);
            _clips[Sfx.BodySlam] = Impact("bodySlam", 0.3f, 80f, 0.8f, 0.7f);
            _clips[Sfx.ProjectileHit] = Impact("projectileHit", 0.2f, 200f, 0.6f, 0.5f);
            _clips[Sfx.Clash] = Impact("clash", 0.35f, 120f, 1f, 0.7f);
            LoadPack();
        }

        private void LoadPack()
        {
            AudioClip[] all = Resources.LoadAll<AudioClip>("Audio/SFX");
            if (all == null || all.Length == 0) return;
            foreach (KeyValuePair<Sfx, string> entry in PackMap)
            {
                var matches = new List<AudioClip>();
                foreach (AudioClip clip in all)
                {
                    if (clip != null && clip.name.StartsWith(entry.Value, System.StringComparison.OrdinalIgnoreCase)) matches.Add(clip);
                }
                if (matches.Count > 0) _variants[entry.Key] = matches.ToArray();
            }
        }

        public void Play(Sfx sfx, float volume = 1f, float pitchVariance = 0.06f)
        {
            AudioClip clip;
            AudioClip[] variants;
            if (_variants.TryGetValue(sfx, out variants)) clip = variants[_random.Next(variants.Length)];
            else if (!_clips.TryGetValue(sfx, out clip)) return;
            AudioSource src = _sources[_next];
            _next = (_next + 1) % _sources.Length;
            src.pitch = 1f + ((float)_random.NextDouble() * 2f - 1f) * pitchVariance;
            src.PlayOneShot(clip, volume * GameSettings.Volume);
        }

        private AudioClip Make(string name, float seconds, System.Func<int, float, float> sample)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                data[i] = sample(i, i / (float)SampleRate);
            }
            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Envelope(float t, float length, float attack = 0.005f)
        {
            if (t < attack) return t / attack;
            return Mathf.Clamp01(1f - (t - attack) / Mathf.Max(0.0001f, length - attack));
        }

        /// <summary>Filtered noise with a frequency sweep (whooshes).</summary>
        private AudioClip Noise(string name, float length, float fromHz, float toHz, float gain, float unused)
        {
            float low = 0f;
            var rnd = new System.Random(name.GetHashCode());
            return Make(name, length, (i, t) =>
            {
                float cutoff = Mathf.Lerp(fromHz, toHz, t / length);
                float k = Mathf.Clamp01(cutoff / SampleRate * 6.28f);
                float n = (float)rnd.NextDouble() * 2f - 1f;
                low += (n - low) * k;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / length));
                return low * env * gain * 2.2f;
            });
        }

        /// <summary>Low thump plus a noise crack (hits, impacts).</summary>
        private AudioClip Impact(string name, float length, float baseHz, float noiseAmount, float gain)
        {
            var rnd = new System.Random(name.GetHashCode());
            float phase = 0f;
            return Make(name, length, (i, t) =>
            {
                float freq = baseHz * (1f + 2.5f * Mathf.Exp(-t * 30f));
                phase += freq / SampleRate;
                float body = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 9f);
                float crack = ((float)rnd.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 45f) * noiseAmount;
                return (body + crack) * gain * Envelope(t, length, 0.002f);
            });
        }

        /// <summary>Sine sweep with optional vibrato (magic, UI, parry).</summary>
        private AudioClip Tone(string name, float length, float fromHz, float toHz, float gain, float harmonics)
        {
            float phase = 0f;
            return Make(name, length, (i, t) =>
            {
                float freq = Mathf.Lerp(fromHz, toHz, t / length);
                phase += freq / SampleRate;
                float s = Mathf.Sin(phase * Mathf.PI * 2f) + harmonics * Mathf.Sin(phase * Mathf.PI * 6f);
                return s * gain * Envelope(t, length);
            });
        }
    }
}
